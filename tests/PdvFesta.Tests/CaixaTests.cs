using PdvFesta.Core;
using Xunit;

namespace PdvFesta.Tests;

/// <summary>
/// TDD - TROCO e FLUXO DE CAIXA (dashboard do tesoureiro).
/// </summary>
public class CaixaTests
{
    // ---------- TROCO ----------

    [Fact]
    public void Troco_Dinheiro_ExatoQuandoRecebeIgual()
    {
        Assert.Equal(0, Caixa.CalcularTroco(totalCentavos: 1500, recebidoCentavos: 1500));
    }

    [Fact]
    public void Troco_Dinheiro_CalculaDiferenca()
    {
        // total 1800, cliente deu 2000 -> troco 200
        Assert.Equal(200, Caixa.CalcularTroco(1800, 2000));
    }

    [Fact]
    public void Troco_ValorQuebrado_ExatoEmCentavos()
    {
        // total 7,50 (750), recebeu 10,00 (1000) -> troco 2,50 (250)
        Assert.Equal(250, Caixa.CalcularTroco(750, 1000));
    }

    [Fact]
    public void Troco_RecebidoMenorQueTotal_LancaExcecao()
    {
        // pagamento insuficiente em dinheiro nao pode fechar venda
        Assert.Throws<PagamentoInsuficienteException>(() => Caixa.CalcularTroco(1000, 900));
    }

    // ---------- FLUXO DE CAIXA (consolidacao) ----------

    private static Venda V(int total, FormaPagamento forma) =>
        new() { TotalCentavos = total, Forma = forma };

    [Fact]
    public void Fechamento_CaixaVazio_TudoZero()
    {
        var r = Caixa.Consolidar(new List<Venda>());
        Assert.Equal(0, r.TotalDinheiroCentavos);
        Assert.Equal(0, r.TotalPixCentavos);
        Assert.Equal(0, r.TotalCartaoCentavos);
        Assert.Equal(0, r.FaturamentoBrutoCentavos);
        Assert.Equal(0, r.QuantidadeVendas);
    }

    [Fact]
    public void Fechamento_SeparaPorFormaDePagamento()
    {
        var vendas = new List<Venda>
        {
            V(1000, FormaPagamento.Dinheiro),
            V(500,  FormaPagamento.Dinheiro),
            V(700,  FormaPagamento.Pix),
            V(2000, FormaPagamento.Cartao),
            V(300,  FormaPagamento.Cartao),
        };
        var r = Caixa.Consolidar(vendas);
        Assert.Equal(1500, r.TotalDinheiroCentavos);  // 1000 + 500
        Assert.Equal(700,  r.TotalPixCentavos);
        Assert.Equal(2300, r.TotalCartaoCentavos);    // 2000 + 300
        Assert.Equal(4500, r.FaturamentoBrutoCentavos); // soma geral
        Assert.Equal(5, r.QuantidadeVendas);
    }

    [Fact]
    public void Fechamento_FaturamentoBruto_EhSomaDasTresFormas()
    {
        var vendas = new List<Venda>
        {
            V(1234, FormaPagamento.Dinheiro),
            V(5678, FormaPagamento.Pix),
            V(9012, FormaPagamento.Cartao),
        };
        var r = Caixa.Consolidar(vendas);
        Assert.Equal(
            r.TotalDinheiroCentavos + r.TotalPixCentavos + r.TotalCartaoCentavos,
            r.FaturamentoBrutoCentavos);
    }
}
