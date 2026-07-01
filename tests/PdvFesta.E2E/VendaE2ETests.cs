using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using PdvFesta.Core;
using Xunit;

namespace PdvFesta.E2E;

/// <summary>
/// Teste funcional End-to-End com FlaUI: dirige a UI REAL (mouse/teclado)
/// e valida que a venda chega ao banco SQLite. Roda LOCAL (precisa de sessao
/// com desktop); no CI headless ficam os testes unitarios.
///
/// Fluxo: abre o app -> clica no Quentao -> F2 (pagamento) -> digita recebido
/// -> Enter (confirma) -> le o SQLite e confere a venda.
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

        // Usa o build de Debug da App (gerado por dotnet build).
        var baseDir = AppContext.BaseDirectory; // .../tests/PdvFesta.E2E/bin/Debug/net8.0-windows
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

    [Fact]
    public void FluxoCompleto_ClicaProduto_Paga_GravaNoBanco()
    {
        Assert.True(File.Exists(_exePath), $"Compile a App primeiro. Nao achei: {_exePath}");

        // 1) sobe o app apontando para um banco temporario isolado
        var psi = new ProcessStartInfo(_exePath) { UseShellExecute = false };
        psi.EnvironmentVariables["PDVFESTA_DB"] = _dbPath;
        // cardapio ao lado do exe garante o seed
        _proc = Process.Start(psi)!;

        using var automation = new UIA3Automation();
        var app = FlaUI.Core.Application.Attach(_proc);
        var janela = RetentarObterJanela(app, automation);
        Assert.NotNull(janela);

        // 2) clica no botao do Quentao (AutomationId = btnProduto_quentao)
        var btnQuentao = RetentarAchar(janela!, "btnProduto_quentao");
        Assert.NotNull(btnQuentao);
        btnQuentao!.AsButton().Invoke();

        // clica de novo -> 2 quentoes
        btnQuentao.AsButton().Invoke();

        // 3) F2 abre o pagamento
        Keyboard.Press(VirtualKeyShort.F2);
        System.Threading.Thread.Sleep(600);

        var formPag = RetentarAchar(janela!, "txtRecebido", profundidade: true);
        Assert.NotNull(formPag);

        // dinheiro ja vem selecionado; digita valor recebido e confirma
        formPag!.AsTextBox().Enter("20,00");
        System.Threading.Thread.Sleep(300);
        Keyboard.Type(VirtualKeyShort.ENTER);
        System.Threading.Thread.Sleep(800);

        // 4) valida no banco: 1 venda de 2 x Quentao (700) = 1400, dinheiro
        // fecha o app para liberar o arquivo antes de ler
        try { if (!_proc.HasExited) _proc.Kill(true); } catch { }
        System.Threading.Thread.Sleep(500);

        using var repo = new Repositorio(_dbPath);
        repo.Inicializar();
        var vendas = repo.ListarVendas();
        Assert.Single(vendas);
        Assert.Equal(1400, vendas[0].TotalCentavos);
        Assert.Equal(FormaPagamento.Dinheiro, vendas[0].Forma);
        Assert.Equal(600, vendas[0].TrocoCentavos); // 2000 - 1400
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

    private static AutomationElement? RetentarAchar(AutomationElement raiz, string automationId, bool profundidade = false)
    {
        for (int i = 0; i < 20; i++)
        {
            var el = raiz.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
            if (el is not null) return el;
            System.Threading.Thread.Sleep(400);
        }
        return null;
    }
}
