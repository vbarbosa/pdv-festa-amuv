using System.Runtime.Versioning;

namespace PdvFesta.App;

/// <summary>
/// Identidade visual da AMUV (Manchete United). Cores fortes usadas com PARCIMONIA:
/// so em cabecalhos/titulos/icone, para dar marca sem poluir a UI do caixa.
/// </summary>
[SupportedOSPlatform("windows")]
public static class Marca
{
    public static readonly Color Vermelho = Color.FromArgb(0xC8, 0x10, 0x2E); // #C8102E
    public static readonly Color Amarelo = Color.FromArgb(0xFF, 0xB8, 0x00);  // #FFB800
    public const string Nome = "ARRAIA DA AMUV";

    /// <summary>Carrega o icone do app (app.ico ao lado do exe), ou null se faltar.</summary>
    public static Icon? Icone()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "app.ico");
            return File.Exists(path) ? new Icon(path) : null;
        }
        catch { return null; }
    }

    /// <summary>Carrega a logo PNG (assets ou ao lado do exe), ou null.</summary>
    public static Image? Logo()
    {
        foreach (var nome in new[] { "logo-amuv.png", "app-logo.png" })
        {
            var p = Path.Combine(AppContext.BaseDirectory, nome);
            if (File.Exists(p)) { try { return Image.FromFile(p); } catch { } }
        }
        return null;
    }
}
