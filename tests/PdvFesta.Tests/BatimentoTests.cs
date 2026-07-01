using PdvFesta.Core;
using Xunit;

namespace PdvFesta.Tests;

/// <summary>
/// Batimento de gaveta (conferencia na troca de operador / fechamento): compara o dinheiro
/// CONTADO com o ESPERADO (Total em Gaveta), apontando sobra/falta.
/// </summary>
public class BatimentoTests
{
    // turno com fundo 10000c (R$100) + 2 vendas em dinheiro (700 + 600) => gaveta esperada 11300c.
    private static ResumoTurno ResumoExemplo(int fundoCentavos = 10000)
    {
        var turno = new Turno { Id = 1, FundoCentavos = fundoCentavos };
        var vendas = new List<Venda>
        {
            new() { Forma = FormaPagamento.Dinheiro, TotalCentavos = 700 },
            new() { Forma = FormaPagamento.Dinheiro, TotalCentavos = 600 },
            new() { Forma = FormaPagamento.Pix,      TotalCentavos = 5000 },   // pix NAO entra na gaveta
        };
        return Caixa.ConsolidarTurno(turno, vendas, new List<MovimentoCaixa>());
    }

    [Fact]
    public void Bater_QuandoContadoIgualEsperado_Bate()
    {
        var resumo = ResumoExemplo();                 // esperado = 10000 + 1300 = 11300
        var r = Caixa.Bater(resumo, 11300);
        Assert.True(r.Bate);
        Assert.False(r.Sobra);
        Assert.False(r.Falta);
        Assert.Equal(0, r.DiferencaCentavos);
        Assert.Equal(11300, r.EsperadoCentavos);
    }

    [Fact]
    public void Bater_QuandoContadoMaior_Sobra()
    {
        var r = Caixa.Bater(ResumoExemplo(), 11500);  // contou 200 a mais
        Assert.True(r.Sobra);
        Assert.Equal(200, r.DiferencaCentavos);
    }

    [Fact]
    public void Bater_QuandoContadoMenor_Falta()
    {
        var r = Caixa.Bater(ResumoExemplo(), 11000);  // faltou 300
        Assert.True(r.Falta);
        Assert.Equal(-300, r.DiferencaCentavos);
    }

    [Fact]
    public void Bater_IgnoraPixECartaoNoEsperado()
    {
        // o esperado (gaveta) so tem fundo + dinheiro (11300), nunca o pix de 5000.
        var r = Caixa.Bater(ResumoExemplo(), 11300);
        Assert.True(r.Bate);
    }

    [Fact]
    public void Bater_ConsideraSuprimentosESangrias()
    {
        var turno = new Turno { Id = 2, FundoCentavos = 10000 };
        var vendas = new List<Venda> { new() { Forma = FormaPagamento.Dinheiro, TotalCentavos = 1000 } };
        var movs = new List<MovimentoCaixa>
        {
            new() { Tipo = TipoMovimento.Suprimento, ValorCentavos = 5000 },   // +5000
            new() { Tipo = TipoMovimento.Sangria,    ValorCentavos = 2000 },   // -2000
        };
        var resumo = Caixa.ConsolidarTurno(turno, vendas, movs);
        // esperado = 10000 + 1000 + 5000 - 2000 = 14000
        Assert.Equal(14000, Caixa.Bater(resumo, 14000).EsperadoCentavos);
        Assert.True(Caixa.Bater(resumo, 14000).Bate);
    }
}
