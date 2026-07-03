using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using PdvFesta.Core;
using Xunit;

namespace PdvFesta.E2E;

/// <summary>
/// E2E da CENTRAL DE IMPRESSORA (F12): abre a tela pelo app real, exercita o semaforo de
/// status ao vivo, os detalhes tecnicos, a fila de impressao e as acoes rapidas (Detectar
/// automaticamente, Limpar fila). Captura screenshots por etapa para evidencia visual.
/// NAO imprime nada fisico: so exercita a UI e valida que a tela abre/opera sem travar.
/// Roda apenas na sandbox Hyper-V (EmSandbox); fora dela e no-op para nao prender o desktop.
/// </summary>
[Collection("e2e")]
public sealed class CentralImpressoraE2ETests : E2ETestBase
{
    private readonly string _dbPath;

    public CentralImpressoraE2ETests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"impr_{Guid.NewGuid():N}.db");
    }

    public override void Dispose()
    {
        base.Dispose();
        foreach (var ext in new[] { "", "-wal", "-shm" })
            if (File.Exists(_dbPath + ext)) { try { File.Delete(_dbPath + ext); } catch { } }
    }

    [Fact]
    public void CentralImpressora_AbreExerciteSemaforoFilaEacoes()
    {
        if (!EmSandbox) return;

        // caixa aberto direto no banco (evita passos de UI fragil so para chegar na F12).
        using (var repo = new Repositorio(_dbPath))
        {
            repo.Inicializar();
            repo.AbrirCaixa(0, "teste");
        }
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        using var automation = new UIA3Automation();
        var janela = SubirApp(automation);

        // ---- abre a Central de Impressora (F12) ----
        janela.Focus();
        Thread.Sleep(400);
        Keyboard.Press(VirtualKeyShort.F12);
        Thread.Sleep(1200);

        var central = RetentarModal(automation, "Central de Impressora");
        Assert.NotNull(central);                       // a tela abriu
        Evidencia("01-central-impressora-aberta");     // semaforo + detalhes + fila visiveis

        // ---- Detectar automaticamente (plug-and-play) ----
        InvocarBotaoPorTexto(central!, "Detectar automaticamente");
        Thread.Sleep(1000);
        Evidencia("02-apos-detectar-automatico");

        // ---- Limpar fila (destrava cupom preso; seguro mesmo com fila vazia) ----
        InvocarBotaoQueComeceCom(central!, "Limpar fila");
        Thread.Sleep(800);
        Evidencia("03-apos-limpar-fila");

        // ---- semaforo continua legivel (PRONTA/ATENCAO/PARADA), sem travar ----
        var textos = central!.FindAllDescendants()
            .Select(e => { try { return e.Name ?? ""; } catch { return ""; } })
            .ToList();
        bool temSemaforo = textos.Any(t =>
            t.Contains("PRONTA") || t.Contains("ATENCAO") || t.Contains("PARADA") || t.Contains("Sem impressora"));
        Assert.True(temSemaforo, "semaforo de status nao encontrado na tela");

        // fecha a Central e o app.
        try { central.AsWindow().Close(); } catch { Keyboard.Press(VirtualKeyShort.ESCAPE); }
        Thread.Sleep(400);
        Evidencia("04-central-fechada");

        try { if (!Proc!.HasExited) Proc.Kill(true); } catch { }
        Thread.Sleep(400);

        // sanidade: o caixa segue aberto (a tela de impressora nao mexe em vendas).
        using var repo2 = new Repositorio(_dbPath);
        repo2.Inicializar();
        Assert.NotNull(repo2.CaixaAberto());
    }

    // ---------- helpers ----------
    private Window SubirApp(UIA3Automation automation)
    {
        Assert.True(File.Exists(ExePath), $"Compile a App primeiro. Nao achei: {ExePath}");
        var psi = new ProcessStartInfo(ExePath) { UseShellExecute = false };
        psi.EnvironmentVariables["PDVFESTA_DB"] = _dbPath;
        Proc = Process.Start(psi)!;
        var app = FlaUI.Core.Application.Attach(Proc);
        Window? janela = null;
        for (int i = 0; i < 30 && janela is null; i++)
        {
            janela = app.GetMainWindow(automation, TimeSpan.FromSeconds(1));
            if (janela is null) Thread.Sleep(500);
        }
        Assert.NotNull(janela);
        return janela!;
    }

    private static Window? RetentarModal(UIA3Automation aut, string titulo)
    {
        for (int i = 0; i < 15; i++)
        {
            var el = aut.GetDesktop().FindFirstDescendant(cf => cf.ByName(titulo));
            if (el is not null) { try { return el.AsWindow(); } catch { return null; } }
            Thread.Sleep(300);
        }
        return null;
    }

    private static void InvocarBotaoPorTexto(AutomationElement raiz, string texto)
    {
        var btn = raiz.FindAllDescendants()
            .FirstOrDefault(e => { try { return (e.Name ?? "").Contains(texto, StringComparison.OrdinalIgnoreCase); } catch { return false; } });
        try { btn?.AsButton().Invoke(); } catch { }
    }

    private static void InvocarBotaoQueComeceCom(AutomationElement raiz, string prefixo)
    {
        var btn = raiz.FindAllDescendants()
            .FirstOrDefault(e => { try { return (e.Name ?? "").StartsWith(prefixo, StringComparison.OrdinalIgnoreCase); } catch { return false; } });
        try { btn?.AsButton().Invoke(); } catch { }
    }
}
