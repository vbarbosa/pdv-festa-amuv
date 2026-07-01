using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace PdvFesta.Core;

/// <summary>
/// Impressao termica NATIVA via ESC/POS RAW enviado direto ao spooler do Windows
/// (winspool.drv / WritePrinter). NAO usa PrintDocument (evita a janela de dialogo,
/// imagens lentas e margens desconfiguradas). Mesma tecnica validada na MPT-II 58mm.
/// </summary>
[SupportedOSPlatform("windows")]
public static class EscPosPrinter
{
    #region P/Invoke winspool.drv
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DOCINFO
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string pDocName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? pOutputFile;
        [MarshalAs(UnmanagedType.LPWStr)] public string pDataType;
    }

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool OpenPrinter(string src, out IntPtr h, IntPtr def);
    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr h);
    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool StartDocPrinter(IntPtr h, int level, ref DOCINFO di);
    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr h);
    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr h);
    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr h);
    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr h, byte[] buf, int count, out int written);
    #endregion

    // Codepage PC860 (Portugues) para acentos corretos na termica.
    private static readonly Encoding Enc;

    static EscPosPrinter()
    {
        // Necessario no .NET moderno para acessar codepages legadas (PC860).
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Enc = Encoding.GetEncoding(860);
    }

    /// <summary>Envia bytes crus para a impressora nomeada. Retorna (ok, mensagem).</summary>
    public static (bool ok, string msg) EnviarRaw(string impressora, byte[] dados)
    {
        if (string.IsNullOrWhiteSpace(impressora))
            return (false, "Nenhuma impressora configurada.");

        if (!OpenPrinter(impressora, out var h, IntPtr.Zero))
            return (false, $"Nao foi possivel abrir a impressora '{impressora}' (erro {Marshal.GetLastWin32Error()}).");

        try
        {
            var di = new DOCINFO { pDocName = "Cupom PDV", pDataType = "RAW" };
            if (!StartDocPrinter(h, 1, ref di)) return (false, "StartDocPrinter falhou.");
            if (!StartPagePrinter(h)) { EndDocPrinter(h); return (false, "StartPagePrinter falhou."); }

            bool ok = WritePrinter(h, dados, dados.Length, out _);
            EndPagePrinter(h);
            EndDocPrinter(h);
            return ok ? (true, "Impresso com sucesso.") : (false, "WritePrinter falhou.");
        }
        finally
        {
            ClosePrinter(h);
        }
    }

    /// <summary>Imprime um cupom de venda formatado 58mm (32 colunas) + corte.</summary>
    public static (bool ok, string msg) ImprimirCupom(string impressora, Venda venda, string titulo)
    {
        var linhas = CupomFormatter.MontarCupom(venda, titulo);
        return EnviarRaw(impressora, MontarBytes(linhas, tituloDestaque: titulo));
    }

    /// <summary>Cupom de teste (status OK) para a tela de configuracao de impressora.</summary>
    public static (bool ok, string msg) ImprimirTeste(string impressora)
    {
        var linhas = new List<string>
        {
            CupomFormatter.Centralizar("PDV FESTA - TESTE"),
            CupomFormatter.Divisoria(),
            CupomFormatter.LinhaItem("Status", "OK"),
            CupomFormatter.LinhaItem("Impressora", "58mm"),
            CupomFormatter.LinhaItem("Data", DateTime.Now.ToString("dd/MM HH:mm")),
            CupomFormatter.Divisoria(),
            CupomFormatter.Centralizar("Acentos: cao pao acai"),
            CupomFormatter.Centralizar("Tudo certo! :)"),
        };
        return EnviarRaw(impressora, MontarBytes(linhas, tituloDestaque: "PDV FESTA - TESTE"));
    }

    /// <summary>
    /// Converte linhas de texto (ja com &lt;=32 col) em bytes ESC/POS:
    /// reset, codepage PC860, titulo em fonte dupla, corpo normal, avanco e corte.
    /// </summary>
    private static byte[] MontarBytes(List<string> linhas, string tituloDestaque)
    {
        const byte ESC = 0x1B, GS = 0x1D;
        var b = new List<byte>();

        b.AddRange(new byte[] { ESC, 0x40 });        // ESC @  -> reset
        b.AddRange(new byte[] { ESC, 0x74, 0x02 });  // ESC t 2 -> codepage PC860

        foreach (var linha in linhas)
        {
            bool ehTitulo = linha.Trim() == tituloDestaque.Trim();
            if (ehTitulo)
            {
                b.AddRange(new byte[] { ESC, 0x61, 0x01 }); // centraliza
                b.AddRange(new byte[] { ESC, 0x21, 0x30 }); // fonte dupla (altura+largura)
                b.AddRange(Enc.GetBytes(linha.Trim() + "\n"));
                b.AddRange(new byte[] { ESC, 0x21, 0x00 }); // fonte normal
                b.AddRange(new byte[] { ESC, 0x61, 0x00 }); // volta a esquerda
            }
            else
            {
                b.AddRange(Enc.GetBytes(linha + "\n"));
            }
        }

        b.AddRange(Enc.GetBytes("\n\n"));            // avanco minimo antes do corte
        b.AddRange(new byte[] { GS, 0x56, 0x42, 0x00 }); // GS V 66 0 -> corte com feed minimo
        return b.ToArray();
    }
}
