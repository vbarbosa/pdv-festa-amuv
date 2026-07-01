using PdvFesta.Core;
using Xunit;

namespace PdvFesta.Tests;

public class CupomETrocoEdgeTests
{
    [Fact]
    public void Troco_Exato_Zero()
    {
        Assert.Equal(0, Caixa.CalcularTroco(1000, 1000));
    }

    [Fact]
    public void Troco_RecebidoMenor_Lanca()
    {
        Assert.Throws<PagamentoInsuficienteException>(() => Caixa.CalcularTroco(1000, 999));
    }

    [Theory]
    [InlineData(1700, 2000, 300)]
    [InlineData(1, 100, 99)]
    [InlineData(9999, 10000, 1)]
    public void Troco_ValoresVariados(int total, int recebido, int esperado)
    {
        Assert.Equal(esperado, Caixa.CalcularTroco(total, recebido));
    }

    [Theory]
    [InlineData(0, "R$ 0,00")]
    [InlineData(5, "R$ 0,05")]
    [InlineData(100, "R$ 1,00")]
    [InlineData(123456, "R$ 1234,56")]   // sem separador de milhar (formato "0.00")
    public void Moeda_FormataCorreto(int centavos, string esperado)
    {
        Assert.Equal(esperado, CupomFormatter.Moeda(centavos));
    }

    [Fact]
    public void LinhaItem_TruncaNomeLongoSemEstourarLargura()
    {
        var linha = CupomFormatter.LinhaItem("Nome Muito Muito Longo Que Estoura", "R$ 9,00", 32);
        Assert.True(linha.Length <= 32);
        Assert.EndsWith("R$ 9,00", linha);
    }

    [Fact]
    public void LinhaItem_PreencheComPontos()
    {
        var linha = CupomFormatter.LinhaItem("Pipoca", "R$ 5,00", 32);
        Assert.Equal(32, linha.Length);
        Assert.Contains("...", linha);
    }

    [Fact]
    public void Wrap_QuebraPorPalavra()
    {
        var linhas = CupomFormatter.Wrap("Cachorro Quente Especial Grande", 16);
        Assert.All(linhas, l => Assert.True(l.Length <= 16));
        Assert.True(linhas.Count >= 2);
    }

    [Fact]
    public void Wrap_PalavraMaiorQueLargura_Trunca()
    {
        var linhas = CupomFormatter.Wrap("Supercalifragilisticexpialidoce", 10);
        Assert.All(linhas, l => Assert.True(l.Length <= 10));
    }

    [Fact]
    public void Centralizar_TextoMaiorQueLargura_Trunca()
    {
        var c = CupomFormatter.Centralizar("Texto Enorme Que Nao Cabe", 10);
        Assert.True(c.Length <= 10);
    }

    [Fact]
    public void Divisorias_TamanhoCorreto()
    {
        Assert.Equal(32, CupomFormatter.Divisoria(32).Length);
        Assert.Equal(32, CupomFormatter.DivisoriaDupla(32).Length);
        Assert.Equal(32, CupomFormatter.DivisoriaPontilhada(32).Length);
        Assert.All(CupomFormatter.DivisoriaDupla(32), ch => Assert.Equal('=', ch));
    }

    [Fact]
    public void ReciboComVales_QuantidadeZero_NaoGeraVale()
    {
        var venda = new Venda
        {
            Id = 1, Forma = FormaPagamento.Dinheiro, TotalCentavos = 0,
            Itens = { new ItemVenda { ProdutoId = "x", Nome = "X", PrecoUnitarioCentavos = 100, Quantidade = 0 } }
        };
        var linhas = CupomFormatter.MontarTicket(venda, new ConfigCupom { Modo = ModoCupom.ReciboComVales });
        Assert.DoesNotContain(linhas, l => l.Texto.Contains("Vale 1 item"));
    }

    [Fact]
    public void NomeForma_CobreTodasAsFormas()
    {
        Assert.Equal("DINHEIRO", CupomFormatter.NomeForma(FormaPagamento.Dinheiro));
        Assert.Equal("PIX", CupomFormatter.NomeForma(FormaPagamento.Pix));
        Assert.Equal("CARTAO DEBITO", CupomFormatter.NomeForma(FormaPagamento.CartaoDebito));
        Assert.Equal("CARTAO CREDITO", CupomFormatter.NomeForma(FormaPagamento.CartaoCredito));
    }
}
