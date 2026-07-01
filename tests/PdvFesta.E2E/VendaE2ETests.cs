using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using PdvFesta.Core;
using Xunit;

namespace PdvFesta.E2E;

/// <summary>
/// Teste funcional End-to-End com FlaUI: dirige a UI REAL (mouse/teclado) e valida
/// que a venda chega ao banco SQLite. Roda LOCAL (precisa de sessao com desktop).
///
/// Fluxo: abre o app -> abre o caixa -> clica Quentao + Cartela Bingo ->
/// valida 2 linhas na DataGridView -> F2 (pagamento) -> digita recebido -> Enter ->
/// le o SQLite e confere a venda (total, forma, troco, turno).
/// </summary>
[Collection("e2e")]
public sealed class VendaE2ETests : E2ETestBase
{
    private readonly string _dbPath;

    public VendaE2ETests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"e2e_{Guid.NewGuid():N}.db");
    }

    public override void Dispose()
    {
        base.Dispose();   // mata o app e qualquer orfao
        foreach (var ext in new[] { "", "-wal", "-shm" })
            if (File.Exists(_dbPath + ext)) { try { File.Delete(_dbPath + ext); } catch { } }
    }

    private Window SubirApp(UIA3Automation automation)
    {
        Assert.True(File.Exists(ExePath), $"Compile a App primeiro. Nao achei: {ExePath}");
        var psi = new ProcessStartInfo(ExePath) { UseShellExecute = false };
        psi.EnvironmentVariables["PDVFESTA_DB"] = _dbPath;
        Proc = Process.Start(psi)!;

        var app = FlaUI.Core.Application.Attach(Proc);
        var janela = RetentarObterJanela(app, automation);
        Assert.NotNull(janela);
        return janela!;
    }

    /// <summary>Ao iniciar sem caixa, a tela de Abertura aparece (modal): confirma com fundo 0.</summary>
    private void AbrirCaixa(UIA3Automation automation)
    {
        // O modal e uma janela SEPARADA (ShowDialog) -> busca a partir do desktop.
        var btn = RetentarAcharDesktop(automation, "btnAbrirCaixa");
        Assert.NotNull(btn);
        btn!.AsButton().Invoke();
        System.Threading.Thread.Sleep(600);
    }

    [Fact]
    public void FluxoCompleto_AbreCaixa_DoisProdutos_ValidaGrid_Paga_GravaNoBanco()
    {
        if (!EmSandbox) return;   // so roda na VM sandbox (nao trava a maquina em uso)
        using var automation = new UIA3Automation();
        var janela = SubirApp(automation);

        AbrirCaixa(automation);

        // Adiciona 2 itens pela NAVEGACAO por teclado (letra da categoria -> Enter no 1o item).
        // Robusto: NAO assume precos (a ordem alfabetica pode mudar). Valida a CONSISTENCIA:
        // o total que grava no banco == soma dos itens gravados, e recebido/troco batem.
        janela.Focus();
        System.Threading.Thread.Sleep(800);
        Keyboard.Type("b");                              // Bebidas
        System.Threading.Thread.Sleep(700);
        Keyboard.Press(VirtualKeyShort.ENTER);           // adiciona 1o item de Bebidas
        System.Threading.Thread.Sleep(700);
        Keyboard.Type("i");                              // bIngo
        System.Threading.Thread.Sleep(700);
        Keyboard.Press(VirtualKeyShort.ENTER);           // adiciona 1o item de Bingo
        System.Threading.Thread.Sleep(800);

        // valida que a DataGridView do carrinho renderizou 2 LINHAS
        var grid = RetentarAchar(janela, "gridCarrinho");
        Assert.NotNull(grid);
        Assert.Equal(2, grid!.AsGrid().RowCount);

        // F2 abre o pagamento (modal separado)
        Keyboard.Press(VirtualKeyShort.F2);
        System.Threading.Thread.Sleep(700);

        var txt = RetentarAcharDesktop(automation, "txtRecebido");
        Assert.NotNull(txt);
        txt!.AsTextBox().Enter("50,00");                 // paga com 50 (cobre qualquer combinacao)
        System.Threading.Thread.Sleep(300);
        Keyboard.Type(VirtualKeyShort.ENTER);
        System.Threading.Thread.Sleep(900);

        // fecha o app para liberar o arquivo antes de ler o banco
        try { if (!Proc!.HasExited) Proc.Kill(true); } catch { }
        System.Threading.Thread.Sleep(500);

        using var repo = new Repositorio(_dbPath);
        repo.Inicializar();
        var vendas = repo.ListarVendas();
        Assert.Single(vendas);
        var venda = vendas[0];
        Assert.Equal(2, venda.Itens.Count);                                  // 2 itens gravados
        Assert.Equal(venda.Itens.Sum(i => i.SubtotalCentavos), venda.TotalCentavos); // total = soma
        Assert.Equal(FormaPagamento.Dinheiro, venda.Forma);
        Assert.Equal(5000 - venda.TotalCentavos, venda.TrocoCentavos);        // troco coerente
        Assert.NotNull(venda.CaixaId);                                        // vinculada ao turno
    }

    [Fact]
    public void NavegacaoPorTeclado_CategoriaItemEnter_AdicionaEGrava()
    {
        if (!EmSandbox) return;   // so roda na VM sandbox (nao trava a maquina em uso)
        using var automation = new UIA3Automation();
        var janela = SubirApp(automation);
        AbrirCaixa(automation);
        Evidencia("abertura-caixa");

        janela.Focus();
        System.Threading.Thread.Sleep(800);

        // NOVO fluxo: LETRA da categoria -> (Nº do item) -> ENTER adiciona.
        // "B" = Bebidas, Enter adiciona o 1o item.
        Keyboard.Type("b");
        System.Threading.Thread.Sleep(700);
        Keyboard.Press(VirtualKeyShort.ENTER);
        System.Threading.Thread.Sleep(700);

        // "I" = bIngo. "2" destaca o 2o item da aba (valida a selecao por numero). Enter adiciona.
        Keyboard.Type("i");
        System.Threading.Thread.Sleep(700);
        Keyboard.Type("2");
        System.Threading.Thread.Sleep(700);
        Keyboard.Press(VirtualKeyShort.ENTER);
        System.Threading.Thread.Sleep(800);
        Evidencia("itens-no-carrinho");

        // carrinho deve ter 2 linhas (1 Bebida + 1 Bingo)
        var grid = RetentarAchar(janela, "gridCarrinho");
        Assert.NotNull(grid);
        Assert.Equal(2, grid!.AsGrid().RowCount);

        // paga (F2 -> recebido -> Enter)
        Keyboard.Press(VirtualKeyShort.F2);
        System.Threading.Thread.Sleep(700);
        var txt = RetentarAcharDesktop(automation, "txtRecebido");
        Assert.NotNull(txt);
        txt!.AsTextBox().Enter("50,00");
        System.Threading.Thread.Sleep(300);
        Evidencia("pagamento-troco");
        Keyboard.Type(VirtualKeyShort.ENTER);
        System.Threading.Thread.Sleep(900);
        Evidencia("venda-concluida");

        try { if (!Proc!.HasExited) Proc.Kill(true); } catch { }
        System.Threading.Thread.Sleep(500);

        using var repo = new Repositorio(_dbPath);
        repo.Inicializar();
        var vendas = repo.ListarVendas();
        Assert.Single(vendas);
        // robusto: 2 itens gravados e o total bate com a soma (sem depender de precos fixos).
        Assert.Equal(2, vendas[0].Itens.Count);
        Assert.Equal(vendas[0].Itens.Sum(i => i.SubtotalCentavos), vendas[0].TotalCentavos);
    }

    // ---- helpers de robustez (a UI pode demorar a aparecer) ----
    private static Window? RetentarObterJanela(FlaUI.Core.Application app, UIA3Automation aut)
    {
        for (int i = 0; i < 30; i++)
        {
            var w = app.GetMainWindow(aut, TimeSpan.FromSeconds(1));
            if (w is not null) return w;
            System.Threading.Thread.Sleep(500);
        }
        return null;
    }

    private static AutomationElement? RetentarAchar(AutomationElement raiz, string automationId)
    {
        for (int i = 0; i < 20; i++)
        {
            var el = raiz.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
            if (el is not null) return el;
            System.Threading.Thread.Sleep(400);
        }
        return null;
    }

    /// <summary>Busca a partir do DESKTOP (para janelas modais separadas do app).</summary>
    private static AutomationElement? RetentarAcharDesktop(UIA3Automation aut, string automationId)
    {
        for (int i = 0; i < 20; i++)
        {
            var el = aut.GetDesktop().FindFirstDescendant(cf => cf.ByAutomationId(automationId));
            if (el is not null) return el;
            System.Threading.Thread.Sleep(400);
        }
        return null;
    }
}
