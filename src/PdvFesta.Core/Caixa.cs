namespace PdvFesta.Core;

/// <summary>Lancada quando o valor recebido em dinheiro e menor que o total.</summary>
public sealed class PagamentoInsuficienteException : Exception
{
    public PagamentoInsuficienteException(int total, int recebido)
        : base($"Pagamento insuficiente: total {total}c, recebido {recebido}c.") { }
}

/// <summary>Resultado do fechamento de caixa (dashboard do tesoureiro).</summary>
public sealed class ResumoCaixa
{
    public int TotalDinheiroCentavos { get; init; }
    public int TotalPixCentavos { get; init; }
    public int TotalCartaoCentavos { get; init; }
    public int FaturamentoBrutoCentavos { get; init; }
    public int QuantidadeVendas { get; init; }
}

/// <summary>Regras puras de caixa: troco e consolidacao. Sem estado, faceis de testar.</summary>
public static class Caixa
{
    /// <summary>
    /// Troco em centavos. Se recebido &lt; total, lanca PagamentoInsuficienteException.
    /// </summary>
    public static int CalcularTroco(int totalCentavos, int recebidoCentavos)
    {
        if (recebidoCentavos < totalCentavos)
            throw new PagamentoInsuficienteException(totalCentavos, recebidoCentavos);
        return recebidoCentavos - totalCentavos;
    }

    /// <summary>Consolida uma lista de vendas em um resumo por forma de pagamento.</summary>
    public static ResumoCaixa Consolidar(IEnumerable<Venda> vendas)
    {
        var lista = vendas as ICollection<Venda> ?? vendas.ToList();

        int dinheiro = lista.Where(v => v.Forma == FormaPagamento.Dinheiro).Sum(v => v.TotalCentavos);
        int pix = lista.Where(v => v.Forma == FormaPagamento.Pix).Sum(v => v.TotalCentavos);
        int cartao = lista.Where(v => v.Forma == FormaPagamento.Cartao).Sum(v => v.TotalCentavos);

        return new ResumoCaixa
        {
            TotalDinheiroCentavos = dinheiro,
            TotalPixCentavos = pix,
            TotalCartaoCentavos = cartao,
            FaturamentoBrutoCentavos = dinheiro + pix + cartao,
            QuantidadeVendas = lista.Count
        };
    }
}
