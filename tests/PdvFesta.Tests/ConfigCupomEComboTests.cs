using PdvFesta.Core;
using Xunit;

namespace PdvFesta.Tests;

public class ConfigCupomEComboTests : IDisposable
{
    private readonly string _db;
    private readonly Repositorio _repo;

    public ConfigCupomEComboTests()
    {
        _db = Path.Combine(Path.GetTempPath(), $"cfg_{Guid.NewGuid():N}.db");
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

    [Theory]
    [InlineData(ModoCupom.Completo)]
    [InlineData(ModoCupom.FichaConsumo)]
    [InlineData(ModoCupom.ReciboComVales)]
    public void ConfigCupom_SalvaELe_TodosOsModos(ModoCupom modo)
    {
        new ConfigCupom { Modo = modo, Evento = "ARRAIA", Rodape = "Obrigado" }.Salvar(_repo);
        var lido = ConfigCupom.Ler(_repo);
        Assert.Equal(modo, lido.Modo);
        Assert.Equal("ARRAIA", lido.Evento);
    }

    [Fact]
    public void ConfigCupom_DefaultEhReciboComVales()
    {
        var lido = ConfigCupom.Ler(_repo);
        Assert.Equal(ModoCupom.ReciboComVales, lido.Modo);
    }

    [Fact]
    public void ConfigCupom_PreservaSubtituloESepararPorItem()
    {
        new ConfigCupom { Modo = ModoCupom.FichaConsumo, Subtitulo = "Caixa 02", SepararPorItem = true }.Salvar(_repo);
        var lido = ConfigCupom.Ler(_repo);
        Assert.Equal("Caixa 02", lido.Subtitulo);
        Assert.True(lido.SepararPorItem);
    }

    [Fact]
    public void ProdutoComComposicao_RoundTripNoBanco()
    {
        // exercita o ComboSerializer via o uso real (produto combo salvo -> lido do banco)
        var combo = new Produto
        {
            Id = "combo_x", Nome = "COMBO X", PrecoCentavos = 1000, Categoria = "Promocoes", Ativo = true,
            Composicao = { new ComboItem { ProdutoId = "refri", Quantidade = 2 }, new ComboItem { ProdutoId = "bolo", Quantidade = 1 } }
        };
        _repo.SalvarProduto(combo);
        var lido = _repo.ListarProdutos().First(p => p.Id == "combo_x");
        Assert.Equal(2, lido.Composicao.Count);
        Assert.Equal("refri", lido.Composicao[0].ProdutoId);
        Assert.Equal(2, lido.Composicao[0].Quantidade);
    }

    [Fact]
    public void ProdutoSemComposicao_LeListaVazia()
    {
        _repo.SalvarProduto(new Produto { Id = "simples", Nome = "Simples", PrecoCentavos = 500, Categoria = "Comidas", Ativo = true });
        var lido = _repo.ListarProdutos().First(p => p.Id == "simples");
        Assert.Empty(lido.Composicao);
    }
}
