using PdvFesta.Core;
using Xunit;

namespace PdvFesta.Tests;

/// <summary>
/// Exclusao PERMANENTE com trava (produto/categoria com uso nao apaga) e versionamento
/// do cardapio (exportar -> importar preserva os produtos).
/// </summary>
public class ExclusaoECardapioTests : IDisposable
{
    private readonly string _db;
    private readonly Repositorio _repo;

    public ExclusaoECardapioTests()
    {
        _db = Path.Combine(Path.GetTempPath(), $"exc_{Guid.NewGuid():N}.db");
        _repo = new Repositorio(_db);
        _repo.Inicializar();
    }

    public void Dispose()
    {
        _repo.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var ext in new[] { "", "-wal", "-shm" })
            if (File.Exists(_db + ext)) { try { File.Delete(_db + ext); } catch { } }
    }

    private Produto Prod(string id, string cat = "Comidas", int preco = 500) =>
        new() { Id = id, Nome = id, PrecoCentavos = preco, Categoria = cat, Ativo = true };

    [Fact]
    public void ExcluirProduto_SemVendas_Apaga()
    {
        _repo.SalvarProduto(Prod("pipoca"));
        _repo.ExcluirProduto("pipoca");
        Assert.False(_repo.ProdutoExiste("pipoca"));
    }

    [Fact]
    public void ExcluirProduto_ComVendas_Lanca()
    {
        _repo.SalvarProduto(Prod("milho"));
        _repo.AbrirCaixa(0, "op");
        var venda = new Venda { Forma = FormaPagamento.Dinheiro, TotalCentavos = 500 };
        venda.Itens.Add(new ItemVenda { ProdutoId = "milho", Nome = "Milho", PrecoUnitarioCentavos = 500, Quantidade = 1 });
        _repo.SalvarVenda(venda);

        // produto com vendas NAO pode ser excluido (protege a auditoria).
        Assert.Throws<InvalidOperationException>(() => _repo.ExcluirProduto("milho"));
        Assert.True(_repo.ProdutoExiste("milho"));
    }

    [Fact]
    public void ExcluirCategoria_ComProdutos_Lanca()
    {
        _repo.SalvarCategoria(new Categoria { Nome = "Bebidas", Ordem = 0, Ativo = true });
        _repo.SalvarProduto(Prod("agua", cat: "Bebidas"));
        Assert.Throws<InvalidOperationException>(() => _repo.ExcluirCategoria("Bebidas"));
    }

    [Fact]
    public void ExcluirCategoria_Vazia_Apaga()
    {
        _repo.SalvarCategoria(new Categoria { Nome = "Vazia", Ordem = 0, Ativo = true });
        _repo.ExcluirCategoria("Vazia");
        Assert.DoesNotContain(_repo.ListarCategorias(incluirInativas: true), c => c.Nome == "Vazia");
    }

    [Fact]
    public void Cardapio_ExportarEImportar_PreservaProdutos()
    {
        _repo.SalvarProduto(Prod("pipoca", preco: 500));
        _repo.SalvarProduto(Prod("refri", cat: "Bebidas", preco: 600));
        _repo.SalvarConfig("titulo_cupom", "ARRAIA");

        var pasta = Path.Combine(Path.GetTempPath(), $"card_{Guid.NewGuid():N}");
        var arquivo = CardapioLoader.ExportarParaPasta(_repo, pasta);
        Assert.True(File.Exists(arquivo));

        // apaga tudo e importa de volta
        _repo.SalvarCatalogo(new List<Produto>());
        Assert.Empty(_repo.ListarProdutos());

        int n = CardapioLoader.ImportarDeArquivo(_repo, arquivo);
        Assert.Equal(2, n);
        Assert.True(_repo.ProdutoExiste("pipoca"));
        Assert.True(_repo.ProdutoExiste("refri"));
        Assert.Equal("ARRAIA", _repo.LerConfig("titulo_cupom", ""));

        Directory.Delete(pasta, true);
    }
}
