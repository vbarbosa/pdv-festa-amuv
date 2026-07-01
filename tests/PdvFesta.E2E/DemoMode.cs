using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using PdvFesta.Core;
using Xunit;

namespace PdvFesta.E2E;

/// <summary>
/// MODO DEMONSTRACAO (ator do video). Opera o app DEVAGAR e de forma didatica para o
/// orquestrador (Gerar-VideoTreinamento.ps1) gravar a tela.
///
/// IMPORTANTE: nao usa mouse fisico nem teclado — tudo via UI Automation Invoke()/Enter().
/// Assim NAO rouba o mouse/teclado do usuario; o app "opera sozinho" na tela. So executa
/// sob PDV_DEMO=1 (senao passa trivialmente, sem atrapalhar CI/local).
/// Roteiro: Cena 1 caixa livre -> 2 adiciona itens -> 3 paga/troco -> 4 fechamento.
/// </summary>
[Collection("e2e")]
public sealed class DemoMode : IDisposable
{
    private readonly string _dbPath;
    private readonly string _exePath;
    private Process? _proc;

    public DemoMode()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"demo_{Guid.NewGuid():N}.db");
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

    [Fact]
    public void GravarDemonstracao()
    {
        if (Environment.GetEnvironmentVariable("PDV_DEMO") != "1") return;   // gate do orquestrador
        Assert.True(File.Exists(_exePath), $"Compile a App (Debug win-x64) antes. Nao achei: {_exePath}");

        using (var repo = new Repositorio(_dbPath))
        {
            repo.Inicializar();
            repo.AbrirCaixa(0, "Demonstracao");   // ja abre o caixa (sem impressora)
        }
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        var psi = new ProcessStartInfo(_exePath) { UseShellExecute = false };
        psi.EnvironmentVariables["PDVFESTA_DB"] = _dbPath;
        _proc = Process.Start(psi)!;

        using var automation = new UIA3Automation();
        var app = FlaUI.Core.Application.Attach(_proc);
        var janela = Esperar(() => app.GetMainWindow(automation, TimeSpan.FromSeconds(1)));
        Assert.NotNull(janela);
        try { janela!.Focus(); janela.SetForeground(); } catch { }

        // ===== CENA 1: caixa livre =====
        Pausa(2800);

        // ===== CENA 2: adiciona 1x Quentao e 2x Cartela Bingo (via Invoke, sem mouse) =====
        Invocar(janela!, "btnProdutoTodos_quentao"); Pausa(1300);
        Invocar(janela!, "btnProdutoTodos_bingo1");  Pausa(1000);
        Invocar(janela!, "btnProdutoTodos_bingo1");  Pausa(1600);

        // ===== CENA 3: pagar + troco automatico =====
        Invocar(janela!, "btnPagar"); Pausa(1600);
        var txt = EsperarDesktop(automation, "txtRecebido");
        txt?.AsTextBox().Enter("50,00");   // total 2000 -> troco 3000 (mostra o troco grande)
        Pausa(2200);
        EsperarDesktop(automation, "btnConfirmar")?.AsButton().Invoke();
        Pausa(2000);

        // ===== CENA 4: fechamento de caixa (menu -> senha 0000 -> resumo em verde) =====
        try
        {
            var menu = janela!.FindFirstDescendant(cf => cf.ByControlType(ControlType.MenuBar));
            var arquivo = menu?.FindFirstChild(cf => cf.ByName("Arquivo"))?.AsMenuItem();
            arquivo?.Expand();
            Pausa(600);
            var fech = arquivo?.Items.FirstOrDefault(i => i.Name.Contains("Fechamento"));
            fech?.Invoke();
            Pausa(1200);

            var senha = EsperarDesktop(automation, "txtSenhaAdmin");
            if (senha is not null)
            {
                senha.AsTextBox().Enter("0000");
                Pausa(600);
                EsperarDesktop(automation, "btnConfirmarSenha")?.AsButton().Invoke();
                Pausa(3000);   // dashboard com totais em verde na tela
            }
        }
        catch { /* fechamento e cosmetico; se falhar, o video ja tem as cenas principais */ }

        Pausa(1500);
    }

    // ---------- helpers (sem mouse/teclado fisico) ----------
    private static void Pausa(int ms) => System.Threading.Thread.Sleep(ms);

    private static void Invocar(AutomationElement janela, string automationId)
    {
        var el = Esperar(() => janela.FindFirstDescendant(cf => cf.ByAutomationId(automationId)));
        try { el?.AsButton().Invoke(); } catch { }
    }

    private static T? Esperar<T>(Func<T?> obter, int tentativas = 30) where T : class
    {
        for (int i = 0; i < tentativas; i++)
        {
            var v = obter();
            if (v is not null) return v;
            System.Threading.Thread.Sleep(400);
        }
        return null;
    }

    private static AutomationElement? EsperarDesktop(UIA3Automation aut, string automationId) =>
        Esperar(() => aut.GetDesktop().FindFirstDescendant(cf => cf.ByAutomationId(automationId)));
}
