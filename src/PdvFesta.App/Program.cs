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
            MessageBox.Show(
                "Erro ao iniciar o PDV:\n\n" + ex.Message +
                "\n\n(Detalhes salvos na pasta logs)",
                "PDV Festa", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>Registra o erro em arquivo e avisa o operador sem derrubar o app.</summary>
    private static void Registrar(Exception? ex, string origem)
    {
        if (ex is null) return;
        Log.Erro($"({origem}) erro nao tratado", ex);

        MessageBox.Show(
            "Ocorreu um erro inesperado, mas o sistema continua funcionando.\n" +
            "Se persistir, reinicie o programa.\n\nDetalhe: " + ex.Message,
            "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
