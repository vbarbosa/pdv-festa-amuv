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
    /// <summary>Todas as bandeiras de cartao somadas (generico + debito + credito).</summary>
    public int TotalCartaoCentavos { get; init; }
    public int TotalDebitoCentavos { get; init; }
    public int TotalCreditoCentavos { get; init; }
    public int FaturamentoBrutoCentavos { get; init; }
    public int QuantidadeVendas { get; init; }
}

/// <summary>Quantidade e valor vendidos de UM produto (auditoria da Leitura Z / barracas).</summary>
public sealed class ItemVendido
{
    public string Nome { get; init; } = "";
    public int Quantidade { get; init; }
    public int TotalCentavos { get; init; }
}

/// <summary>
/// Espelho financeiro de um turno: fundo inicial + entradas por forma + movimentos,
/// culminando no "Total em Gaveta" (o numero que o operador confere na gaveta de dinheiro).
/// </summary>
public sealed class ResumoTurno
{
    public required Turno Turno { get; init; }
    public required ResumoCaixa Vendas { get; init; }
    public int SuprimentosCentavos { get; init; }
    public int SangriasCentavos { get; init; }

    /// <summary>Fundo de caixa (troco inicial) informado na abertura.</summary>
    public int FundoCentavos => Turno.FundoCentavos;

    /// <summary>
    /// O numero magico da conferencia:
    /// Fundo inicial + Vendas em Dinheiro + Suprimentos - Sangrias.
    /// (Pix e cartao NAO entram: nao ha dinheiro fisico na gaveta por eles.)
    /// </summary>
    public int TotalGavetaCentavos =>
        FundoCentavos + Vendas.TotalDinheiroCentavos + SuprimentosCentavos - SangriasCentavos;
}

/// <summary>Regras puras de caixa: troco, consolidacao e turno. Sem estado, faceis de testar.</summary>
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

    /// <summary>
    /// Consolida uma lista de vendas em um resumo por forma de pagamento.
    /// Vendas CANCELADAS (estornadas) sao ignoradas — assim a gaveta bate centavo a centavo.
    /// </summary>
    public static ResumoCaixa Consolidar(IEnumerable<Venda> vendas)
    {
        var lista = (vendas as ICollection<Venda> ?? vendas.ToList())
            .Where(v => !v.Cancelada).ToList();

        int dinheiro = lista.Where(v => v.Forma == FormaPagamento.Dinheiro).Sum(v => v.TotalCentavos);
        int pix = lista.Where(v => v.Forma == FormaPagamento.Pix).Sum(v => v.TotalCentavos);
        int debito = lista.Where(v => v.Forma == FormaPagamento.CartaoDebito).Sum(v => v.TotalCentavos);
        int credito = lista.Where(v => v.Forma == FormaPagamento.CartaoCredito).Sum(v => v.TotalCentavos);
        int cartaoGenerico = lista.Where(v => v.Forma == FormaPagamento.Cartao).Sum(v => v.TotalCentavos);
        int cartaoTotal = cartaoGenerico + debito + credito;

        return new ResumoCaixa
        {
            TotalDinheiroCentavos = dinheiro,
            TotalPixCentavos = pix,
            TotalCartaoCentavos = cartaoTotal,
            TotalDebitoCentavos = debito,
            TotalCreditoCentavos = credito,
            FaturamentoBrutoCentavos = dinheiro + pix + cartaoTotal,
            QuantidadeVendas = lista.Count
        };
    }

    /// <summary>
    /// Monta o espelho financeiro de um turno a partir das vendas e movimentos DELE.
    /// A filtragem por turno e responsabilidade do chamador (ou usa <see cref="ConsolidarTurno(Turno, IEnumerable{Venda}, IEnumerable{MovimentoCaixa})"/>).
    /// </summary>
    public static ResumoTurno ConsolidarTurno(
        Turno turno, IEnumerable<Venda> vendasDoTurno, IEnumerable<MovimentoCaixa> movimentosDoTurno)
    {
        var movs = movimentosDoTurno as ICollection<MovimentoCaixa> ?? movimentosDoTurno.ToList();
        int suprimentos = movs.Where(m => m.Tipo == TipoMovimento.Suprimento).Sum(m => m.ValorCentavos);
        int sangrias = movs.Where(m => m.Tipo == TipoMovimento.Sangria).Sum(m => m.ValorCentavos);

        return new ResumoTurno
        {
            Turno = turno,
            Vendas = Consolidar(vendasDoTurno),
            SuprimentosCentavos = suprimentos,
            SangriasCentavos = sangrias
        };
    }

    /// <summary>
    /// Agrupa os itens de varias vendas por NOME, somando quantidade e valor.
    /// Usado no relatorio Z para auditar as barracas (ex: "Quentao: 154 - R$ 1.078,00").
    /// Ordenado do mais vendido (valor) para o menos.
    /// </summary>
    public static List<ItemVendido> ContarItens(IEnumerable<Venda> vendas)
    {
        return vendas
            .Where(v => !v.Cancelada)          // itens de vendas canceladas nao contam
            .SelectMany(v => v.Itens)
            .GroupBy(i => i.Nome)
            .Select(g => new ItemVendido
            {
                Nome = g.Key,
                Quantidade = g.Sum(i => i.Quantidade),
                TotalCentavos = g.Sum(i => i.SubtotalCentavos)
            })
            .OrderByDescending(i => i.TotalCentavos)
            .ThenBy(i => i.Nome)
            .ToList();
    }
}
