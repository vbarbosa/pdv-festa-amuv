using System.IO.Ports;
using System.Runtime.Versioning;

namespace PdvFesta.Core;

/// <summary>Descobre impressoras instaladas no Windows e portas COM ativas.</summary>
[SupportedOSPlatform("windows")]
public static class PrinterDiscovery
{
    /// <summary>Nomes de todas as impressoras instaladas no sistema.</summary>
    public static List<string> ListarImpressoras()
    {
        var lista = new List<string>();
        foreach (string? p in System.Drawing.Printing.PrinterSettings.InstalledPrinters)
            if (!string.IsNullOrWhiteSpace(p)) lista.Add(p);
        lista.Sort();
        return lista;
    }

    /// <summary>Portas COM ativas (para impressoras seriais/Bluetooth SPP).</summary>
    public static List<string> ListarPortasCom()
    {
        var portas = SerialPort.GetPortNames().Distinct().ToList();
        portas.Sort();
        return portas;
    }

    /// <summary>Tenta achar automaticamente uma impressora que pareca ser a MPT/termica.</summary>
    public static string? SugerirTermica()
    {
        var impressoras = ListarImpressoras();
        return impressoras.FirstOrDefault(p =>
            p.Contains("MPT", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("POS", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("58", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("Thermal", StringComparison.OrdinalIgnoreCase));
    }
}
