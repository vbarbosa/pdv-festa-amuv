using System.Globalization;
using PdvFesta.Core;
using Xunit;

namespace PdvFesta.Tests;

/// <summary>
/// Exportacao CSV (resumo do turno + lista detalhada de vendas). Cultura fixada (pt-BR) para
/// o separador e os numeros serem deterministicos no teste.
/// </summary>
public class ExportadorCsvTests
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private static ResumoTurno ResumoExemplo()
    {
        var turno = new Turno { Id = 3, Abertura = new DateTime(2026, 7, 4, 18, 0, 0), FundoCentavos = 10000 };
        var vendas = new ResumoCaixa
        {
            TotalDinheiroCentavos = 5000, TotalPixCentavos = 3000,
            TotalDebitoCentavos = 1000, TotalCreditoCentavos = 0,
            FaturamentoBrutoCentavos = 9000, QuantidadeVendas = 4
        };
        return new ResumoTurno { Turno = turno, Vendas = vendas, SuprimentosCentavos = 0, SangriasCentavos = 2000 };
    }

    [Fact]
    public void Resumo_TemTotaisEItens_ComSeparadorDaCultura()
    {
        var itens = new[]
        {
            new ItemVendido { Nome = "Quentao", Quantidade = 3, TotalCentavos = 2100 },
            new ItemVendido { Nome = "Refri", Quantidade = 2, TotalCentavos = 1200 },
        };
        var csv = ExportadorCsv.Resumo(ResumoExemplo(), itens, PtBr);

        Assert.Contains("RESUMO DO CAIXA", csv);
        Assert.Contains("TOTAL EM GAVETA;130,00", csv);   // 100 fundo + 50 dinheiro - 20 sangria
        Assert.Contains("Faturamento bruto;90,00", csv);
        Assert.Contains("Quentao;3;21,00", csv);
        Assert.Contains("Refri;2;12,00", csv);            // separador pt-BR = ';'
    }

    [Fact]
    public void Vendas_UmaLinhaPorVenda_ComItensEStatus()
    {
        var vendas = new[]
        {
            new Venda
            {
                Id = 1, DataHora = new DateTime(2026, 7, 4, 19, 30, 0), TotalCentavos = 1700,
                Forma = FormaPagamento.Dinheiro, Impressoes = 1,
                Itens = { new ItemVenda { ProdutoId = "q", Nome = "Quentao", Quantidade = 2, PrecoUnitarioCentavos = 700 } }
            },
            new Venda
            {
                Id = 2, DataHora = new DateTime(2026, 7, 4, 19, 35, 0), TotalCentavos = 600,
                Forma = FormaPagamento.Pix, Status = StatusVenda.Cancelada, Impressoes = 2,
                Itens = { new ItemVenda { ProdutoId = "r", Nome = "Refri", Quantidade = 1, PrecoUnitarioCentavos = 600 } }
            }
        };
        var csv = ExportadorCsv.Vendas(vendas, PtBr);
        var linhas = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.StartsWith("Venda;Data/Hora;Itens", linhas[0]);       // cabecalho
        Assert.Contains("2x Quentao", csv);
        Assert.Contains("Dinheiro", csv);
        Assert.Contains("CANCELADA", csv);                            // 2a venda cancelada
        Assert.Contains(";2", csv.Split('\n')[2]);                    // impressoes=2 na 2a venda
    }

    [Fact]
    public void Vendas_IgnoraLinhaDeDescontoDeComboNosItens()
    {
        var venda = new Venda
        {
            Id = 1, TotalCentavos = 800, Forma = FormaPagamento.Dinheiro,
            Itens =
            {
                new ItemVenda { ProdutoId = "r", Nome = "Refri", Quantidade = 1, PrecoUnitarioCentavos = 600 },
                new ItemVenda { ProdutoId = "b", Nome = "Bolo", Quantidade = 1, PrecoUnitarioCentavos = 400 },
                new ItemVenda { ProdutoId = "", Nome = "COMBO -R$2", Quantidade = 1, PrecoUnitarioCentavos = -200 },
            }
        };
        var csv = ExportadorCsv.Vendas(new[] { venda }, PtBr);
        Assert.Contains("1x Refri", csv);
        Assert.Contains("1x Bolo", csv);
        Assert.DoesNotContain("COMBO -R$2", csv);   // desconto nao aparece na lista de itens
    }

    [Fact]
    public void Csv_EscapaCamposComSeparadorOuAspas()
    {
        var venda = new Venda
        {
            Id = 1, TotalCentavos = 500, Forma = FormaPagamento.Dinheiro,
            Itens = { new ItemVenda { ProdutoId = "x", Nome = "Pao; \"especial\"", Quantidade = 1, PrecoUnitarioCentavos = 500 } }
        };
        var csv = ExportadorCsv.Vendas(new[] { venda }, PtBr);
        // campo com ';' e aspas -> vem entre aspas, com aspas internas duplicadas
        Assert.Contains("\"1x Pao; \"\"especial\"\"\"", csv);
    }

    [Fact]
    public void Resumo_SemItens_NaoQuebra()
    {
        var csv = ExportadorCsv.Resumo(ResumoExemplo(), Array.Empty<ItemVendido>(), PtBr);
        Assert.Contains("Produto;Quantidade;Total (R$)", csv);   // so o cabecalho da secao de itens
    }
}
