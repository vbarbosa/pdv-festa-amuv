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
public sealed class VendaE2ETests : IDisposable
{
    private readonly string _dbPath;
    private readonly string _exePath;
    private Process? _proc;

    public VendaE2ETests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"e2e_{Guid.NewGuid():N}.db");
        var baseDir = AppContext.BaseDirectory;
        var repoRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));
        _exePath = Path.Combine(repoRoot, "src", "PdvFesta.App", "bin", "Debug",
                                "net8.0-windows", "win-x64", "PDV-Festa-AMUV.exe");
    }

    public void Dispose()
    {
        try { if (_proc is { HasExited: false }) _proc.Kill(true); } catch { }
        foreach (var ext in new[] { "", "-wal", "-shm" })
            if (File.Exists(_dbPath + ext)) { try { File.Delete(_dbPath + ext); } catch { } }
    }

    private Window SubirApp(UIA3Automation automation)
    {
        Assert.True(File.Exists(_exePath), $"Compile a App primeiro. Nao achei: {_exePath}");
        var psi = new ProcessStartInfo(_exePath) { UseShellExecute = false };
        psi.EnvironmentVariables["PDVFESTA_DB"] = _dbPath;
        _proc = Process.Start(psi)!;

        var app = FlaUI.Core.Application.Attach(_proc);
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
        using var automation = new UIA3Automation();
        var janela = SubirApp(automation);

        AbrirCaixa(automation);

        // Adiciona via ATALHO de teclado (robusto: independe da aba selecionada, ao
        // contrario do clique, que so acha o botao se a aba dele estiver visivel).
        // Atalho 1 = Quentao (700), atalho 3 = Cartela Bingo (600).
        janela.Focus();
        System.Threading.Thread.Sleep(300);
        Keyboard.Type("1");
        System.Threading.Thread.Sleep(300);
        Keyboard.Type("3");
        System.Threading.Thread.Sleep(400);

        // valida que a DataGridView do carrinho renderizou 2 LINHAS
        var grid = RetentarAchar(janela, "gridCarrinho");
        Assert.NotNull(grid);
        Assert.Equal(2, grid!.AsGrid().RowCount);

        // F2 abre o pagamento (modal separado)
        Keyboard.Press(VirtualKeyShort.F2);
        System.Threading.Thread.Sleep(700);

        var txt = RetentarAcharDesktop(automation, "txtRecebido");
        Assert.NotNull(txt);
        txt!.AsTextBox().Enter("20,00");
        System.Threading.Thread.Sleep(300);
        Keyboard.Type(VirtualKeyShort.ENTER);
        System.Threading.Thread.Sleep(900);

        // fecha o app para liberar o arquivo antes de ler o banco
        try { if (!_proc!.HasExited) _proc.Kill(true); } catch { }
        System.Threading.Thread.Sleep(500);

        using var repo = new Repositorio(_dbPath);
        repo.Inicializar();
        var vendas = repo.ListarVendas();
        Assert.Single(vendas);
        Assert.Equal(1300, vendas[0].TotalCentavos);         // 700 + 600
        Assert.Equal(FormaPagamento.Dinheiro, vendas[0].Forma);
        Assert.Equal(700, vendas[0].TrocoCentavos);          // 2000 - 1300
        Assert.NotNull(vendas[0].CaixaId);                   // vinculada ao turno
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
