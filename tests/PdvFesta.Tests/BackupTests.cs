using PdvFesta.Core;
using Xunit;

namespace PdvFesta.Tests;

/// <summary>
/// TDD - dump/restore: exportar tudo (catalogo + vendas) para 1 arquivo JSON
/// e reimportar em outro PC (caso um caixa quebre no meio da festa).
/// </summary>
public class BackupTests : IDisposable
{
    private readonly string _dbA;
    private readonly string _dbB;

    public BackupTests()
    {
        _dbA = Path.Combine(Path.GetTempPath(), $"pdvA_{Guid.NewGuid():N}.db");
        _dbB = Path.Combine(Path.GetTempPath(), $"pdvB_{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        foreach (var db in new[] { _dbA, _dbB })
            foreach (var ext in new[] { "", "-wal", "-shm" })
                if (File.Exists(db + ext)) { try { File.Delete(db + ext); } catch { } }
    }

    [Fact]
    public void ExportarImportar_TransfereVendasECatalogo()
    {
        // PC A: catalogo + 2 vendas
        string json;
        using (var repoA = new Repositorio(_dbA))
        {
            repoA.Inicializar();
            repoA.SalvarCatalogo(new List<Produto>
            {
                new() { Id = "quentao", Nome = "Quentao", PrecoCentavos = 700, Atalho = 1 },
                new() { Id = "bingo", Nome = "Bingo", PrecoCentavos = 600, Atalho = 3 },
            });
            repoA.SalvarVenda(new Venda { TotalCentavos = 700, Forma = FormaPagamento.Dinheiro });
            repoA.SalvarVenda(new Venda { TotalCentavos = 600, Forma = FormaPagamento.Pix });

            json = BackupService.Exportar(repoA);
        }

        Assert.False(string.IsNullOrWhiteSpace(json));

        // PC B: importa o dump
        using (var repoB = new Repositorio(_dbB))
        {
            repoB.Inicializar();
            BackupService.Importar(repoB, json);

            var produtos = repoB.ListarProdutos();
            var vendas = repoB.ListarVendas();
            Assert.Equal(2, produtos.Count);
            Assert.Equal(2, vendas.Count);

            var resumo = Caixa.Consolidar(vendas);
            Assert.Equal(1300, resumo.FaturamentoBrutoCentavos);
        }
    }

    [Fact]
    public void Importar_NaoDuplica_QuandoJaTemDados()
    {
        using var repoA = new Repositorio(_dbA);
        repoA.Inicializar();
        repoA.SalvarVenda(new Venda { TotalCentavos = 500, Forma = FormaPagamento.Pix });
        var json = BackupService.Exportar(repoA);

        // importar sobre o mesmo banco deve SUBSTITUIR, nao somar (evita contar 2x)
        BackupService.Importar(repoA, json);
        Assert.Single(repoA.ListarVendas());
    }
}
