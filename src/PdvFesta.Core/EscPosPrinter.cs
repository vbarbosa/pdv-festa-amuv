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

    #region Comandos ESC/POS (isolados em constantes claras)
    private const byte ESC = 0x1B, GS = 0x1D, LF = 0x0A;

    private static readonly byte[] ESC_INIT = { ESC, 0x40 };            // ESC @  -> reset
    private static readonly byte[] ESC_CODEPAGE_PC860 = { ESC, 0x74, 0x02 }; // ESC t 2
    private static readonly byte[] ESC_ALIGN_LEFT = { ESC, 0x61, 0x00 };
    private static readonly byte[] ESC_ALIGN_CENTER = { ESC, 0x61, 0x01 };
    private static readonly byte[] ESC_NORMAL_FONT = { ESC, 0x21, 0x00 }; // fonte normal
    private static readonly byte[] ESC_DOUBLE_HEIGHT_WIDTH = { ESC, 0x21, 0x30 }; // altura+largura duplas
    private static readonly byte[] GS_CUT_PAPER = { GS, 0x56, 0x42, 0x00 }; // GS V 66 0 -> corte c/ feed minimo
    #endregion

    /// <summary>
    /// Envia bytes crus para a impressora nomeada. Retorna (ok, mensagem).
    /// BLINDADO: qualquer falha de hardware (cabo solto, sem papel, porta ocupada)
    /// vira (false, msg) — NUNCA lanca excecao, para nao derrubar o caixa.
    /// </summary>
    public static (bool ok, string msg) EnviarRaw(string impressora, byte[] dados)
    {
        if (string.IsNullOrWhiteSpace(impressora))
            return (false, "Nenhuma impressora configurada.");

        IntPtr h = IntPtr.Zero;
        try
        {
            if (!OpenPrinter(impressora, out h, IntPtr.Zero))
                return (false, $"Nao foi possivel abrir a impressora '{impressora}' (erro {Marshal.GetLastWin32Error()}).");

            var di = new DOCINFO { pDocName = "Cupom PDV", pDataType = "RAW" };
            if (!StartDocPrinter(h, 1, ref di)) return (false, "StartDocPrinter falhou (sem papel ou porta ocupada?).");
            if (!StartPagePrinter(h)) { EndDocPrinter(h); return (false, "StartPagePrinter falhou."); }

            bool ok = WritePrinter(h, dados, dados.Length, out _);
            EndPagePrinter(h);
            EndDocPrinter(h);
            return ok ? (true, "Impresso com sucesso.") : (false, "WritePrinter falhou (verifique cabo e papel).");
        }
        catch (Exception ex)
        {
            return (false, "Falha de hardware na impressora: " + ex.Message);
        }
        finally
        {
            if (h != IntPtr.Zero) { try { ClosePrinter(h); } catch { } }
        }
    }

    /// <summary>Imprime o ticket de consumo respeitando o modo/layout configurado.</summary>
    public static (bool ok, string msg) ImprimirTicket(string impressora, Venda venda, ConfigCupom cfg)
    {
        var linhas = CupomFormatter.MontarTicket(venda, cfg);
        return EnviarRaw(impressora, MontarBytes(linhas));
    }

    /// <summary>Imprime a Leitura Z (fechamento de turno) na termica.</summary>
    public static (bool ok, string msg) ImprimirFechamentoZ(
        string impressora, ResumoTurno resumo, IEnumerable<ItemVendido> itens, ConfigCupom cfg)
    {
        var linhas = CupomFormatter.MontarFechamentoZ(resumo, itens, cfg);
        return EnviarRaw(impressora, MontarBytes(linhas));
    }

    /// <summary>
    /// Compat: cupom completo simples a partir de um titulo (usado por chamadas legadas).
    /// </summary>
    public static (bool ok, string msg) ImprimirCupom(string impressora, Venda venda, string titulo)
    {
        var cfg = new ConfigCupom { Evento = titulo, Modo = ModoCupom.Completo };
        return ImprimirTicket(impressora, venda, cfg);
    }

    /// <summary>Cupom de teste (status OK) para a tela de configuracao de impressora.</summary>
    public static (bool ok, string msg) ImprimirTeste(string impressora)
    {
        var linhas = new List<LinhaCupom>
        {
            new("PDV FESTA - TESTE", EstiloLinha.Titulo),
            new(CupomFormatter.Divisoria()),
            new(CupomFormatter.LinhaItem("Status", "OK")),
            new(CupomFormatter.LinhaItem("Impressora", "58mm")),
            new(CupomFormatter.LinhaItem("Data", DateTime.Now.ToString("dd/MM HH:mm"))),
            new(CupomFormatter.Divisoria()),
            new(CupomFormatter.Centralizar("Acentos: cao pao acai")),
            new(CupomFormatter.Centralizar("Tudo certo! :)")),
        };
        return EnviarRaw(impressora, MontarBytes(linhas));
    }

    /// <summary>
    /// Converte linhas COM ESTILO em bytes ESC/POS: reset, codepage PC860, e para
    /// cada linha aplica o estilo (titulo dupla+centralizado, expandida dupla, corte de ficha).
    /// Sempre termina com avanco + corte final.
    /// </summary>
    private static byte[] MontarBytes(IReadOnlyList<LinhaCupom> linhas)
    {
        var b = new List<byte>();
        b.AddRange(ESC_INIT);
        b.AddRange(ESC_CODEPAGE_PC860);

        foreach (var linha in linhas)
        {
            switch (linha.Estilo)
            {
                case EstiloLinha.Corte:
                    // separa a ficha: avanco + corte, e reinicia o proximo bloco.
                    b.Add(LF);
                    b.AddRange(GS_CUT_PAPER);
                    break;

                case EstiloLinha.Titulo:
                    b.AddRange(ESC_ALIGN_CENTER);
                    b.AddRange(ESC_DOUBLE_HEIGHT_WIDTH);
                    b.AddRange(Enc.GetBytes(linha.Texto.Trim()));
                    b.Add(LF);
                    b.AddRange(ESC_NORMAL_FONT);
                    b.AddRange(ESC_ALIGN_LEFT);
                    break;

                case EstiloLinha.Expandida:
                    b.AddRange(ESC_DOUBLE_HEIGHT_WIDTH);
                    b.AddRange(Enc.GetBytes(linha.Texto));
                    b.Add(LF);
                    b.AddRange(ESC_NORMAL_FONT);
                    break;

                default: // Normal
                    b.AddRange(Enc.GetBytes(linha.Texto));
                    b.Add(LF);
                    break;
            }
        }

        b.Add(LF); b.Add(LF);           // avanco minimo antes do corte final
        b.AddRange(GS_CUT_PAPER);
        return b.ToArray();
    }
}
