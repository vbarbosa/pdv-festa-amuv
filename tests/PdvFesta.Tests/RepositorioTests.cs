using PdvFesta.Core;
using Xunit;

namespace PdvFesta.Tests;

/// <summary>
/// TDD - persistencia SQLite. Cada teste usa um arquivo .db temporario proprio.
/// Valida: salvar/recuperar vendas, consolidacao lida do banco, e que WAL esta ativo.
/// </summary>
public class RepositorioTests : IDisposable
{
    private readonly string _dbPath;

    public RepositorioTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"pdvtest_{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        foreach (var ext in new[] { "", "-wal", "-shm" })
        {
            var f = _dbPath + ext;
            if (File.Exists(f)) { try { File.Delete(f); } catch { } }
        }
    }

    [Fact]
    public void Inicializar_CriaBancoEArquivo()
    {
        using var repo = new Repositorio(_dbPath);
        repo.Inicializar();
        Assert.True(File.Exists(_dbPath));
    }

    [Fact]
    public void WalHabilitado_AposInicializar()
    {
        using var repo = new Repositorio(_dbPath);
        repo.Inicializar();
        Assert.Equal("wal", repo.ModoJournal().ToLowerInvariant());
    }

    [Fact]
    public void SalvarVenda_GeraIdERecupera()
    {
        using var repo = new Repositorio(_dbPath);
        repo.Inicializar();

        var venda = new Venda
        {
            TotalCentavos = 1800,
            Forma = FormaPagamento.Dinheiro,
            RecebidoCentavos = 2000,
            TrocoCentavos = 200,
            Operador = "Tio Ze",
            Itens =
            {
                new ItemVenda { ProdutoId = "quentao", Nome = "Quentao", PrecoUnitarioCentavos = 700, Quantidade = 1 },
                new ItemVenda { ProdutoId = "pipoca", Nome = "Pipoca", PrecoUnitarioCentavos = 500, Quantidade = 1 },
                new ItemVenda { ProdutoId = "bingo", Nome = "Bingo", PrecoUnitarioCentavos = 600, Quantidade = 1 },
            }
        };

        long id = repo.SalvarVenda(venda);
        Assert.True(id > 0);

        var todas = repo.ListarVendas();
        Assert.Single(todas);
        Assert.Equal(1800, todas[0].TotalCentavos);
        Assert.Equal(3, todas[0].Itens.Count);
        Assert.Equal("Tio Ze", todas[0].Operador);
    }

    [Fact]
    public void ListarVendas_MultiplasVendas_ConsolidacaoBate()
    {
        using var repo = new Repositorio(_dbPath);
        repo.Inicializar();

        repo.SalvarVenda(new Venda { TotalCentavos = 1000, Forma = FormaPagamento.Dinheiro });
        repo.SalvarVenda(new Venda { TotalCentavos = 700, Forma = FormaPagamento.Pix });
        repo.SalvarVenda(new Venda { TotalCentavos = 2000, Forma = FormaPagamento.Cartao });

        var resumo = Caixa.Consolidar(repo.ListarVendas());
        Assert.Equal(1000, resumo.TotalDinheiroCentavos);
        Assert.Equal(700, resumo.TotalPixCentavos);
        Assert.Equal(2000, resumo.TotalCartaoCentavos);
        Assert.Equal(3700, resumo.FaturamentoBrutoCentavos);
        Assert.Equal(3, resumo.QuantidadeVendas);
    }

    [Fact]
    public void Persistencia_ReabrindoBanco_MantemDados()
    {
        long id;
        using (var repo = new Repositorio(_dbPath))
        {
            repo.Inicializar();
            id = repo.SalvarVenda(new Venda { TotalCentavos = 500, Forma = FormaPagamento.Pix });
        }
        // Reabre num novo objeto (simula reiniciar o programa apos queda de energia)
        using (var repo2 = new Repositorio(_dbPath))
        {
            repo2.Inicializar();
            var todas = repo2.ListarVendas();
            Assert.Single(todas);
            Assert.Equal(id, todas[0].Id);
            Assert.Equal(500, todas[0].TotalCentavos);
        }
    }

    [Fact]
    public void ProdutosCatalogo_SalvaERecupera()
    {
        using var repo = new Repositorio(_dbPath);
        repo.Inicializar();

        var catalogo = new List<Produto>
        {
            new() { Id = "quentao", Nome = "Quentao", PrecoCentavos = 700, Atalho = 1 },
            new() { Id = "pipoca", Nome = "Pipoca", PrecoCentavos = 500, Atalho = 2 },
        };
        repo.SalvarCatalogo(catalogo);

        var lido = repo.ListarProdutos();
        Assert.Equal(2, lido.Count);
        Assert.Contains(lido, p => p.Id == "quentao" && p.PrecoCentavos == 700 && p.Atalho == 1);
    }
}
