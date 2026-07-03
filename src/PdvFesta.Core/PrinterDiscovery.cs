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
    public static bool EstaOnline(string alvo) => StatusFila(alvo).PareceOnline;

    /// <summary>Estado apurado de uma fila de impressao (ou porta COM).</summary>
    public readonly record struct StatusImpressora(bool PareceOnline, bool ConfirmavelSoImprimindo, string Descricao);

    /// <summary>
    /// Le o estado real da fila via WMI: alem do WorkOffline (toggle manual), tambem o
    /// PrinterStatus/DetectedErrorState e jobs em erro. IMPORTANTE: impressora USB barata
    /// (MPT/POS) NAO reporta desligamento/cabo removido — o Windows so descobre ao mandar
    /// o job. Por isso, para fila USB, o status "pronta" e PROVISORIO (ConfirmavelSoImprimindo
    /// = true): so o teste de impressao confirma de verdade. Nao mentimos "PRONTA".
    /// </summary>
    public static StatusImpressora StatusFila(string alvo)
    {
        if (string.IsNullOrWhiteSpace(alvo))
            return new(false, false, "Nenhuma impressora selecionada.");

        // porta serial/Bluetooth: existir a porta ja indica pareamento; sem fila do Windows.
        if (alvo.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
            return new(true, true, "Porta pareada — o envio confirma de verdade.");

        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                $"SELECT WorkOffline, PrinterStatus, PrinterState, DetectedErrorState " +
                $"FROM Win32_Printer WHERE Name = '{alvo.Replace("'", "''")}'");
            foreach (var o in searcher.Get())
            {
                bool offline = o["WorkOffline"] is bool b && b;
                int erro = o["DetectedErrorState"] is uint e ? (int)e : 0;   // 0/2 = ok/desconhecido

                // DetectedErrorState: 3=baixo papel,4=sem papel,5=baixo toner,6=sem toner,
                // 7=porta aberta,8=atolamento,9=servico,10=saida cheia,11=nao disponivel.
                string? falha = erro switch
                {
                    4 => "SEM PAPEL — troque a bobina.",
                    8 => "PAPEL ATOLADO — verifique o mecanismo.",
                    7 => "TAMPA ABERTA — feche a impressora.",
                    11 => "INDISPONIVEL — verifique cabo/energia.",
                    _ => null
                };
                if (offline)
                    return new(false, false, "OFFLINE — verifique cabo/energia (ou 'usar offline' no Windows).");
                if (falha is not null)
                    return new(false, false, falha);

                // sem erro conhecido: a fila esta pronta, MAS USB nao reporta desconexao fisica.
                if (TemJobEmErro(alvo))
                    return new(false, false, "ULTIMO CUPOM FALHOU — impressora provavelmente desligada.");
                return new(true, true, "Instalada — o status real so e confirmado ao imprimir.");
            }
        }
        catch { /* WMI indisponivel */ }
        return new(true, true, "Instalada — o status real so e confirmado ao imprimir.");
    }

    /// <summary>Ha job travado em erro na fila? (sinal forte de aparelho desligado/sem papel).</summary>
    private static bool TemJobEmErro(string alvo)
    {
        try
        {
            using var jobs = new System.Management.ManagementObjectSearcher(
                $"SELECT JobStatus FROM Win32_PrintJob WHERE Name LIKE '{alvo.Replace("'", "''")},%'");
            foreach (var j in jobs.Get())
            {
                var s = j["JobStatus"]?.ToString() ?? "";
                if (s.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
                    s.Contains("Offline", StringComparison.OrdinalIgnoreCase) ||
                    s.Contains("Retained", StringComparison.OrdinalIgnoreCase) ||
                    s.Contains("Blocked", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch { }
        return false;
    }
}
