using PdvFesta.Core;
using Xunit;

namespace PdvFesta.Tests;

/// <summary>TDD - configuracao persistente (impressora padrao, titulo do cupom).</summary>
public class ConfigTests : IDisposable
{
    private readonly string _db;
    public ConfigTests() => _db = Path.Combine(Path.GetTempPath(), $"pdvcfg_{Guid.NewGuid():N}.db");
    public void Dispose()
    {
        foreach (var ext in new[] { "", "-wal", "-shm" })
            if (File.Exists(_db + ext)) { try { File.Delete(_db + ext); } catch { } }
    }

    [Fact]
    public void ConfigInexistente_RetornaPadrao()
    {
        using var repo = new Repositorio(_db);
        repo.Inicializar();
        Assert.Equal("SEM_IMPRESSORA", repo.LerConfig("impressora", "SEM_IMPRESSORA"));
    }

    [Fact]
    public void SalvarELerConfig_Persiste()
    {
        using var repo = new Repositorio(_db);
        repo.Inicializar();
        repo.SalvarConfig("impressora", "MPT-II 58mm");
        Assert.Equal("MPT-II 58mm", repo.LerConfig("impressora", ""));
    }

    [Fact]
    public void SalvarConfig_Sobrescreve()
    {
        using var repo = new Repositorio(_db);
        repo.Inicializar();
        repo.SalvarConfig("impressora", "Antiga");
        repo.SalvarConfig("impressora", "Nova");
        Assert.Equal("Nova", repo.LerConfig("impressora", ""));
    }

    [Fact]
    public void Config_PersisteAoReabrir()
    {
        using (var repo = new Repositorio(_db))
        {
            repo.Inicializar();
            repo.SalvarConfig("impressora", "MPT-II 58mm");
        }
        using (var repo2 = new Repositorio(_db))
        {
            repo2.Inicializar();
            Assert.Equal("MPT-II 58mm", repo2.LerConfig("impressora", ""));
        }
    }
}
