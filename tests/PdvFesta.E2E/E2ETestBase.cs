using System.Diagnostics;

namespace PdvFesta.E2E;

/// <summary>
/// Base de TODOS os testes E2E. Garante ISOLAMENTO robusto e automatico:
///   - No construtor (antes de cada teste): mata qualquer PDV orfao que tenha sobrado
///     de um teste anterior que falhou no meio. Assim NUNCA rodam 2 apps ao mesmo tempo.
///   - No Dispose (depois de cada teste): mata o app deste teste E qualquer outro PDV,
///     mesmo que o teste tenha estourado excecao antes do Kill explicito.
/// Combinado com xunit.runner.json (serial, 1 thread), a suite roda 1 app por vez.
/// </summary>
public abstract class E2ETestBase : IDisposable
{
    /// <summary>
    /// Motivo do Skip dos testes E2E de UI. Eles PASSAM isolados, mas dependem de uma
    /// sessao de desktop ATIVA/DEDICADA e em foco (limitacao do FlaUI/UIA3): em maquina
    /// compartilhada (uso normal, 2o monitor) o app nao recebe foco e o teste falha de
    /// forma nao-deterministica. A logica de negocio esta coberta pelos testes unitarios.
    /// Para rodar: remova o Skip e execute com a tela ativa, sem usar o PC.
    /// </summary>
    public const string SkipUI =
        "E2E de UI: requer desktop dedicado em foco. Rodar manualmente em maquina livre.";

    protected const string ProcName = "PDV-Festa-AMUV";
    protected readonly string ExePath;
    protected Process? Proc;

    protected E2ETestBase()
    {
        MatarTodosOsApps();   // limpa orfaos ANTES de comecar (porteiro de entrada)

        var baseDir = AppContext.BaseDirectory;
        var repoRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));
        ExePath = Path.Combine(repoRoot, "src", "PdvFesta.App", "bin", "Debug",
                               "net8.0-windows", "win-x64", "PDV-Festa-AMUV.exe");
    }

    /// <summary>Mata TODO processo do PDV na maquina — porteiro contra janela orfa.</summary>
    protected static void MatarTodosOsApps()
    {
        foreach (var p in Process.GetProcessesByName(ProcName))
        {
            try { p.Kill(true); p.WaitForExit(3000); } catch { }
        }
    }

    public virtual void Dispose()
    {
        try { if (Proc is { HasExited: false }) Proc.Kill(true); } catch { }
        MatarTodosOsApps();   // garante que nada sobra, mesmo apos falha no meio do teste
    }
}
