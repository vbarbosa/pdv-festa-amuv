using System.Globalization;

namespace PdvFesta.Core;

/// <summary>Uma tabela de export: um nome (vira aba/arquivo) + cabecalho + linhas de celulas.</summary>
public sealed class TabelaExport
{
    public string Nome { get; }
    public List<string> Cabecalho { get; } = new();
    public List<string[]> Linhas { get; } = new();

    public TabelaExport(string nome, params string[] cabecalho)
    {
        Nome = nome;
        Cabecalho.AddRange(cabecalho);
    }

    public void Add(params string[] celulas) => Linhas.Add(celulas);
}

/// <summary>Como os itens da venda aparecem no export.</summary>
public enum ModoItens
{
    /// <summary>Uma linha por VENDA, com os itens amontoados numa celula ("2x A | 1x B").</summary>
    AmontoadoNaVenda,
    /// <summary>Uma linha por ITEM da venda (explode) — facil de somar/filtrar no Excel.</summary>
    UmaLinhaPorItem
}

/// <summary>Quais tabelas incluir no export (o operador escolhe).</summary>
[Flags]
public enum SecoesExport
{
    Resumo = 1,
    Vendas = 2,
    Itens = 4,          // itens agrupados (quanto vendeu de cada produto)
    Precos = 8,         // precos praticados (mudanca de preco)
    Tudo = Resumo | Vendas | Itens | Precos
}

/// <summary>
/// Monta as TABELAS de um relatorio (resumo, vendas, itens, precos) a partir dos dados —
/// PURO, sem I/O. Cada formato (CSV/XLSX) so consome essas tabelas. Assim o conteudo e o mesmo
/// independente do formato escolhido pelo operador.
/// </summary>
public static class RelatorioBuilder
{
    private static string R(int centavos, CultureInfo c) => (centavos / 100m).ToString("0.00", c);

    /// <summary>
    /// Constroi as tabelas pedidas em <paramref name="secoes"/> para as vendas dadas.
    /// 'titulo' e 'periodo' vao no cabecalho do Resumo.
    /// </summary>
    public static List<TabelaExport> Montar(
        IReadOnlyList<Venda> vendas, string periodoDescr, ModoItens modoItens,
        SecoesExport secoes, CultureInfo? cultura = null)
    {
        var c = cultura ?? CultureInfo.CurrentCulture;
        var tabelas = new List<TabelaExport>();
        var resumo = Caixa.Consolidar(vendas);

        if (secoes.HasFlag(SecoesExport.Resumo))
            tabelas.Add(MontarResumo(vendas, resumo, periodoDescr, c));

        if (secoes.HasFlag(SecoesExport.Vendas))
            tabelas.Add(MontarVendas(vendas, modoItens, c));

        if (secoes.HasFlag(SecoesExport.Itens))
            tabelas.Add(MontarItens(vendas, c));

        if (secoes.HasFlag(SecoesExport.Precos))
            tabelas.Add(MontarPrecos(vendas, c));

        return tabelas;
    }

    private static TabelaExport MontarResumo(IReadOnlyList<Venda> vendas, ResumoCaixa v, string periodo, CultureInfo c)
    {
        var t = new TabelaExport("Resumo", "Indicador", "Valor");
        t.Add("Período", periodo);
        t.Add("Nº de vendas", v.QuantidadeVendas.ToString(c));
        t.Add("Faturamento bruto (R$)", R(v.FaturamentoBrutoCentavos, c));
        t.Add("Dinheiro (R$)", R(v.TotalDinheiroCentavos, c));
        t.Add("Pix (R$)", R(v.TotalPixCentavos, c));
        t.Add("Cartão Débito (R$)", R(v.TotalDebitoCentavos, c));
        t.Add("Cartão Crédito (R$)", R(v.TotalCreditoCentavos, c));
        t.Add("Canceladas (qtd)", vendas.Count(x => x.Cancelada).ToString(c));
        t.Add("Cortesias (qtd)", v.QuantidadeCortesias.ToString(c));
        t.Add("Cortesias (R$)", R(v.TotalCortesiaCentavos, c));
        return t;
    }

    private static bool EhItemFisico(ItemVenda i) => !(string.IsNullOrEmpty(i.ProdutoId) && i.SubtotalCentavos < 0);

    private static TabelaExport MontarVendas(IReadOnlyList<Venda> vendas, ModoItens modo, CultureInfo c)
    {
        if (modo == ModoItens.UmaLinhaPorItem)
        {
            // uma linha por ITEM (explode) — ideal para analise no Excel
            var t = new TabelaExport("Vendas (por item)",
                "Venda", "Data/Hora", "Item", "Qtd", "Preço Unit (R$)", "Subtotal (R$)", "Pagamento", "Status", "Observação");
            foreach (var venda in vendas)
                foreach (var i in venda.Itens.Where(EhItemFisico))
                    t.Add(
                        venda.Id.ToString(c),
                        venda.DataHora.ToString("dd/MM/yyyy HH:mm", c),
                        i.Nome,
                        i.Quantidade.ToString(c),
                        R(i.PrecoUnitarioCentavos, c),
                        R(i.SubtotalCentavos, c),
                        CupomFormatter.NomeForma(venda.Forma),
                        venda.Cancelada ? "CANCELADA" : "OK",
                        venda.Observacao);
            return t;
        }
        else
        {
            // uma linha por VENDA (itens amontoados)
            var t = new TabelaExport("Vendas",
                "Venda", "Data/Hora", "Itens", "Total (R$)", "Pagamento", "Status", "Impressões", "Observação");
            foreach (var venda in vendas)
            {
                var itensTxt = string.Join(" | ", venda.Itens.Where(EhItemFisico).Select(i => $"{i.Quantidade}x {i.Nome}"));
                t.Add(
                    venda.Id.ToString(c),
                    venda.DataHora.ToString("dd/MM/yyyy HH:mm", c),
                    itensTxt,
                    R(venda.TotalCentavos, c),
                    CupomFormatter.NomeForma(venda.Forma),
                    venda.Cancelada ? "CANCELADA" : "OK",
                    venda.Impressoes.ToString(c),
                    venda.Observacao);
            }
            return t;
        }
    }

    private static TabelaExport MontarItens(IReadOnlyList<Venda> vendas, CultureInfo c)
    {
        var t = new TabelaExport("Itens vendidos", "Produto", "Quantidade", "Total (R$)");
        foreach (var it in Caixa.ContarItens(vendas))
            t.Add(it.Nome, it.Quantidade.ToString(c), R(it.TotalCentavos, c));
        return t;
    }

    private static TabelaExport MontarPrecos(IReadOnlyList<Venda> vendas, CultureInfo c)
    {
        var t = new TabelaExport("Preços praticados", "Produto", "Preço Unit (R$)", "Qtd Vendida", "Total (R$)", "Obs");
        foreach (var grupo in Caixa.PrecosPraticados(vendas).GroupBy(p => p.Nome).OrderBy(g => g.Key))
        {
            var linhas = grupo.OrderBy(p => p.PrecoUnitarioCentavos).ToList();
            bool mudou = linhas.Count > 1;
            foreach (var p in linhas)
                t.Add(p.Nome, R(p.PrecoUnitarioCentavos, c), p.Quantidade.ToString(c), R(p.TotalCentavos, c),
                    mudou ? "PRECO MUDOU" : "");
        }
        return t;
    }
}
