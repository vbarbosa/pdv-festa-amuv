using PdvFesta.Core;
using Xunit;

namespace PdvFesta.Tests;

public class AutoBackupTimerTests : IDisposable
{
    private readonly string _db;
    private readonly string _destino;

    public AutoBackupTimerTests()
    {
        _db = Path.Combine(Path.GetTempPath(), $"abk_{Guid.NewGuid():N}.db");
        // banco SQLite REAL (o backup faz um checkpoint WAL, que exige um .db valido).
        using (var repo = new Repositorio(_db)) { repo.Inicializar(); }
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        _destino = Path.Combine(Path.GetTempPath(), $"abk_dest_{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        try { File.Delete(_db); } catch { }
        try { if (Directory.Exists(_destino)) Directory.Delete(_destino, true); } catch { }
    }

    [Fact]
    public void Executar_ComDestino_GeraBackupComTimestamp()
    {
        using var timer = new AutoBackupTimer(_db, () => _destino);
        timer.Executar();

        var copias = Directory.Exists(_destino) ? Directory.GetFiles(_destino) : Array.Empty<string>();
        Assert.Single(copias);
        Assert.StartsWith("backup_pdv_", Path.GetFileName(copias[0]));
        Assert.EndsWith(".db", copias[0]);
    }

    [Fact]
    public void Executar_SemDestino_NaoFazNadaEnaoLanca()
    {
        using var timer = new AutoBackupTimer(_db, () => "");   // destino vazio = backup off
        timer.Executar();   // nao deve criar nada nem lancar
        Assert.False(Directory.Exists(_destino) && Directory.GetFiles(_destino).Length > 0);
    }

    [Fact]
    public void Executar_DestinoInvalido_ReportaViaLogSemLancar()
    {
        string? msg = null;
        using var timer = new AutoBackupTimer(_db, () => "Z:\\pasta\\que\\nao\\existe\\:", m => msg = m);
        timer.Executar();   // caminho invalido -> captura no log, nunca derruba
        Assert.NotNull(msg);
        Assert.Contains("backup", msg!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Configurar_ZeroOuNegativo_NaoAgenda()
    {
        using var timer = new AutoBackupTimer(_db, () => _destino);
        timer.Configurar(0);    // desligado
        timer.Configurar(-5);   // desligado
        // sem disparo por tempo; nada e criado espontaneamente (checagem rapida).
        System.Threading.Thread.Sleep(150);
        Assert.False(Directory.Exists(_destino));
    }

    [Fact]
    public void ExecutarVariasVezes_GeraCopiasDistintas()
    {
        using var timer = new AutoBackupTimer(_db, () => _destino);
        timer.Executar();
        System.Threading.Thread.Sleep(1100);   // timestamp tem resolucao de segundos
        timer.Executar();
        Assert.Equal(2, Directory.GetFiles(_destino).Length);
    }
}
