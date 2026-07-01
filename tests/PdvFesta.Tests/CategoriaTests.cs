using PdvFesta.Core;
using Xunit;

namespace PdvFesta.Tests;

/// <summary>TDD - categorias: seed com ordem, listagem ativa/inativa e soft delete.</summary>
public class CategoriaTests : IDisposable
{
    private readonly string _db;
    public CategoriaTests() => _db = Path.Combine(Path.GetTempPath(), $"cat_{Guid.NewGuid():N}.db");
    public void Dispose()
    {
        foreach (var ext in new[] { "", "-wal", "-shm" })
            if (File.Exists(_db + ext)) { try { File.Delete(_db + ext); } catch { } }
    }

    private Repositorio NovoRepo() { var r = new Repositorio(_db); r.Inicializar(); return r; }

    [Fact]
    public void Semear_RespeitaAOrdemDada()
    {
        using var repo = NovoRepo();
        repo.SemearCategoriasSeVazio(new[] { "Comidas", "Doces", "Bebidas" });
        var cats = repo.ListarCategorias();
        Assert.Equal(new[] { "Comidas", "Doces", "Bebidas" }, cats.Select(c => c.Nome).ToArray());
        Assert.Equal(0, cats[0].Ordem);
        Assert.Equal(2, cats[2].Ordem);
    }

    [Fact]
    public void Semear_NaoSobrescreveSeJaTemCategorias()
    {
        using var repo = NovoRepo();
        repo.SemearCategoriasSeVazio(new[] { "A", "B" });
        repo.SemearCategoriasSeVazio(new[] { "X", "Y", "Z" }); // deve ser ignorado
        Assert.Equal(2, repo.ListarCategorias().Count);
    }

    [Fact]
    public void Inativar_SaiDaListaAtivaMasFicaNaCompleta()
    {
        using var repo = NovoRepo();
        repo.SemearCategoriasSeVazio(new[] { "Comidas", "Bebidas" });
        repo.InativarCategoria("Bebidas");

        Assert.DoesNotContain(repo.ListarCategorias(incluirInativas: false), c => c.Nome == "Bebidas");
        Assert.Contains(repo.ListarCategorias(incluirInativas: true), c => c.Nome == "Bebidas" && !c.Ativo);
    }

    [Fact]
    public void SalvarCategoria_AtualizaOrdem()
    {
        using var repo = NovoRepo();
        repo.SalvarCategoria(new Categoria { Nome = "Jogos", Ordem = 5, Ativo = true });
        repo.SalvarCategoria(new Categoria { Nome = "Jogos", Ordem = 1, Ativo = true }); // reordena
        Assert.Equal(1, repo.ListarCategorias().Single(c => c.Nome == "Jogos").Ordem);
    }

    [Fact]
    public void Seed_ViaCardapioLoader_CriaCategoriasNaOrdemDoCartaz()
    {
        using var repo = NovoRepo();
        var cardapio = new Cardapio
        {
            Categorias = { "Comidas", "Doces", "Bebidas" },
            Produtos =
            {
                new Produto { Id = "milho", Nome = "Milho", PrecoCentavos = 700, Categoria = "Comidas" },
                new Produto { Id = "agua", Nome = "Agua", PrecoCentavos = 500, Categoria = "Bebidas" },
            }
        };
        CardapioLoader.SemearSeVazio(repo, cardapio);
        Assert.Equal(new[] { "Comidas", "Doces", "Bebidas" },
            repo.ListarCategorias().Select(c => c.Nome).ToArray());
    }
}
