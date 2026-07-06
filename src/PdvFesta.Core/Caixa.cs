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
    /// <summary>Valor "perdido" em cortesias (brindes) — NAO entra no faturamento nem na gaveta.</summary>
    public int TotalCortesiaCentavos { get; init; }
    /// <summary>Quantas cortesias foram dadas no periodo.</summary>
    public int QuantidadeCortesias { get; init; }
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

/// <summary>
/// Conferencia de gaveta (batimento) na troca de operador / fechamento. Compara o dinheiro
/// CONTADO fisicamente com o ESPERADO (Total em Gaveta), apontando sobra ou falta.
/// </summary>
public sealed class ResultadoBatimento
{
    /// <summary>Dinheiro que o sistema esperava na gaveta (Total em Gaveta do turno).</summary>
    public required int EsperadoCentavos { get; init; }
    /// <summary>Dinheiro que o operador realmente contou na gaveta.</summary>
    public required int ContadoCentavos { get; init; }

    /// <summary>Contado - Esperado. Positivo = SOBRA; negativo = FALTA; zero = bate certinho.</summary>
    public int DiferencaCentavos => ContadoCentavos - EsperadoCentavos;
    public bool Bate => DiferencaCentavos == 0;
    public bool Sobra => DiferencaCentavos > 0;
    public bool Falta => DiferencaCentavos < 0;
}

/// <summary>Regras puras de caixa: troco, consolidacao e turno. Sem estado, faceis de testar.</summary>
public static class Caixa
{
    /// <summary>
    /// Bate a gaveta: compara o CONTADO com o ESPERADO (Total em Gaveta) do turno. Puro.
    /// </summary>
    public static ResultadoBatimento Bater(ResumoTurno resumo, int contadoCentavos) => new()
    {
        EsperadoCentavos = resumo.TotalGavetaCentavos,
        ContadoCentavos = contadoCentavos
    };

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

        // CORTESIA: entra numa conta SEPARADA. Nao soma no faturamento nem na gaveta (o item foi
        // dado de graca), mas o gestor ve quanto "custou" em brindes.
        var cortesias = lista.Where(v => v.Forma == FormaPagamento.Cortesia).ToList();
        int cortesia = cortesias.Sum(v => v.TotalCentavos);

        // vendas PAGAS (exclui cortesia da contagem de vendas com receita).
        int qtdPagas = lista.Count(v => v.Forma != FormaPagamento.Cortesia);

        return new ResumoCaixa
        {
            TotalDinheiroCentavos = dinheiro,
            TotalPixCentavos = pix,
            TotalCartaoCentavos = cartaoTotal,
            TotalDebitoCentavos = debito,
            TotalCreditoCentavos = credito,
            FaturamentoBrutoCentavos = dinheiro + pix + cartaoTotal,   // cortesia NAO entra
            QuantidadeVendas = qtdPagas,
            TotalCortesiaCentavos = cortesia,
            QuantidadeCortesias = cortesias.Count
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
            // ignora as linhas de DESCONTO (marcador: ProdutoId vazio + subtotal negativo)
            .Where(i => !(string.IsNullOrEmpty(i.ProdutoId) && i.SubtotalCentavos < 0))
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

    /// <summary>
    /// PRECOS PRATICADOS por item: agrupa por (nome, preco unitario). Se um item foi vendido a
    /// precos DIFERENTES no periodo (ex: Chopp mudou de R$10 para R$12 durante a festa),
    /// aparecem linhas separadas — o contador ve claramente cada preco e quanto rendeu.
    /// </summary>
    public static List<PrecoPraticado> PrecosPraticados(IEnumerable<Venda> vendas)
    {
        return vendas
            .Where(v => !v.Cancelada)
            .SelectMany(v => v.Itens)
            .Where(i => !string.IsNullOrEmpty(i.ProdutoId))   // ignora linhas de desconto
            .GroupBy(i => (i.Nome, i.PrecoUnitarioCentavos))
            .Select(g => new PrecoPraticado
            {
                Nome = g.Key.Nome,
                PrecoUnitarioCentavos = g.Key.PrecoUnitarioCentavos,
                Quantidade = g.Sum(i => i.Quantidade),
                TotalCentavos = g.Sum(i => i.SubtotalCentavos)
            })
            .OrderBy(p => p.Nome).ThenBy(p => p.PrecoUnitarioCentavos)
            .ToList();
    }
}

/// <summary>Um preco praticado de um item no periodo (nome + preco unitario + quanto rendeu).</summary>
public sealed class PrecoPraticado
{
    public string Nome { get; init; } = "";
    public int PrecoUnitarioCentavos { get; init; }
    public int Quantidade { get; init; }
    public int TotalCentavos { get; init; }
}
