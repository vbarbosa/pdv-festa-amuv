using PdvFesta.Core;
using Xunit;

namespace PdvFesta.Tests;

/// <summary>
/// TDD - turno de caixa (abertura/fechamento), sangria/suprimento, "Total em Gaveta",
/// soft-delete de produto e agregacao de itens para a Leitura Z.
/// </summary>
public class TurnoCaixaTests : IDisposable
{
    private readonly string _db;
    public TurnoCaixaTests() => _db = Path.Combine(Path.GetTempPath(), $"turno_{Guid.NewGuid():N}.db");
    public void Dispose()
    {
        foreach (var ext in new[] { "", "-wal", "-shm" })
            if (File.Exists(_db + ext)) { try { File.Delete(_db + ext); } catch { } }
    }

    private Repositorio NovoRepo()
    {
        var r = new Repositorio(_db);
        r.Inicializar();
        return r;
    }

    [Fact]
    public void AbrirCaixa_FicaAbertoAteFechar()
    {
        using var repo = NovoRepo();
        Assert.Null(repo.CaixaAberto());

        var t = repo.AbrirCaixa(fundoCentavos: 10000, operador: "Tia Ana");
        Assert.True(t.Id > 0);

        var aberto = repo.CaixaAberto();
        Assert.NotNull(aberto);
        Assert.Equal(t.Id, aberto!.Id);
        Assert.Equal(10000, aberto.FundoCentavos);

        repo.FecharCaixa(t.Id);
        Assert.Null(repo.CaixaAberto());
    }

    [Fact]
    public void TotalGaveta_FundoMaisDinheiroMaisSuprimentoMenosSangria()
    {
        using var repo = NovoRepo();
        var t = repo.AbrirCaixa(fundoCentavos: 5000, operador: "Ze"); // fundo R$50

        // vendas do turno
        repo.SalvarVenda(new Venda { TotalCentavos = 2000, Forma = FormaPagamento.Dinheiro, CaixaId = t.Id });
        repo.SalvarVenda(new Venda { TotalCentavos = 3000, Forma = FormaPagamento.Pix, CaixaId = t.Id }); // nao entra na gaveta
        repo.SalvarVenda(new Venda { TotalCentavos = 1000, Forma = FormaPagamento.Dinheiro, CaixaId = t.Id });

        // movimentos
        repo.RegistrarMovimento(new MovimentoCaixa { CaixaId = t.Id, Tipo = TipoMovimento.Suprimento, ValorCentavos = 1500 });
        repo.RegistrarMovimento(new MovimentoCaixa { CaixaId = t.Id, Tipo = TipoMovimento.Sangria, ValorCentavos = 500 });

        var resumo = Caixa.ConsolidarTurno(t, repo.ListarVendasPorCaixa(t.Id), repo.ListarMovimentos(t.Id));

        // 5000 (fundo) + 3000 (dinheiro) + 1500 (supr) - 500 (sangria) = 9000
        Assert.Equal(9000, resumo.TotalGavetaCentavos);
        Assert.Equal(3000, resumo.Vendas.TotalDinheiroCentavos);
        Assert.Equal(3000, resumo.Vendas.TotalPixCentavos);
    }

    [Fact]
    public void Consolidar_SeparaDebitoECredito()
    {
        var vendas = new List<Venda>
        {
            new() { TotalCentavos = 1000, Forma = FormaPagamento.CartaoDebito },
            new() { TotalCentavos = 2000, Forma = FormaPagamento.CartaoCredito },
            new() { TotalCentavos = 500,  Forma = FormaPagamento.Cartao }, // legado
        };
        var r = Caixa.Consolidar(vendas);
        Assert.Equal(1000, r.TotalDebitoCentavos);
        Assert.Equal(2000, r.TotalCreditoCentavos);
        Assert.Equal(3500, r.TotalCartaoCentavos); // debito + credito + legado
    }

    [Fact]
    public void ContarItens_AgrupaPorNome_OrdenaPorValor()
    {
        var vendas = new List<Venda>
        {
            new() { Itens = { new ItemVenda { Nome = "Quentao", PrecoUnitarioCentavos = 700, Quantidade = 2 } } },
            new() { Itens = { new ItemVenda { Nome = "Quentao", PrecoUnitarioCentavos = 700, Quantidade = 1 },
                              new ItemVenda { Nome = "Pipoca",  PrecoUnitarioCentavos = 500, Quantidade = 1 } } },
        };
        var itens = Caixa.ContarItens(vendas);
        var quentao = itens.First(i => i.Nome == "Quentao");
        Assert.Equal(3, quentao.Quantidade);
        Assert.Equal(2100, quentao.TotalCentavos);
        // Quentao (2100) vem antes de Pipoca (500)
        Assert.Equal("Quentao", itens[0].Nome);
    }

    [Fact]
    public void SoftDelete_InativaSemApagar_EPreservaHistorico()
    {
        using var repo = NovoRepo();
        repo.SalvarProduto(new Produto { Id = "quentao", Nome = "Quentao", PrecoCentavos = 700, Ativo = true });
        repo.SalvarVenda(new Venda { TotalCentavos = 700, Forma = FormaPagamento.Dinheiro,
            Itens = { new ItemVenda { ProdutoId = "quentao", Nome = "Quentao", PrecoUnitarioCentavos = 700, Quantidade = 1 } } });

        Assert.True(repo.ProdutoTemVendas("quentao"));

        repo.InativarProduto("quentao");
        var prod = repo.ListarProdutos().First(p => p.Id == "quentao");
        Assert.False(prod.Ativo);                    // inativado...
        Assert.True(repo.ProdutoTemVendas("quentao")); // ...mas o historico continua intacto
    }

    [Fact]
    public void SalvarProduto_Upsert_AtualizaPreco()
    {
        using var repo = NovoRepo();
        repo.SalvarProduto(new Produto { Id = "cerveja", Nome = "Cerveja", PrecoCentavos = 500 });
        repo.SalvarProduto(new Produto { Id = "cerveja", Nome = "Cerveja", PrecoCentavos = 600 }); // promocao
        var prod = repo.ListarProdutos().Single(p => p.Id == "cerveja");
        Assert.Equal(600, prod.PrecoCentavos);
    }

    [Fact]
    public void MigracaoCaixaId_BancoAntigo_NaoQuebra()
    {
        // simula um banco "antigo": cria vendas sem turno e reabre (migracao roda no Inicializar)
        using (var repo = NovoRepo())
            repo.SalvarVenda(new Venda { TotalCentavos = 500, Forma = FormaPagamento.Pix });
        using (var repo2 = NovoRepo())
        {
            var vendas = repo2.ListarVendas();
            Assert.Single(vendas);
            Assert.Null(vendas[0].CaixaId); // venda legada sem turno
        }
    }
}
