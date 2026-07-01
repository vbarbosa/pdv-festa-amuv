using System.IO.Ports;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace PdvFesta.Core;

/// <summary>Descobre impressoras instaladas no Windows e portas COM ativas (USB/Bluetooth).</summary>
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

    /// <summary>
    /// Portas COM ativas ROTULADAS (ex: "COM6 (Bluetooth)", "COM3 (serial)") para o usuario
    /// saber qual e a impressora conectada. O prefixo "COMx" e sempre o inicio da string,
    /// para o EscPosPrinter extrair a porta na hora de imprimir.
    /// </summary>
    public static List<string> ListarPortasCom()
    {
        var bt = PortasBluetooth();
        return SerialPort.GetPortNames()
            .Select(p => p.TrimEnd(':').ToUpperInvariant())
            .Distinct()
            .OrderBy(p => p)
            .Select(p => bt.Contains(p) ? $"{p} (Bluetooth)" : $"{p} (serial)")
            .ToList();
    }

    /// <summary>Portas COM que sao pontes Bluetooth (via HKLM\HARDWARE\DEVICEMAP\SERIALCOMM).</summary>
    private static HashSet<string> PortasBluetooth()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DEVICEMAP\SERIALCOMM");
            if (key is null) return set;
            foreach (var nome in key.GetValueNames())
            {
                var com = key.GetValue(nome)?.ToString()?.TrimEnd(':').ToUpperInvariant();
                if (com is not null &&
                    (nome.Contains("Bth", StringComparison.OrdinalIgnoreCase) ||
                     nome.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase)))
                    set.Add(com);
            }
        }
        catch { /* best-effort: sem rotulo de Bluetooth */ }
        return set;
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
