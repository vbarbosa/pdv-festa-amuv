using System.Runtime.Versioning;

namespace PdvFesta.App;

[SupportedOSPlatform("windows")]
internal static class Program
{
    [STAThread]
    private static void Main()
    {
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

        try
        {
            using var servico = new Servico(dbPath, cardapioPath);
            Application.Run(new FormVendas(servico));
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Erro ao iniciar o PDV:\n\n" + ex.Message,
                "PDV Festa", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
