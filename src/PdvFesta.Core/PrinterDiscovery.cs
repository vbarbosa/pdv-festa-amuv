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

    /// <summary>Uma impressora parece ser uma termica de cupom (MPT/POS/58mm/thermal/ZJiang)?</summary>
    internal static bool PareceTermica(string nome) =>
        nome.Contains("MPT", StringComparison.OrdinalIgnoreCase) ||
        nome.Contains("POS", StringComparison.OrdinalIgnoreCase) ||
        nome.Contains("58", StringComparison.OrdinalIgnoreCase) ||
        nome.Contains("80", StringComparison.OrdinalIgnoreCase) ||
        nome.Contains("Thermal", StringComparison.OrdinalIgnoreCase) ||
        nome.Contains("ZJiang", StringComparison.OrdinalIgnoreCase) ||
        nome.Contains("Receipt", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Tenta achar automaticamente a impressora termica, com PRIORIDADE:
    ///  1) termica instalada (USB) que esteja ONLINE (nao offline/erro);
    ///  2) qualquer termica instalada (USB), mesmo sem status claro;
    ///  3) porta COM Bluetooth (fallback).
    /// USB tem prioridade sobre Bluetooth (mais confiavel para a festa).
    /// </summary>
    public static string? SugerirTermica()
    {
        var termicas = ListarImpressoras().Where(PareceTermica).ToList();

        // 1) termica USB online
        var online = termicas.FirstOrDefault(EstaOnline);
        if (online is not null) return online;

        // 2) qualquer termica USB instalada
        if (termicas.Count > 0) return termicas[0];

        // 3) fallback Bluetooth (primeira porta COM rotulada Bluetooth)
        return ListarPortasCom().FirstOrDefault(p => p.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// A fila de impressao esta ONLINE? (nao marcada WorkOffline). Retorna true para portas
    /// COM/Bluetooth (nao tem fila do Windows). Best-effort: em duvida, considera online.
    /// </summary>
    public static bool EstaOnline(string alvo)
    {
        if (string.IsNullOrWhiteSpace(alvo)) return false;
        if (alvo.StartsWith("COM", StringComparison.OrdinalIgnoreCase)) return true;  // serial/BT
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                $"SELECT WorkOffline FROM Win32_Printer WHERE Name = '{alvo.Replace("'", "''")}'");
            foreach (var o in searcher.Get())
            {
                var off = o["WorkOffline"];
                if (off is bool b) return !b;   // online = NAO offline
            }
        }
        catch { /* WMI indisponivel: assume online */ }
        return true;
    }
}
