namespace PdvFesta.Core;

/// <summary>
/// Log de arquivo simples, thread-safe e a prova de falhas (logar NUNCA derruba o app).
/// Grava em &lt;pasta-do-banco&gt;\logs\pdv-AAAAMMDD.log (ex: %AppData%\FestaJuninaPDV\logs).
/// Serve tanto para auditoria/diagnostico no dia da festa quanto para debug do dev.
/// </summary>
public static class Log
{
    private static readonly object _lock = new();
    private static string _dir = AppContext.BaseDirectory;

    /// <summary>Aponta o log para a pasta de dados (a mesma do banco).</summary>
    public static void Inicializar(string pastaBase)
    {
        if (!string.IsNullOrWhiteSpace(pastaBase)) _dir = pastaBase;
        try { Directory.CreateDirectory(PastaLogs); } catch { /* ignora */ }
    }

    public static string PastaLogs => Path.Combine(_dir, "logs");
    public static string ArquivoDeHoje => Path.Combine(PastaLogs, $"pdv-{DateTime.Now:yyyyMMdd}.log");

    public static void Info(string mensagem) => Escrever("INFO", mensagem);
    public static void Aviso(string mensagem) => Escrever("WARN", mensagem);
    public static void Trace(string mensagem) => Escrever("TRACE", mensagem);

    public static void Erro(string mensagem, Exception? ex = null) =>
        Escrever("ERRO", ex is null ? mensagem : $"{mensagem}\n{ex}");

    private static void Escrever(string nivel, string mensagem)
    {
        try
        {
            lock (_lock)
            {
                Directory.CreateDirectory(PastaLogs);
                File.AppendAllText(ArquivoDeHoje,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {nivel,-5} {mensagem}{Environment.NewLine}");
                LimparAntigos();
            }
        }
        catch { /* logar nunca pode lancar */ }
    }

    /// <summary>Mantem so os ultimos ~14 dias de log para nao encher o disco.</summary>
    private static void LimparAntigos()
    {
        try
        {
            var limite = DateTime.Now.AddDays(-14);
            foreach (var f in Directory.EnumerateFiles(PastaLogs, "pdv-*.log"))
                if (File.GetLastWriteTime(f) < limite) File.Delete(f);
        }
        catch { /* best-effort */ }
    }
}
