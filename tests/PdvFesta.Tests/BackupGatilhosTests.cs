using PdvFesta.App;
using PdvFesta.Core;
using Xunit;

namespace PdvFesta.Tests;

/// <summary>
/// Gatilhos de backup automatico configuraveis: a cada N vendas e ao fechar o caixa.
/// Usa uma pasta temp como destino e verifica que os .zip aparecem no momento certo.
/// </summary>
public class BackupGatilhosTests : IDisposable
{
    private readonly string _db;
    private readonly string _pasta;
    private readonly Servico _servico;

    public BackupGatilhosTests()
    {
        _db = Path.Combine(Path.GetTempPath(), $"bkpgat_{Guid.NewGuid():N}.db");
        _pasta = Path.Combine(Path.GetTempPath(), $"bkpgatdir_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_pasta);
        _servico = new Servico(_db, "___inexistente___.json");
        _servico.ImpressaoSimulada = true;
        _servico.DefinirPastaBackup(_pasta);
        _servico.AbrirCaixa(0, "op");
    }

    public void Dispose()
    {
        _servico.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var ext in new[] { "", "-wal", "-shm" })
            if (File.Exists(_db + ext)) { try { File.Delete(_db + ext); } catch { } }
        try { Directory.Delete(_pasta, recursive: true); } catch { }
    }

    private void Vender()
    {
        _servico.Carrinho.Adicionar(new Produto { Id = "q", Nome = "Quentao", PrecoCentavos = 700, Categoria = "C" });
        _servico.FinalizarVenda(FormaPagamento.Dinheiro, 700, "op");
    }

    private int QtdBackups() => BackupManager.Listar(_pasta).Count;

    [Fact]
    public void ACadaNVendas_GeraBackupNoMultiplo()
    {
        _servico.DefinirBackupACadaVendas(3);   // backup a cada 3 vendas

        Vender(); Vender();
        Assert.Equal(0, QtdBackups());          // ainda nao (2 vendas)
        Vender();
        Assert.Equal(1, QtdBackups());          // 3a venda -> 1 backup
        Vender(); Vender(); Vender();
        Assert.Equal(2, QtdBackups());          // 6a venda -> 2o backup
    }

    [Fact]
    public void ACadaNVendas_Zero_NaoGeraNada()
    {
        _servico.DefinirBackupACadaVendas(0);   // desligado
        Vender(); Vender(); Vender(); Vender();
        Assert.Equal(0, QtdBackups());
    }

    [Fact]
    public void AoFecharCaixa_Ligado_GeraBackup()
    {
        _servico.DefinirBackupAoFecharCaixa(true);
        _servico.DefinirBackupACadaVendas(0);
        Vender();
        Assert.Equal(0, QtdBackups());
        _servico.FecharCaixa();
        Assert.Equal(1, QtdBackups());          // fechou -> 1 backup
    }

    [Fact]
    public void AoFecharCaixa_Desligado_NaoGeraBackup()
    {
        _servico.DefinirBackupAoFecharCaixa(false);
        _servico.DefinirBackupACadaVendas(0);
        Vender();
        _servico.FecharCaixa();
        Assert.Equal(0, QtdBackups());
    }

    [Fact]
    public void ConfigsPersistem_NoBanco()
    {
        _servico.DefinirBackupACadaVendas(15);
        _servico.DefinirBackupAoAbrirApp(true);
        _servico.DefinirBackupAoSairApp(true);
        _servico.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        using var s2 = new Servico(_db, "___inexistente___.json");
        Assert.Equal(15, s2.BackupACadaVendas);
        Assert.True(s2.BackupAoAbrirApp);
        Assert.True(s2.BackupAoSairApp);
    }
}
