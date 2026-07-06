using System.Globalization;
using System.Text;

namespace PdvFesta.Core;

/// <summary>
/// Gera CSVs (Excel) a partir dos dados do turno. PURO: recebe dados, devolve string — sem
/// I/O, para ser 100% testavel e reutilizavel (Fechamento, Historico, menu Arquivo).
/// Usa o separador de lista da cultura (';' pt-BR, ',' en-US) e escapa campos corretamente.
/// </summary>
public static class ExportadorCsv
{
    private static string Sep(CultureInfo c)
    {
        var s = c.TextInfo.ListSeparator;
        return string.IsNullOrEmpty(s) ? ";" : s;
    }

    private static string Campo(string s, string sep) =>
        (s.Contains(sep) || s.Contains('"') || s.Contains('\n') || s.Contains('\r'))
            ? "\"" + s.Replace("\"", "\"\"") + "\""
            : s;

    private static string Reais(int centavos, CultureInfo c) => (centavos / 100m).ToString("0.00", c);

    private static string Linha(string sep, params string[] cols) =>
        string.Join(sep, cols.Select(x => Campo(x, sep)));

    /// <summary>
    /// CSV de RESUMO do turno: totais por forma de pagamento, gaveta, faturamento e a lista
    /// de itens vendidos (agrupados). Mesmo conteudo do CSV historico do Fechamento.
    /// </summary>
    public static string Resumo(ResumoTurno resumo, IEnumerable<ItemVendido> itens, CultureInfo? cultura = null)
    {
        var c = cultura ?? CultureInfo.CurrentCulture;
        var sep = Sep(c);
        var v = resumo.Vendas;
        var sb = new StringBuilder();

        sb.AppendLine(Linha(sep, "RESUMO DO CAIXA", "Valor (R$)"));
        sb.AppendLine(Linha(sep, "Turno", resumo.Turno.Id.ToString(c)));
        sb.AppendLine(Linha(sep, "Abertura", resumo.Turno.Abertura.ToString("dd/MM/yyyy HH:mm", c)));
        sb.AppendLine(Linha(sep, "Fundo inicial", Reais(resumo.FundoCentavos, c)));
        sb.AppendLine(Linha(sep, "Dinheiro", Reais(v.TotalDinheiroCentavos, c)));
        sb.AppendLine(Linha(sep, "Pix", Reais(v.TotalPixCentavos, c)));
        sb.AppendLine(Linha(sep, "Debito", Reais(v.TotalDebitoCentavos, c)));
        sb.AppendLine(Linha(sep, "Credito", Reais(v.TotalCreditoCentavos, c)));
        sb.AppendLine(Linha(sep, "Suprimentos", Reais(resumo.SuprimentosCentavos, c)));
        sb.AppendLine(Linha(sep, "Sangrias", Reais(resumo.SangriasCentavos, c)));
        sb.AppendLine(Linha(sep, "TOTAL EM GAVETA", Reais(resumo.TotalGavetaCentavos, c)));
        sb.AppendLine(Linha(sep, "Faturamento bruto", Reais(v.FaturamentoBrutoCentavos, c)));
        sb.AppendLine(Linha(sep, "N de vendas", v.QuantidadeVendas.ToString(c)));
        sb.AppendLine(Linha(sep, "Cortesias (brindes)", v.QuantidadeCortesias.ToString(c)));
        sb.AppendLine(Linha(sep, "Valor em cortesias", Reais(v.TotalCortesiaCentavos, c)));
        sb.AppendLine();
        sb.AppendLine(Linha(sep, "Produto", "Quantidade", "Total (R$)"));
        foreach (var it in itens)
            sb.AppendLine(Linha(sep, it.Nome, it.Quantidade.ToString(c), Reais(it.TotalCentavos, c)));

        return sb.ToString();
    }

    /// <summary>
    /// CSV DETALHADO: uma linha por venda (numero, hora, itens, total, pagamento, status,
    /// nro de impressoes). Vendas canceladas aparecem marcadas (auditoria).
    /// </summary>
    public static string Vendas(IEnumerable<Venda> vendas, CultureInfo? cultura = null)
    {
        var c = cultura ?? CultureInfo.CurrentCulture;
        var sep = Sep(c);
        var sb = new StringBuilder();

        sb.AppendLine(Linha(sep, "Venda", "Data/Hora", "Itens", "Total (R$)", "Pagamento", "Status", "Impressoes", "Observacao"));
        foreach (var venda in vendas)
        {
            // "2x Refri | 1x Bolo" — itens fisicos (ignora linha de desconto de combo)
            var itensTxt = string.Join(" | ", venda.Itens
                .Where(i => !(string.IsNullOrEmpty(i.ProdutoId) && i.SubtotalCentavos < 0))
                .Select(i => $"{i.Quantidade}x {i.Nome}"));

            sb.AppendLine(Linha(sep,
                venda.Id.ToString(c),
                venda.DataHora.ToString("dd/MM/yyyy HH:mm", c),
                itensTxt,
                Reais(venda.TotalCentavos, c),
                NomePagamento(venda.Forma),
                venda.Cancelada ? "CANCELADA" : "OK",
                venda.Impressoes.ToString(c),
                venda.Observacao));   // ex: "CORTESIA: Joao Cantor"
        }
        return sb.ToString();
    }

    private static string NomePagamento(FormaPagamento f) => f switch
    {
        FormaPagamento.Dinheiro => "Dinheiro",
        FormaPagamento.Pix => "Pix",
        FormaPagamento.CartaoDebito => "Debito",
        FormaPagamento.CartaoCredito => "Credito",
        FormaPagamento.Cortesia => "Cortesia",
        _ => "Cartao"
    };
}
