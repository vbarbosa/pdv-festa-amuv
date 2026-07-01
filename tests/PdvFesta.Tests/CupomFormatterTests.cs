using PdvFesta.Core;
using Xunit;

namespace PdvFesta.Tests;

/// <summary>
/// TDD - formatacao do cupom 58mm. Regra dura: 32 colunas por linha.
/// Nenhuma linha do corpo pode passar de 32 caracteres (senao corta/pula na termica).
/// </summary>
public class CupomFormatterTests
{
    private const int W = 32;

    [Fact]
    public void LinhaItem_NomeEsquerda_PrecoDireita_Exatamente32()
    {
        // "Quentao" ......... "R$ 7,00"
        var linha = CupomFormatter.LinhaItem("Quentao", "R$ 7,00", W);
        Assert.Equal(W, linha.Length);
        Assert.StartsWith("Quentao", linha);
        Assert.EndsWith("R$ 7,00", linha);
    }

    [Fact]
    public void NomeMuitoLongo_NaoUltrapassa32()
    {
        var linha = CupomFormatter.LinhaItem(
            "Cachorro-Quente Especial da Casa Gigante", "R$ 10,00", W);
        Assert.True(linha.Length <= W, $"linha tem {linha.Length} col");
    }

    [Fact]
    public void Wrap_QuebraTextoLongoEmVariasLinhas_NenhumaPassaDe32()
    {
        var texto = "Nao trocamos fichas por dinheiro apos as 23h59 do dia 04 de julho";
        var linhas = CupomFormatter.Wrap(texto, W);
        Assert.All(linhas, l => Assert.True(l.Length <= W));
        // recompondo, mantem todas as palavras
        var recomposto = string.Join(" ", linhas).Replace("  ", " ").Trim();
        Assert.Contains("Nao trocamos", recomposto);
        Assert.Contains("julho", recomposto);
    }

    [Fact]
    public void Centralizar_TextoFicaNoMeio_E32()
    {
        var linha = CupomFormatter.Centralizar("ARRAIA", W);
        Assert.True(linha.Length <= W);
        Assert.Contains("ARRAIA", linha);
        // deve ter espaco antes (centralizado)
        Assert.StartsWith(" ", linha);
    }

    [Fact]
    public void Divisoria_TemExatamente32Tracos()
    {
        var linha = CupomFormatter.Divisoria(W);
        Assert.Equal(new string('-', W), linha);
    }

    [Fact]
    public void MoedaCentavos_FormataReais()
    {
        Assert.Equal("R$ 7,00", CupomFormatter.Moeda(700));
        Assert.Equal("R$ 12,50", CupomFormatter.Moeda(1250));
        Assert.Equal("R$ 0,05", CupomFormatter.Moeda(5));
        Assert.Equal("R$ 100,00", CupomFormatter.Moeda(10000));
    }

    [Fact]
    public void CupomCompleto_TodasLinhasDoCorpoRespeitam32()
    {
        var venda = new Venda
        {
            Id = 42,
            TotalCentavos = 1800,
            Forma = FormaPagamento.Dinheiro,
            RecebidoCentavos = 2000,
            TrocoCentavos = 200,
            Itens =
            {
                new ItemVenda { Nome = "Quentao", PrecoUnitarioCentavos = 700, Quantidade = 1 },
                new ItemVenda { Nome = "Pipoca", PrecoUnitarioCentavos = 500, Quantidade = 1 },
                new ItemVenda { Nome = "Cartela Bingo", PrecoUnitarioCentavos = 600, Quantidade = 1 },
            }
        };
        var linhas = CupomFormatter.MontarCupom(venda, "ARRAIA DA AMUV", W);
        Assert.NotEmpty(linhas);
        Assert.All(linhas, l => Assert.True(l.Length <= W, $"'{l}' tem {l.Length} col"));
    }
}
