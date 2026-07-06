using System.Globalization;
using System.IO.Compression;
using PdvFesta.Core;
using Xunit;

namespace PdvFesta.Tests;

/// <summary>
/// Export robusto e configuravel: monta as tabelas (resumo/vendas/itens/precos) e grava nos
/// 4 formatos (CSV unico, varios CSV, XLSX, XLSX com abas). Valida conteudo e estrutura.
/// </summary>
public class ExportRobustoTests : IDisposable
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private readonly string _dir;

    public ExportRobustoTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"exp_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    private static List<Venda> Vendas() => new()
    {
        new() { Id = 1, DataHora = new DateTime(2026,7,4,16,0,0), Forma = FormaPagamento.Pix, TotalCentavos = 1200,
            Itens = { new(){ProdutoId="c",Nome="Chopp",Quantidade=1,PrecoUnitarioCentavos=1000},
                      new(){ProdutoId="q",Nome="Quentao",Quantidade=1,PrecoUnitarioCentavos=200} } },
        new() { Id = 2, DataHora = new DateTime(2026,7,4,21,0,0), Forma = FormaPagamento.Dinheiro, TotalCentavos = 1200,
            Itens = { new(){ProdutoId="c",Nome="Chopp",Quantidade=1,PrecoUnitarioCentavos=1200} } },   // preco mudou
        new() { Id = 3, DataHora = new DateTime(2026,7,4,22,0,0), Forma = FormaPagamento.Cortesia, TotalCentavos = 1000, Observacao="CORTESIA: Cantor",
            Itens = { new(){ProdutoId="c",Nome="Chopp",Quantidade=1,PrecoUnitarioCentavos=1000} } },
    };

    [Fact]
    public void Montar_GeraAsQuatroTabelas_ComConteudoCerto()
    {
        var t = RelatorioBuilder.Montar(Vendas(), "04/07 a 04/07", ModoItens.UmaLinhaPorItem, SecoesExport.Tudo, PtBr);
        Assert.Equal(4, t.Count);
        Assert.Equal("Resumo", t[0].Nome);
        // resumo: cortesia fora do faturamento (Pix 1200 + Dinheiro 1200 = 2400)
        Assert.Contains(t[0].Linhas, l => l[0] == "Faturamento bruto (R$)" && l[1] == "24,00");
        Assert.Contains(t[0].Linhas, l => l[0] == "Cortesias (qtd)" && l[1] == "1");
    }

    [Fact]
    public void ModoItens_UmaLinhaPorItem_Explode()
    {
        var t = RelatorioBuilder.Montar(Vendas(), "x", ModoItens.UmaLinhaPorItem, SecoesExport.Vendas, PtBr);
        var vendas = t[0];
        // venda 1 tem 2 itens -> 2 linhas; +1 (venda2) +1 (venda3) = 4 linhas
        Assert.Equal(4, vendas.Linhas.Count);
        Assert.Contains("Item", vendas.Cabecalho);
    }

    [Fact]
    public void ModoItens_Amontoado_UmaLinhaPorVenda()
    {
        var t = RelatorioBuilder.Montar(Vendas(), "x", ModoItens.AmontoadoNaVenda, SecoesExport.Vendas, PtBr);
        Assert.Equal(3, t[0].Linhas.Count);   // 3 vendas = 3 linhas
    }

    [Fact]
    public void Precos_MarcaMudancaDePreco()
    {
        var t = RelatorioBuilder.Montar(Vendas(), "x", ModoItens.UmaLinhaPorItem, SecoesExport.Precos, PtBr);
        var precos = t[0];
        // Chopp teve 2 precos (R$10 e R$12) -> ambas linhas marcadas "PRECO MUDOU"
        var chopp = precos.Linhas.Where(l => l[0] == "Chopp").ToList();
        Assert.Equal(2, chopp.Count);
        Assert.All(chopp, l => Assert.Equal("PRECO MUDOU", l[^1]));
    }

    [Fact]
    public void Secoes_Filtra_SoAsPedidas()
    {
        var t = RelatorioBuilder.Montar(Vendas(), "x", ModoItens.UmaLinhaPorItem, SecoesExport.Resumo | SecoesExport.Itens, PtBr);
        Assert.Equal(2, t.Count);
        Assert.DoesNotContain(t, x => x.Nome.StartsWith("Vendas"));
    }

    [Fact]
    public void Gravar_CsvUnico_UmArquivoComSecoes()
    {
        var t = RelatorioBuilder.Montar(Vendas(), "x", ModoItens.UmaLinhaPorItem, SecoesExport.Tudo, PtBr);
        var g = ExportadorArquivos.Gravar(t, FormatoExport.CsvUnico, Path.Combine(_dir, "rel"), "festa", PtBr);
        Assert.Single(g);
        var txt = File.ReadAllText(g[0]);
        Assert.Contains("RESUMO", txt);
        Assert.Contains("PREÇOS PRATICADOS", txt);
    }

    [Fact]
    public void Gravar_CsvMultiplos_UmArquivoPorSecao()
    {
        var t = RelatorioBuilder.Montar(Vendas(), "x", ModoItens.UmaLinhaPorItem, SecoesExport.Tudo, PtBr);
        var g = ExportadorArquivos.Gravar(t, FormatoExport.CsvMultiplos, _dir, "festa", PtBr);
        Assert.Equal(4, g.Count);
        Assert.All(g, p => Assert.True(File.Exists(p)));
    }

    [Fact]
    public void Gravar_Xlsx_EstruturaValida()
    {
        var t = RelatorioBuilder.Montar(Vendas(), "x", ModoItens.UmaLinhaPorItem, SecoesExport.Tudo, PtBr);
        var g = ExportadorArquivos.Gravar(t, FormatoExport.XlsxAbas, Path.Combine(_dir, "rel"), "festa", PtBr);
        Assert.Single(g);
        Assert.EndsWith(".xlsx", g[0]);

        using var zip = ZipFile.OpenRead(g[0]);
        var partes = zip.Entries.Select(e => e.FullName).ToHashSet();
        Assert.Contains("[Content_Types].xml", partes);
        Assert.Contains("xl/workbook.xml", partes);
        Assert.Contains("xl/worksheets/sheet1.xml", partes);
        Assert.Contains("xl/worksheets/sheet4.xml", partes);   // 4 abas
    }

    [Fact]
    public void Gravar_XlsxUnico_UmaAbaSo()
    {
        var t = RelatorioBuilder.Montar(Vendas(), "x", ModoItens.UmaLinhaPorItem, SecoesExport.Tudo, PtBr);
        var g = ExportadorArquivos.Gravar(t, FormatoExport.XlsxUnico, Path.Combine(_dir, "rel"), "festa", PtBr);
        using var zip = ZipFile.OpenRead(g[0]);
        var abas = zip.Entries.Count(e => e.FullName.StartsWith("xl/worksheets/"));
        Assert.Equal(1, abas);
    }
}
