namespace PdvFesta.Core;

/// <summary>
/// Backup automatico em background: a cada N minutos copia o .db para a pasta
/// secundaria (ex: OneDrive) com timestamp. Usa System.Threading.Timer (thread do pool),
/// entao NAO trava a UI do caixa. Erros de backup sao capturados e reportados via callback,
/// nunca derrubam a aplicacao no meio de uma venda.
/// </summary>
public sealed class AutoBackupTimer : IDisposable
{
    private readonly string _dbPath;
    private readonly Func<string?> _obterPastaDestino;   // le a pasta configurada na hora
    private readonly Func<int>? _obterManter;            // quantos backups manter (0 = todos)
    private readonly Action<string>? _log;
    private Timer? _timer;
    private int _emExecucao;   // 0/1 para evitar reentrancia se um backup demorar

    public AutoBackupTimer(string dbPath, Func<string?> obterPastaDestino, Action<string>? log = null,
                           Func<int>? obterManter = null)
    {
        _dbPath = dbPath;
        _obterPastaDestino = obterPastaDestino;
        _obterManter = obterManter;
        _log = log;
    }

    /// <summary>(Re)inicia o timer com o intervalo dado. minutos &lt;= 0 desliga.</summary>
    public void Configurar(int minutos)
    {
        _timer?.Dispose();
        _timer = null;
        if (minutos <= 0) return;

        var periodo = TimeSpan.FromMinutes(minutos);
        _timer = new Timer(_ => Executar(), null, periodo, periodo);
    }

    /// <summary>Dispara um backup imediato (a mesma rotina do timer). internal p/ testes.</summary>
    internal void Executar()
    {
        // evita dois backups simultaneos
        if (Interlocked.Exchange(ref _emExecucao, 1) == 1) return;
        try
        {
            var destino = _obterPastaDestino();
            if (string.IsNullOrWhiteSpace(destino)) return;
            var arquivo = BackupManager.CopiarComTimestamp(_dbPath, destino);
            _log?.Invoke($"Backup automatico: {Path.GetFileName(arquivo)}");

            // limpa antigos para nao encher o disco/OneDrive (se configurado 'manter N').
            int manter = _obterManter?.Invoke() ?? 0;
            if (manter > 0)
            {
                int n = BackupManager.LimparAntigos(destino, manter);
                if (n > 0) _log?.Invoke($"Backup: {n} antigo(s) apagado(s) (mantendo {manter}).");
            }
        }
        catch (Exception ex)
        {
            _log?.Invoke("Falha no backup automatico: " + ex.Message);
        }
        finally
        {
            Interlocked.Exchange(ref _emExecucao, 0);
        }
    }

    public void Dispose() => _timer?.Dispose();
}
