using System.Runtime.Versioning;

namespace PdvFesta.Core;

/// <summary>
/// Resolve onde os dados ficam. Prioriza %AppData%\FestaJuninaPDV (sobrevive a
/// reinstalacao). Se nao der para escrever la (pen drive read-only, sem perfil),
/// cai para a pasta do executavel. Testavel via injecao de caminhos.
/// </summary>
[SupportedOSPlatform("windows")]
public static class AppPaths
{
    public const string PastaApp = "FestaJuninaPDV";
    public const string NomeBanco = "caixa_festa.db";

    /// <summary>Diretorio de dados efetivo (cria se possivel). Faz fallback se %AppData% falhar.</summary>
    public static string DiretorioDados(string? baseExe = null)
    {
        baseExe ??= AppContext.BaseDirectory;

        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), PastaApp);

        if (TentarCriarEEscrever(appData))
            return appData;

        // Fallback: pasta do executavel (modo portatil / pen drive)
        var local = Path.Combine(baseExe, "dados");
        if (TentarCriarEEscrever(local))
            return local;

        // Ultimo recurso: a propria pasta do exe
        return baseExe;
    }

    public static string CaminhoBanco(string? baseExe = null) =>
        Path.Combine(DiretorioDados(baseExe), NomeBanco);

    /// <summary>Verifica se conseguimos criar a pasta e escrever nela (permissao real).</summary>
    public static bool TentarCriarEEscrever(string dir)
    {
        try
        {
            Directory.CreateDirectory(dir);
            var probe = Path.Combine(dir, ".write_test");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
