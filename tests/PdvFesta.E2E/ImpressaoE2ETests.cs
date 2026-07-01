using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using PdvFesta.Core;
using Xunit;

namespace PdvFesta.E2E;

/// <summary>
/// Teste de IMPRESSAO pelo PROPRIO APP (FlaUI dirige a UI real): configura a impressora
/// Bluetooth (COM6), abre o caixa, faz UMA venda e a venda e impressa pela EscPosPrinter
/// do app. Requer a impressora MPT-II pareada na COM6 (hardware) -> por isso fica Skip
/// por padrao; rode manualmente com a impressora ligada removendo o Skip.
/// </summary>
[Collection("e2e")]
public sealed class ImpressaoE2ETests : E2ETestBase
{
    private const string PortaImpressora = "COM6 (Bluetooth)";
    private readonly string _dbPath;

    public ImpressaoE2ETests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"e2eprint_{Guid.NewGuid():N}.db");
    }

    public override void Dispose()
    {
        base.Dispose();
        foreach (var ext in new[] { "", "-wal", "-shm" })
            if (File.Exists(_dbPath + ext)) { try { File.Delete(_dbPath + ext); } catch { } }
    }

    [Fact(Skip = "Requer impressora MPT-II real na COM6. Rodar manualmente com a impressora ligada.")]
    public void Venda_ViaApp_ImprimeNaImpressoraBluetooth()
    {
        Assert.True(File.Exists(ExePath), $"Compile a App primeiro. Nao achei: {ExePath}");

        // Pre-configura a impressora e abre o caixa direto no banco (menos passos de UI fragil).
        using (var repo = new Repositorio(_dbPath))
        {
            repo.Inicializar();
            repo.SalvarConfig("impressora", PortaImpressora);
            repo.AbrirCaixa(0, "teste");
        }
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        // Sobe o app (ja com caixa aberto + impressora configurada). Ele semeia o cardapio.
        var psi = new ProcessStartInfo(ExePath) { UseShellExecute = false };
        psi.EnvironmentVariables["PDVFESTA_DB"] = _dbPath;
        Proc = Process.Start(psi)!;

        using var automation = new UIA3Automation();
        var app = FlaUI.Core.Application.Attach(Proc);
        var janela = RetentarObterJanela(app, automation);
        Assert.NotNull(janela);

        // 2x Quentao (atalho 1, duas vezes) + 1x Cartela Bingo (atalho 3) -> testa a
        // tabulacao com item repetido ("2x Quentao"). Paga em dinheiro; o APP imprime na COM6.
        janela!.Focus();
        System.Threading.Thread.Sleep(500);
        Keyboard.Type("1");
        System.Threading.Thread.Sleep(300);
        Keyboard.Type("1");
        System.Threading.Thread.Sleep(300);
        Keyboard.Type("3");
        System.Threading.Thread.Sleep(400);
        Keyboard.Press(VirtualKeyShort.F2);
        System.Threading.Thread.Sleep(800);

        var txt = RetentarAcharDesktop(automation, "txtRecebido");
        Assert.NotNull(txt);
        txt!.AsTextBox().Enter("20,00");
        System.Threading.Thread.Sleep(300);
        Keyboard.Type(VirtualKeyShort.ENTER);
        System.Threading.Thread.Sleep(2000);   // deixa a impressora receber os bytes

        // valida a venda no banco (o cupom fisico o operador confere no papel)
        try { if (!Proc.HasExited) Proc.Kill(true); } catch { }
        System.Threading.Thread.Sleep(500);
        using var repo2 = new Repositorio(_dbPath);
        repo2.Inicializar();
        var vendas = repo2.ListarVendas();
        Assert.Single(vendas);
        Assert.Equal(2000, vendas[0].TotalCentavos);       // 2x700 + 600
        Assert.Equal(FormaPagamento.Dinheiro, vendas[0].Forma);
        Assert.NotNull(vendas[0].CaixaId);
        Assert.Contains(vendas[0].Itens, i => i.Nome == "Quentao" && i.Quantidade == 2);
    }

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
