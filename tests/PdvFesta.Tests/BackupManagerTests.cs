using System.IO.Compression;
using PdvFesta.Core;
using Xunit;

namespace PdvFesta.Tests;

/// <summary>
/// TDD - backup/restore fisico do arquivo SQLite (disaster recovery).
/// Cobre: gerar .zip com timestamp, restaurar de .db e de .zip.
/// </summary>
public class BackupManagerTests : IDisposable
{
    private readonly string _work;

    public BackupManagerTests()
    {
        _work = Path.Combine(Path.GetTempPath(), $"bkpmgr_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_work);
    }

    public void Dispose()
    {
        if (Directory.Exists(_work)) { try { Directory.Delete(_work, true); } catch { } }
    }

    private string CriarBancoComUmaVenda()
    {
        var db = Path.Combine(_work, "caixa_festa.db");
        using var repo = new Repositorio(db);
        repo.Inicializar();
        repo.SalvarVenda(new Venda { TotalCentavos = 1234, Forma = FormaPagamento.Pix });
        return db;
    }

    [Fact]
    public void GerarZip_CriaArquivoComTimestamp()
    {
        var db = CriarBancoComUmaVenda();
        var destino = Path.Combine(_work, "backups");

        var zip = BackupManager.GerarZip(db, destino);

        Assert.True(File.Exists(zip));
        Assert.EndsWith(".zip", zip);
        Assert.Contains("backup_pdv_", Path.GetFileName(zip));

        // o zip contem o .db
        using var arch = ZipFile.OpenRead(zip);
        Assert.Contains(arch.Entries, e => e.Name.EndsWith(".db"));
    }

    [Fact]
    public void CopiarComTimestamp_GeraDbNomeado()
    {
        var db = CriarBancoComUmaVenda();
        var destino = Path.Combine(_work, "auto");

        var copia = BackupManager.CopiarComTimestamp(db, destino);

        Assert.True(File.Exists(copia));
        Assert.EndsWith(".db", copia);
        Assert.Contains("backup_pdv_", Path.GetFileName(copia));
    }

    [Fact]
    public void RestaurarDeDb_SubstituiBancoEmUso()
    {
        // banco origem com 1 venda
        var origem = CriarBancoComUmaVenda();

        // banco destino (em uso) com dados diferentes
        var destino = Path.Combine(_work, "em_uso.db");
        using (var repo = new Repositorio(destino))
        {
            repo.Inicializar();
            repo.SalvarVenda(new Venda { TotalCentavos = 999, Forma = FormaPagamento.Cartao });
            repo.SalvarVenda(new Venda { TotalCentavos = 888, Forma = FormaPagamento.Cartao });
        }

        BackupManager.RestaurarArquivo(origem, destino);

        using (var repo = new Repositorio(destino))
        {
            repo.Inicializar();
            var vendas = repo.ListarVendas();
            Assert.Single(vendas);                 // agora tem os dados da ORIGEM
            Assert.Equal(1234, vendas[0].TotalCentavos);
        }
    }

    [Fact]
    public void RestaurarDeZip_ExtraiESubstitui()
    {
        var db = CriarBancoComUmaVenda();
        var zip = BackupManager.GerarZip(db, Path.Combine(_work, "z"));

        var destino = Path.Combine(_work, "restaurado.db");
        BackupManager.RestaurarDeZip(zip, destino);

        using var repo = new Repositorio(destino);
        repo.Inicializar();
        var vendas = repo.ListarVendas();
        Assert.Single(vendas);
        Assert.Equal(1234, vendas[0].TotalCentavos);
    }

    [Fact]
    public void RestaurarArquivo_CriaBackupDeSegurancaDoAtual()
    {
        var origem = CriarBancoComUmaVenda();
        var destino = Path.Combine(_work, "atual.db");
        File.WriteAllText(destino, "conteudo antigo");

        BackupManager.RestaurarArquivo(origem, destino);

        // deve ter guardado o antigo com sufixo .bak antes de sobrescrever
        var baks = Directory.GetFiles(_work, "atual.db.*.bak");
        Assert.NotEmpty(baks);
    }
}
