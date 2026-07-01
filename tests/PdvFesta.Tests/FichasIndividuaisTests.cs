using System.Linq;
using PdvFesta.Core;
using Xunit;

namespace PdvFesta.Tests;

/// <summary>
/// MODELO QUERMESSE (ModoCupom.ReciboComVales): o cupom traz o recibo gerencial E
/// desmembra cada item em N vales individuais de "1x NOME" (um por unidade fisica),
/// separados por pontilhado para rasgar e entregar nas barracas.
/// </summary>
public class FichasIndividuaisTests
{
    private static Venda VendaExemplo() => new()
    {
        Id = 42,
        Forma = FormaPagamento.Dinheiro,
        RecebidoCentavos = 5000,
        TrocoCentavos = 700,
        TotalCentavos = 4300,
        Itens =
        {
            new ItemVenda { ProdutoId = "cachorro", Nome = "Cachorro Quente", PrecoUnitarioCentavos = 1000, Quantidade = 3 },
            new ItemVenda { ProdutoId = "refri",    Nome = "Refrigerante",    PrecoUnitarioCentavos = 600,  Quantidade = 2 },
        }
    };

    private static ConfigCupom CfgVales() => new() { Modo = ModoCupom.ReciboComVales, Evento = "ARRAIA" };

    [Fact]
    public void ReciboComVales_DesmembraCadaUnidadeEmUmVale()
    {
        var linhas = CupomFormatter.MontarTicket(VendaExemplo(), CfgVales());
        var texto = string.Join("\n", linhas.Select(x => x.Texto));

        // 3 cachorros + 2 refris = 5 vales, cada um comeca com "1X" (fonte expandida).
        int vales = linhas.Count(x => x.Estilo == EstiloLinha.Expandida && x.Texto.StartsWith("1X"));
        Assert.Equal(5, vales);

        // aparecem os dois produtos em CAIXA ALTA (o nome pode quebrar em 2 linhas na
        // fonte expandida de 16 colunas — por isso conferimos as palavras, nao a frase).
        Assert.Contains("1X CACHORRO", texto);
        Assert.Contains("QUENTE", texto);
        Assert.Contains("1X REFRIGERANTE", texto);
    }

    [Fact]
    public void ReciboComVales_TemUmValePorUnidade()
    {
        var linhas = CupomFormatter.MontarTicket(VendaExemplo(), CfgVales());

        // marcador "Vale 1 item" e exclusivo de cada ficha -> 1 por unidade fisica = 5.
        int marcadores = linhas.Count(x => x.Texto.Contains("Vale 1 item"));
        Assert.Equal(5, marcadores);
    }

    [Fact]
    public void ReciboComVales_MantemOReciboGerencial()
    {
        var linhas = CupomFormatter.MontarTicket(VendaExemplo(), CfgVales());
        var texto = string.Join("\n", linhas.Select(x => x.Texto));

        // o recibo continua presente (total, troco) ANTES da divisoria dupla das fichas
        Assert.Contains("TOTAL", texto);
        Assert.Contains("Troco", texto);
        Assert.Contains(CupomFormatter.DivisoriaDupla(), texto.Split('\n'));
        Assert.Contains("FICHAS DE CONSUMO", texto);
    }

    [Fact]
    public void ReciboComVales_TemMargemAntesDoPrimeiroVale()
    {
        var linhas = CupomFormatter.MontarTicket(VendaExemplo(), CfgVales());
        // logo apos a 2a divisoria dupla (fecha o cabecalho "FICHAS DE CONSUMO") deve haver
        // uma linha em branco, para o 1o vale nao ficar colado no cabecalho.
        int idxCab = linhas.FindIndex(x => x.Texto.Contains("FICHAS DE CONSUMO"));
        Assert.True(idxCab >= 0);
        // idxCab+1 = divisoria dupla de baixo; idxCab+2 = linha em branco (a margem)
        Assert.Equal("", linhas[idxCab + 2].Texto);
    }

    [Fact]
    public void ReciboComVales_IgnoraLinhaDeDescontoDeCombo()
    {
        var venda = VendaExemplo();
        // linha de desconto de combo: ProdutoId vazio, subtotal negativo -> NAO vira vale.
        venda.Itens.Add(new ItemVenda { ProdutoId = "", Nome = "COMBO -R$2", PrecoUnitarioCentavos = -200, Quantidade = 1 });

        var linhas = CupomFormatter.MontarTicket(venda, CfgVales());
        int vales = linhas.Count(x => x.Estilo == EstiloLinha.Expandida && x.Texto.StartsWith("1X"));

        // continua 5 (o desconto nao gera vale)
        Assert.Equal(5, vales);
    }
}
