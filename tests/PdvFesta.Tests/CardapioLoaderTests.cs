using PdvFesta.Core;
using Xunit;

namespace PdvFesta.Tests;

/// <summary>TDD - carregar o cardapio do JSON e semear o banco na 1a execucao.</summary>
public class CardapioLoaderTests : IDisposable
{
    private readonly string _db;
    private readonly string _json;

    public CardapioLoaderTests()
    {
        _db = Path.Combine(Path.GetTempPath(), $"pdvcard_{Guid.NewGuid():N}.db");
        _json = Path.Combine(Path.GetTempPath(), $"card_{Guid.NewGuid():N}.json");
        File.WriteAllText(_json, """
        {
          "tituloCupom": "FESTA TESTE",
          "produtos": [
            { "id": "quentao", "nome": "Quentao", "precoCentavos": 700, "categoria": "Bebidas", "atalho": 1 },
            { "id": "pipoca",  "nome": "Pipoca",  "precoCentavos": 500, "categoria": "Comidas", "atalho": 2 },
            { "id": "combo1",  "nome": "Combo",   "precoCentavos": 1000, "categoria": "Promocoes", "atalho": 0,
              "composicao": [ { "produtoId": "quentao", "quantidade": 2 } ] }
          ]
        }
        """);
    }

    public void Dispose()
    {
        foreach (var f in new[] { _db, _db + "-wal", _db + "-shm", _json })
            if (File.Exists(f)) { try { File.Delete(f); } catch { } }
    }

    [Fact]
    public void CarregarDoArquivo_LeTituloEProdutos()
    {
        var cardapio = CardapioLoader.CarregarDeArquivo(_json);
        Assert.Equal("FESTA TESTE", cardapio.TituloCupom);
        Assert.Equal(3, cardapio.Produtos.Count);
        Assert.Contains(cardapio.Produtos, p => p.Id == "quentao" && p.PrecoCentavos == 700);
        Assert.Contains(cardapio.Produtos, p => p.EhCombo);
    }

    [Fact]
    public void SeedSeVazio_PopulaBancoNaPrimeiraVez()
    {
        using var repo = new Repositorio(_db);
        repo.Inicializar();
        Assert.Empty(repo.ListarProdutos());

        var cardapio = CardapioLoader.CarregarDeArquivo(_json);
        CardapioLoader.SemearSeVazio(repo, cardapio);

        Assert.Equal(3, repo.ListarProdutos().Count);
    }

    [Fact]
    public void SeedSeVazio_NaoSobrescreveSeJaTemDados()
    {
        using var repo = new Repositorio(_db);
        repo.Inicializar();
        repo.SalvarCatalogo(new List<Produto> { new() { Id = "unico", Nome = "Unico", PrecoCentavos = 100 } });

        var cardapio = CardapioLoader.CarregarDeArquivo(_json);
        CardapioLoader.SemearSeVazio(repo, cardapio);

        // manteve o catalogo existente, nao semeou por cima
        Assert.Single(repo.ListarProdutos());
        Assert.Equal("unico", repo.ListarProdutos()[0].Id);
    }
}
