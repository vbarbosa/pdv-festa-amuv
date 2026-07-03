using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;

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

    /// <summary>
    /// Converte o texto do cupom em bytes para a termica, REMOVENDO acentos antes.
    /// Impressoras termicas baratas (POS58 etc.) frequentemente cospem lixo em acentos;
    /// imprimir em ASCII puro ("PAO", "ACAI", "CAFE") sai limpo em QUALQUER modelo.
    /// A remocao afeta SO a impressao — a tela continua com acentuacao normal.
    /// </summary>
    private static byte[] Bytes(string texto) => Enc.GetBytes(RemoverAcentos(texto));

    /// <summary>Troca cada caractere acentuado pela sua base ASCII (á->a, ç->c, ã->a...).</summary>
    public static string RemoverAcentos(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return texto;
        var norm = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(norm.Length);
        foreach (var ch in norm)
        {
            var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }
        // 'ç' ja vira 'c' pela decomposicao; garante alguns casos especiais restantes.
        return sb.ToString().Normalize(NormalizationForm.FormC)
                 .Replace('ª', 'a').Replace('º', 'o');
    }

    #region Comandos ESC/POS (isolados em constantes claras)
    private const byte ESC = 0x1B, GS = 0x1D, LF = 0x0A;

    private static readonly byte[] ESC_INIT = { ESC, 0x40 };            // ESC @  -> reset
    private static readonly byte[] ESC_CODEPAGE_PC860 = { ESC, 0x74, 0x02 }; // ESC t 2
    // ESC 3 n -> espacamento entre linhas = n dots. Era 26; +10% => 29 (mais respiro entre
    // linhas/vales sem apertar). Ainda economico vs. o padrao ~30-34.
    private static readonly byte[] ESC_LINE_SPACING = { ESC, 0x33, 29 };
    private static readonly byte[] ESC_ALIGN_LEFT = { ESC, 0x61, 0x00 };
    private static readonly byte[] ESC_ALIGN_CENTER = { ESC, 0x61, 0x01 };
    private static readonly byte[] ESC_NORMAL_FONT = { ESC, 0x21, 0x00 }; // fonte normal
    // Titulo: altura+largura duplas (impacto no cabecalho).
    private static readonly byte[] ESC_DOUBLE_HEIGHT_WIDTH = { ESC, 0x21, 0x30 };
    // Vales/expandida: SO altura dupla (largura normal) => fonte menor/mais estreita que a
    // 2x2, mas ainda grande e legivel de longe. Cabe o nome todo sem quebrar tanto.
    private static readonly byte[] ESC_DOUBLE_HEIGHT = { ESC, 0x21, 0x10 };
    // GS V 1 -> corte parcial SEM avanco extra. E o comando que a MPT-II digere (testado): a
    // variante 'GS V 66 n' (avanco parametrizado) TRAVA o firmware dela. O corte sai logo apos
    // a ultima linha; o residuo de papel branco restante e a distancia fisica cabeca->lamina.
    private static readonly byte[] GS_CUT_PAPER = { GS, 0x56, 0x01 };
    #endregion

    /// <summary>
    /// Envia bytes crus para o alvo escolhido. Funciona por DOIS caminhos:
    ///  - **USB / fila do Windows** (nome de impressora instalada) -> spooler (winspool).
    ///  - **Bluetooth / serial** (ex: "COM6", "COM6 (Bluetooth)") -> porta COM (SerialPort).
    /// BLINDADO: qualquer falha de hardware (cabo solto, sem papel, porta ocupada, fora de
    /// alcance) vira (false, msg) — NUNCA lanca excecao, para nao derrubar o caixa.
    /// </summary>
    public static (bool ok, string msg) EnviarRaw(string alvo, byte[] dados)
    {
        if (string.IsNullOrWhiteSpace(alvo))
            return (false, "Nenhuma impressora configurada.");

        // TETO DE TEMPO: impressao NUNCA pode congelar o caixa. Se a impressora travar (cabo
        // solto, spooler ocupado, firmware embolado), a operacao e abandonada apos 15s e a
        // venda segue — o cupom pode ser reimpresso pelo Historico. Proteger o caixa > o papel.
        try
        {
            var tarefa = System.Threading.Tasks.Task.Run(() => EnviarRawInterno(alvo, dados));
            if (tarefa.Wait(TimeSpan.FromSeconds(15)))
                return tarefa.Result;
            return (false, "A impressora nao respondeu a tempo (15s). A venda foi salva — reimprima pelo Historico.");
        }
        catch (Exception ex)
        {
            return (false, "Falha ao imprimir: " + ex.Message);
        }
    }

    private static (bool ok, string msg) EnviarRawInterno(string alvo, byte[] dados)
    {
        var porta = ExtrairPortaCom(alvo);
        return porta is not null ? EnviarSerial(porta, dados) : EnviarSpooler(alvo, dados);
    }

    /// <summary>Extrai "COMx" do inicio do alvo (ex: "COM6 (Bluetooth)"); null se nao for COM.</summary>
    private static string? ExtrairPortaCom(string alvo)
    {
        var m = Regex.Match(alvo.Trim(), @"^(COM\d+)\b", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.ToUpperInvariant() : null;
    }

    /// <summary>Impressao via porta serial/COM (impressoras Bluetooth SPP ou USB-serial).</summary>
    private static (bool ok, string msg) EnviarSerial(string porta, byte[] dados)
    {
        SerialPort? sp = null;
        try
        {
            sp = new SerialPort(porta, 9600, Parity.None, 8, StopBits.One)
            {
                Handshake = Handshake.None,
                WriteTimeout = 5000,
                DtrEnable = true,
                RtsEnable = true
            };
            sp.Open();
            sp.Write(dados, 0, dados.Length);
            sp.BaseStream.Flush();
            System.Threading.Thread.Sleep(400);  // deixa a bobina puxar antes de fechar
            return (true, $"Impresso via {porta}.");
        }
        catch (Exception ex)
        {
            return (false, $"Falha na porta {porta} (impressora ligada/no alcance?): {ex.Message}");
        }
        finally
        {
            try { if (sp?.IsOpen == true) sp.Close(); sp?.Dispose(); } catch { }
        }
    }

    /// <summary>Impressao via fila do Windows (winspool) — impressoras instaladas (USB/driver).</summary>
    private static (bool ok, string msg) EnviarSpooler(string impressora, byte[] dados)
    {
        // ANTI-EMBOLA: antes de mandar o cupom, limpa jobs presos e tira a fila do offline.
        // Sem isto, um job travado de um cupom anterior segura/mistura os proximos tickets.
        PrepararFila(impressora);

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

    /// <summary>
    /// Deixa a fila pronta para receber um cupom SEM embolar: (1) tira do modo offline se
    /// estiver, (2) remove jobs em ERRO/retidos que estejam segurando a fila. Best-effort:
    /// qualquer falha aqui e ignorada (a impressao segue). So para filas do Windows (USB).
    /// </summary>
    private static void PrepararFila(string impressora)
    {
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                $"SELECT * FROM Win32_Printer WHERE Name = '{impressora.Replace("'", "''")}'");
            foreach (System.Management.ManagementObject p in searcher.Get())
            {
                // (1) online: se estiver offline, religa (SetPrinter via metodo WMI da classe).
                try
                {
                    if (p["WorkOffline"] is bool off && off)
                    {
                        p["WorkOffline"] = false;
                        p.Put();
                    }
                }
                catch { /* alguns drivers nao deixam setar; segue */ }

                // (2) remove jobs presos em erro (Retained/Error) que travam a fila.
                try
                {
                    using var jobs = new System.Management.ManagementObjectSearcher(
                        $"SELECT * FROM Win32_PrintJob WHERE Name LIKE '{impressora.Replace("'", "''")},%'");
                    foreach (System.Management.ManagementObject j in jobs.Get())
                    {
                        var status = j["JobStatus"]?.ToString() ?? "";
                        if (status.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
                            status.Contains("Retained", StringComparison.OrdinalIgnoreCase) ||
                            status.Contains("Blocked", StringComparison.OrdinalIgnoreCase))
                            try { j.Delete(); } catch { }
                    }
                }
                catch { /* sem jobs ou WMI indisponivel */ }
            }
        }
        catch { /* WMI totalmente indisponivel: segue sem preparar */ }

        // (3) SALVAGUARDA: um job travado JA EM IMPRESSAO ("Error | Printing") nao sai com
        // j.Delete() — fica agarrado no spooler e bloqueia TODOS os proximos cupons (foi o
        // que derrubou a fila no teste de campo). Se ainda houver job preso apos o passo (2),
        // reinicia o spooler do usuario, que e o unico jeito de solta-lo. Best-effort e seguro.
        if (HaJobPresoAposLimpeza(impressora))
            ReiniciarSpoolerBestEffort();
    }

    /// <summary>Sobrou algum job em erro/impressao presa apos a tentativa de limpeza?</summary>
    private static bool HaJobPresoAposLimpeza(string impressora)
    {
        try
        {
            using var jobs = new System.Management.ManagementObjectSearcher(
                $"SELECT JobStatus FROM Win32_PrintJob WHERE Name LIKE '{impressora.Replace("'", "''")},%'");
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

    /// <summary>
    /// Reinicia o servico Spooler para soltar job agarrado (Error|Printing). Roda no contexto
    /// do usuario; se nao tiver permissao, apenas ignora (o cupom seguinte tentara mesmo assim).
    /// NUNCA lanca — proteger o caixa e prioridade sobre limpar a fila.
    /// </summary>
    private static void ReiniciarSpoolerBestEffort()
    {
        try
        {
            using var sc = new System.ServiceProcess.ServiceController("Spooler");
            if (sc.Status == System.ServiceProcess.ServiceControllerStatus.Running)
            {
                sc.Stop();
                sc.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(5));
            }
            sc.Start();
            sc.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Running, TimeSpan.FromSeconds(5));
        }
        catch { /* sem permissao / servico ocupado: segue — nao derruba o caixa */ }
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

    /// <summary>
    /// Cupom de teste (status OK) para a tela de configuracao de impressora. Ao contrario de um
    /// cupom de venda, o teste CONFIRMA o resultado: apos enviar, olha a fila do Windows por
    /// alguns instantes. Se o job travar em erro (aparelho desligado/sem papel), devolve ok=false
    /// com a causa — em vez do falso "enviado!". Assim o operador so ve verde se saiu papel.
    /// </summary>
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
        var (ok, msg) = EnviarRaw(impressora, MontarBytes(linhas));
        if (!ok) return (false, msg);

        // porta COM/serial nao tem fila do Windows para inspecionar: o envio ja e a confirmacao.
        if (impressora.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
            return (true, msg);

        // fila USB/Windows: confirma que o job REALMENTE saiu (nao ficou preso em erro).
        return ConfirmarSaidaDoJob(impressora);
    }

    /// <summary>
    /// Espera ate ~4s a fila do Windows drenar o teste. Se algum job travar em Error/Offline,
    /// devolve ok=false com a causa (impressora provavelmente desligada). Se a fila esvaziar,
    /// ok=true (saiu papel). Best-effort: se WMI falhar, assume enviado.
    /// </summary>
    private static (bool ok, string msg) ConfirmarSaidaDoJob(string impressora)
    {
        try
        {
            var filtro = $"SELECT JobStatus FROM Win32_PrintJob WHERE Name LIKE '{impressora.Replace("'", "''")},%'";
            for (int tentativa = 0; tentativa < 8; tentativa++)   // ~4s (8 x 500ms)
            {
                System.Threading.Thread.Sleep(500);
                bool temJob = false;
                using var jobs = new System.Management.ManagementObjectSearcher(filtro);
                foreach (var j in jobs.Get())
                {
                    temJob = true;
                    var s = j["JobStatus"]?.ToString() ?? "";
                    if (s.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
                        s.Contains("Offline", StringComparison.OrdinalIgnoreCase) ||
                        s.Contains("Retained", StringComparison.OrdinalIgnoreCase) ||
                        s.Contains("Blocked", StringComparison.OrdinalIgnoreCase) ||
                        s.Contains("PaperOut", StringComparison.OrdinalIgnoreCase))
                        return (false, "o cupom travou na fila — impressora desligada, sem papel ou desconectada.");
                }
                if (!temJob) return (true, "");   // fila drenou: saiu papel.
            }
            // ainda ha job pendente (sem erro claro) apos 4s: pode estar lenta.
            return (false, "o cupom ainda esta na fila (impressora nao respondeu). Verifique cabo/energia e papel.");
        }
        catch { return (true, ""); }   // WMI indisponivel: nao da pra confirmar, assume enviado.
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
        b.AddRange(ESC_LINE_SPACING);   // aperta o avanco vertical (economiza bobina)

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
                    b.AddRange(Bytes(linha.Texto.Trim()));
                    b.Add(LF);
                    b.AddRange(ESC_NORMAL_FONT);
                    b.AddRange(ESC_ALIGN_LEFT);
                    break;

                case EstiloLinha.Expandida:
                    // vales: SO altura dupla (fonte menor/estreita que 2x2, ainda grande).
                    b.AddRange(ESC_DOUBLE_HEIGHT);
                    b.AddRange(Bytes(linha.Texto));
                    b.Add(LF);
                    b.AddRange(ESC_NORMAL_FONT);
                    break;

                default: // Normal
                    b.AddRange(Bytes(linha.Texto));
                    b.Add(LF);
                    break;
            }
        }

        // Sem LFs extras: o proprio GS V ja avanca ate a lamina. Cortar logo apos a ultima
        // linha economiza bobina (evita os "2 dedos" de papel em branco no fim do cupom).
        b.AddRange(GS_CUT_PAPER);
        return b.ToArray();
    }
}
