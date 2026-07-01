using PdvFesta.Core;
using Xunit;

namespace PdvFesta.Tests;

public class PricingEngineEdgeTests
{
    private static ItemVenda Item(string id, int qtd, int preco = 500) =>
        new() { ProdutoId = id, Nome = id, PrecoUnitarioCentavos = preco, Quantidade = qtd };

    private static Promocao Combo(int desconto, params (string id, int qtd)[] itens) => new()
    {
        Descricao = "COMBO", Tipo = TipoPromocao.Combo, ValorDescontoCentavos = desconto, Ativo = true,
        Itens = itens.Select(i => new PromocaoItem { ProdutoId = i.id, Quantidade = i.qtd }).ToList()
    };

    [Fact]
    public void MultiplosConjuntos_DescontoMultiplicado()
    {
        var itens = new[] { Item("refri", 4), Item("bolo", 4) };
        var promo = Combo(200, ("refri", 1), ("bolo", 1));
        var d = PricingEngine.Calcular(itens, new[] { promo }, DateTime.Now);
        Assert.Single(d);
        Assert.Equal(800, d[0].ValorCentavos);
    }

    [Fact]
    public void ConjuntosLimitadosPeloItemMaisEscasso()
    {
        var itens = new[] { Item("refri", 5), Item("bolo", 2) };
        var d = PricingEngine.Calcular(itens, new[] { Combo(200, ("refri", 1), ("bolo", 1)) }, DateTime.Now);
        Assert.Equal(400, d[0].ValorCentavos);
    }

    [Fact]
    public void QuantidadeExigidaMaiorQueUm()
    {
        var itens = new[] { Item("refri", 5) };
        var d = PricingEngine.Calcular(itens, new[] { Combo(200, ("refri", 2)) }, DateTime.Now);
        Assert.Equal(400, d[0].ValorCentavos);
    }

    [Fact]
    public void DescontoZeroOuNegativo_NaoAplica()
    {
        var itens = new[] { Item("refri", 2) };
        Assert.Empty(PricingEngine.Calcular(itens, new[] { Combo(0, ("refri", 1)) }, DateTime.Now));
        Assert.Empty(PricingEngine.Calcular(itens, new[] { Combo(-100, ("refri", 1)) }, DateTime.Now));
    }

    [Fact]
    public void PromocaoSemItens_NaoAplica()
    {
        var d = PricingEngine.Calcular(new[] { Item("refri", 2) }, new[] { Combo(200) }, DateTime.Now);
        Assert.Empty(d);
    }

    [Fact]
    public void LinhasDeDesconto_NaoContamComoProduto()
    {
        var itens = new List<ItemVenda>
        {
            Item("refri", 1), Item("bolo", 1),
            new() { ProdutoId = "", Nome = "DESCONTO", PrecoUnitarioCentavos = -200, Quantidade = 1 }
        };
        var d = PricingEngine.Calcular(itens, new[] { Combo(200, ("refri", 1), ("bolo", 1)) }, DateTime.Now);
        Assert.Equal(200, d[0].ValorCentavos);
    }

    [Fact]
    public void VariasPromocoes_SomamIndependente()
    {
        var itens = new[] { Item("refri", 2), Item("bolo", 1), Item("pinhao", 1) };
        var promos = new[]
        {
            Combo(200, ("refri", 2)),
            Combo(150, ("bolo", 1), ("pinhao", 1))
        };
        var d = PricingEngine.Calcular(itens, promos, DateTime.Now);
        Assert.Equal(2, d.Count);
        Assert.Equal(350, d.Sum(x => x.ValorCentavos));
    }

    [Theory]
    [InlineData(10, 8, 20, true)]
    [InlineData(21, 8, 20, false)]
    [InlineData(8, 8, 20, true)]
    [InlineData(20, 8, 20, true)]
    public void ValidaAgora_JanelaNormal(int hora, int ini, int fim, bool esperado)
    {
        var p = new Promocao { Ativo = true, HoraInicio = TimeSpan.FromHours(ini), HoraFim = TimeSpan.FromHours(fim), Itens = { new() } };
        Assert.Equal(esperado, p.ValidaAgora(DateTime.Today.AddHours(hora)));
    }

    [Theory]
    [InlineData(23, 22, 2, true)]
    [InlineData(1, 22, 2, true)]
    [InlineData(12, 22, 2, false)]
    public void ValidaAgora_JanelaCruzaMeiaNoite(int hora, int ini, int fim, bool esperado)
    {
        var p = new Promocao { Ativo = true, HoraInicio = TimeSpan.FromHours(ini), HoraFim = TimeSpan.FromHours(fim), Itens = { new() } };
        Assert.Equal(esperado, p.ValidaAgora(DateTime.Today.AddHours(hora)));
    }

    [Fact]
    public void ValidaAgora_SoAteX_ValeDesdeMeiaNoite()
    {
        var p = new Promocao { Ativo = true, HoraFim = TimeSpan.FromHours(20), Itens = { new() } };
        Assert.True(p.ValidaAgora(DateTime.Today.AddHours(3)));
        Assert.False(p.ValidaAgora(DateTime.Today.AddHours(21)));
    }

    [Fact]
    public void ValidaAgora_SemHorario_SempreValidaSeAtiva()
    {
        var p = new Promocao { Ativo = true, Itens = { new() } };
        Assert.True(p.ValidaAgora(DateTime.Today.AddHours(4)));
        p.Ativo = false;
        Assert.False(p.ValidaAgora(DateTime.Today.AddHours(4)));
    }
}
