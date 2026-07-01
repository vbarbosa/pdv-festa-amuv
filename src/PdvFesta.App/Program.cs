using System.Runtime.Versioning;
using PdvFesta.Core;

namespace PdvFesta.App;

[SupportedOSPlatform("windows")]
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // ---- BLINDAGEM GLOBAL: nenhum erro inesperado derruba o caixa em silencio ----
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (s, e) => Registrar(e.Exception, "ThreadException");
        AppDomain.CurrentDomain.UnhandledException += (s, e) => Registrar(e.ExceptionObject as Exception, "DomainException");

        ApplicationConfiguration.Initialize();

        // Banco em %AppData%\FestaJuninaPDV (sobrevive a reinstalacao);
        // se nao der para escrever la, cai para a pasta do .exe (portatil/pen drive).
        var baseDir = AppContext.BaseDirectory;

        // Override de banco por variavel de ambiente (usado pelos testes E2E para
        // isolar o teste num .db temporario, sem tocar no banco real).
        var dbOverride = Environment.GetEnvironmentVariable("PDVFESTA_DB");
        var dbPath = !string.IsNullOrWhiteSpace(dbOverride)
            ? dbOverride
            : PdvFesta.Core.AppPaths.CaminhoBanco(baseDir);

        // cardapio.json acompanha o .exe (semente inicial do catalogo)
        var cardapioPath = Path.Combine(baseDir, "cardapio.json");

        // Log na mesma pasta do banco (ex: %AppData%\FestaJuninaPDV\logs).
        Log.Inicializar(Path.GetDirectoryName(dbPath) ?? baseDir);
        Log.Info($"===== PDV iniciando ===== (db: {dbPath})");

        try
        {
            using var servico = new Servico(dbPath, cardapioPath);
            Application.Run(new FormVendas(servico));
            Log.Info("===== PDV encerrado normalmente =====");
        }
        catch (Exception ex)
        {
            Registrar(ex, "Startup");
            Avisar("Erro ao iniciar o PDV:\n\n" + ex.Message + "\n\n(Detalhes salvos na pasta logs)",
                "PDV Festa", MessageBoxIcon.Error);
        }
    }

    /// <summary>Registra o erro em arquivo e avisa o operador sem derrubar o app.</summary>
    private static void Registrar(Exception? ex, string origem)
    {
        if (ex is null) return;
        Log.Erro($"({origem}) erro nao tratado", ex);
        Avisar("Ocorreu um erro inesperado, mas o sistema continua funcionando.\n" +
            "Se persistir, reinicie o programa.\n\nDetalhe: " + ex.Message, "Aviso", MessageBoxIcon.Warning);
    }

    /// <summary>
    /// MessageBox seguro: em sessao NAO-interativa (automacao/servico/CI), mostrar dialogo
    /// lanca InvalidOperationException e derrubaria o app EM CASCATA ao reportar um erro.
    /// Aqui usamos DefaultDesktopOnly quando nao ha UserInteractive; se ainda falhar, so loga.
    /// </summary>
    private static void Avisar(string texto, string titulo, MessageBoxIcon icone)
    {
        try
        {
            var opts = Environment.UserInteractive
                ? default
                : MessageBoxOptions.DefaultDesktopOnly;
            MessageBox.Show(texto, titulo, MessageBoxButtons.OK, icone,
                MessageBoxDefaultButton.Button1, opts);
        }
        catch { /* sem UI (headless): o log ja registrou; nunca derrubar por causa do aviso */ }
    }
}
