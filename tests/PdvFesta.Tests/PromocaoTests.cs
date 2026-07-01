using PdvFesta.Core;
using Xunit;

namespace PdvFesta.Tests;

/// <summary>TDD - motor de precos (combos/promocoes): deteccao, horario e persistencia.</summary>
public class PromocaoTests : IDisposable
{
    private readonly string _db;
    public PromocaoTests() => _db = Path.Combine(Path.GetTempPath(), $"promo_{Guid.NewGuid():N}.db");
    public void Dispose()
    {
        foreach (var ext in new[] { "", "-wal", "-shm" })
            if (File.Exists(_db + ext)) { try { File.Delete(_db + ext); } catch { } }
    }

    private static ItemVenda Item(string id, int qtd) =>
        new() { ProdutoId = id, Nome = id, PrecoUnitarioCentavos = 500, Quantidade = qtd };

    private static Promocao Combo(string desc, int desc_cent, params (string id, int q)[] itens) => new()
    {
        Descricao = desc, Tipo = TipoPromocao.Combo, ValorDescontoCentavos = desc_cent,
        Ativo = true, Itens = itens.Select(t => new PromocaoItem { ProdutoId = t.id, Quantidade = t.q }).ToList()
    };

    [Fact]
    public void Combo_AplicaDescontoQuandoTemOsItens()
    {
        var carrinho = new List<ItemVenda> { Item("cachorro", 1), Item("refri", 1) };
        var promo = Combo("COMBO Hotdog+Refri", 200, ("cachorro", 1), ("refri", 1));
        var d = PricingEngine.Calcular(carrinho, new[] { promo }, DateTime.Now);
        Assert.Single(d);
        Assert.Equal(200, d[0].ValorCentavos);
    }

    [Fact]
    public void Combo_AplicaPorConjuntoCompleto()
    {
        var carrinho = new List<ItemVenda> { Item("cachorro", 2), Item("refri", 3) };
        var promo = Combo("COMBO", 200, ("cachorro", 1), ("refri", 1));
        var d = PricingEngine.Calcular(carrinho, new[] { promo }, DateTime.Now);
        Assert.Equal(400, d[0].ValorCentavos);   // min(2,3) = 2 conjuntos
    }

    [Fact]
    public void Combo_NaoAplicaSeFaltaItem()
    {
        var carrinho = new List<ItemVenda> { Item("cachorro", 1) };  // sem refri
        var promo = Combo("COMBO", 200, ("cachorro", 1), ("refri", 1));
        Assert.Empty(PricingEngine.Calcular(carrinho, new[] { promo }, DateTime.Now));
    }

    [Fact]
    public void Promocao_ForaDoHorario_NaoAplica()
    {
        var carrinho = new List<ItemVenda> { Item("quentao", 1) };
        var promo = new Promocao
        {
            Descricao = "Quentao ate 20h", Tipo = TipoPromocao.PrecoEspecial, ValorDescontoCentavos = 100, Ativo = true,
            HoraInicio = TimeSpan.FromHours(8), HoraFim = TimeSpan.FromHours(20),
            Itens = { new PromocaoItem { ProdutoId = "quentao", Quantidade = 1 } }
        };
        var as21h = DateTime.Today.AddHours(21);
        var as19h = DateTime.Today.AddHours(19);
        Assert.Empty(PricingEngine.Calcular(carrinho, new[] { promo }, as21h));   // fora
        Assert.Single(PricingEngine.Calcular(carrinho, new[] { promo }, as19h));  // dentro
    }

    [Fact]
    public void PromocaoInativa_NaoAplica()
    {
        var carrinho = new List<ItemVenda> { Item("cachorro", 1), Item("refri", 1) };
        var promo = Combo("COMBO", 200, ("cachorro", 1), ("refri", 1));
        promo.Ativo = false;
        Assert.Empty(PricingEngine.Calcular(carrinho, new[] { promo }, DateTime.Now));
    }

    [Fact]
    public void Carrinho_ComDesconto_ReduzTotalEAssaLinhaNaVenda()
    {
        var carrinho = new Carrinho();
        carrinho.Adicionar(new Produto { Id = "cachorro", Nome = "Cachorro-Quente", PrecoCentavos = 1000 });
        carrinho.Adicionar(new Produto { Id = "refri", Nome = "Refri", PrecoCentavos = 600 });
        carrinho.AplicarDescontos(new[] { Combo("COMBO Hotdog+Refri", 200, ("cachorro", 1), ("refri", 1)) }, DateTime.Now);

        Assert.Equal(1600, carrinho.SubtotalCentavos);
        Assert.Equal(200, carrinho.DescontoTotalCentavos);
        Assert.Equal(1400, carrinho.TotalCentavos);

        var venda = carrinho.FecharVenda(FormaPagamento.Pix, 0, "");
        Assert.Equal(1400, venda.TotalCentavos);
        Assert.Contains(venda.Itens, i => string.IsNullOrEmpty(i.ProdutoId) && i.SubtotalCentavos == -200);
    }

    [Fact]
    public void Repo_SalvaEListaPromocaoComItens_ESoftDelete()
    {
        using var repo = new Repositorio(_db);
        repo.Inicializar();
        var id = repo.SalvarPromocao(Combo("COMBO Refri+Bolo", 200, ("refri", 1), ("bolo", 1)));
        Assert.True(id > 0);

        var lida = repo.ListarPromocoes().Single();
        Assert.Equal("COMBO Refri+Bolo", lida.Descricao);
        Assert.Equal(2, lida.Itens.Count);

        repo.InativarPromocao(id);
        Assert.Empty(repo.ListarPromocoes());                    // ativas
        Assert.Single(repo.ListarPromocoes(incluirInativas: true)); // continua no banco
    }

    [Fact]
    public void Seed_PromocoesSeVazio_NaoDuplica()
    {
        using var repo = new Repositorio(_db);
        repo.Inicializar();
        repo.SemearPromocoesSeVazio(new[] { Combo("COMBO Refri+Bolo", 200, ("refri", 1), ("bolochoc", 1)) });
        Assert.Single(repo.ListarPromocoes());
        repo.SemearPromocoesSeVazio(new[] { Combo("OUTRO", 100, ("x", 1)) });  // ja tem -> ignora
        Assert.Single(repo.ListarPromocoes());
    }

    [Fact]
    public void PromocaoSeed_Ate20h_ConverteERespeitaHorario()
    {
        var seed = new PromocaoSeed
        {
            Descricao = "COMBO 2 Refri (ate 20h)", Tipo = "Combo", DescontoCentavos = 200,
            HoraInicio = "00:00", HoraFim = "20:00",
            Itens = { new PromocaoItem { ProdutoId = "refri", Quantidade = 2 } }
        };
        var p = seed.ParaPromocao();
        Assert.Equal(TipoPromocao.Combo, p.Tipo);
        Assert.True(p.ValidaAgora(DateTime.Today.AddHours(19)));   // antes das 20h: vale
        Assert.False(p.ValidaAgora(DateTime.Today.AddHours(21)));  // depois: nao vale
    }

    [Fact]
    public void ContarItens_IgnoraLinhaDeDesconto()
    {
        var venda = new Venda
        {
            Itens =
            {
                new ItemVenda { ProdutoId = "cachorro", Nome = "Cachorro", PrecoUnitarioCentavos = 1000, Quantidade = 1 },
                new ItemVenda { ProdutoId = "", Nome = "COMBO", PrecoUnitarioCentavos = -200, Quantidade = 1 },
            }
        };
        var itens = Caixa.ContarItens(new[] { venda });
        Assert.Single(itens);                        // so o cachorro; a linha de desconto nao conta
        Assert.Equal("Cachorro", itens[0].Nome);
    }
}
