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

    /// <summary>
    /// Estamos num ambiente onde e SEGURO dirigir a UI? True quando PDV_E2E_EVID esta setada
    /// (o orquestrador Hyper-V seta isso dentro da VM sandbox dedicada). Fora dela, os testes
    /// que checam isto retornam cedo — nao travam a maquina em uso.
    /// </summary>
    protected static bool EmSandbox =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PDV_E2E_EVID"));

    protected const string ProcName = "PDV-Festa-AMUV";
    protected readonly string ExePath;
    protected Process? Proc;

    protected E2ETestBase()
    {
        MatarTodosOsApps();   // limpa orfaos ANTES de comecar (porteiro de entrada)
        ExePath = ResolverExe();
    }

    /// <summary>
    /// Acha o PDV-Festa-AMUV.exe testando, em ordem: (1) o app INSTALADO pelo Setup
    /// (%LocalAppData%\Programs\FestaJuninaPDV) — caso da sandbox Hyper-V; (2) o build do
    /// repositorio (uso local). Assim o mesmo teste roda na VM e no dev sem ajuste.
    /// </summary>
    private static string ResolverExe()
    {
        var instalado = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "FestaJuninaPDV", "PDV-Festa-AMUV.exe");
        if (File.Exists(instalado)) return instalado;

        var baseDir = AppContext.BaseDirectory;
        var repoRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));
        return Path.Combine(repoRoot, "src", "PdvFesta.App", "bin", "Debug",
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

    // ---------- evidencias (screenshots por etapa) ----------
    // So captura quando PDV_E2E_EVID aponta uma pasta (setada pelo orquestrador Hyper-V).
    // Em execucao local normal, e um no-op — nao atrapalha nem exige nada.
    private static readonly string? EvidDir = Environment.GetEnvironmentVariable("PDV_E2E_EVID");
    private static int _seq;

    /// <summary>Tira um screenshot da tela e salva como "NN-etapa.png" na pasta de evidencias.</summary>
    protected static void Evidencia(string etapa)
    {
        if (string.IsNullOrWhiteSpace(EvidDir)) return;
        try
        {
            Directory.CreateDirectory(EvidDir);
            int n = System.Threading.Interlocked.Increment(ref _seq);
            var limpo = string.Concat(etapa.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_'));
            var arquivo = Path.Combine(EvidDir, $"{n:00}-{limpo}.png");

            var bounds = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
            using var bmp = new System.Drawing.Bitmap(bounds.Width, bounds.Height);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
                g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size);
            bmp.Save(arquivo, System.Drawing.Imaging.ImageFormat.Png);
        }
        catch { /* evidencia e best-effort: nunca derruba o teste */ }
    }
}
