namespace PdvFesta.Core;

/// <summary>Tipo de promocao.</summary>
public enum TipoPromocao
{
    /// <summary>Preco especial de UM item (desconto por unidade), geralmente por horario.</summary>
    PrecoEspecial = 0,
    /// <summary>Combo: um conjunto de itens da um desconto fixo por conjunto completo.</summary>
    Combo = 1
}

/// <summary>Item exigido por uma promocao (produto + quantidade minima no carrinho).</summary>
public sealed class PromocaoItem
{
    public string ProdutoId { get; set; } = "";
    public int Quantidade { get; set; } = 1;
}

/// <summary>
/// Regra de promocao/combo agendada. O desconto e aplicado por CONJUNTO COMPLETO
/// encontrado no carrinho, dentro da janela de horario (se definida) e se Ativa.
/// Soft delete via Ativo (mantem historico).
/// </summary>
public sealed class Promocao
{
    public long Id { get; set; }
    public string Descricao { get; set; } = "";
    public TipoPromocao Tipo { get; set; }
    /// <summary>Desconto em CENTAVOS por conjunto completo.</summary>
    public int ValorDescontoCentavos { get; set; }
    /// <summary>Janela de validade (null/null = sempre valida enquanto Ativa).</summary>
    public TimeSpan? HoraInicio { get; set; }
    public TimeSpan? HoraFim { get; set; }
    public bool Ativo { get; set; } = true;
    public List<PromocaoItem> Itens { get; set; } = new();

    /// <summary>Valida se a promocao pode ser aplicada no instante dado (Ativa + no horario).</summary>
    public bool ValidaAgora(DateTime agora)
    {
        if (!Ativo) return false;
        if (HoraInicio is null && HoraFim is null) return true;   // sem restricao de horario
        var t = agora.TimeOfDay;
        var ini = HoraInicio ?? TimeSpan.Zero;                    // so "ate X" -> vale desde 00:00
        var fim = HoraFim ?? new TimeSpan(23, 59, 59);            // so "a partir de X"
        // janela normal (ex: 08:00-20:00) ou que cruza a meia-noite (ex: 22:00-02:00)
        return ini <= fim ? t >= ini && t <= fim : t >= ini || t <= fim;
    }
}

/// <summary>Um desconto aplicado ao carrinho (linha verde). Valor em centavos POSITIVO.</summary>
public sealed record DescontoAplicado(string Descricao, int ValorCentavos);

/// <summary>
/// Motor de precos: varre o carrinho e devolve os descontos aplicaveis. Puro (sem estado,
/// sem banco) para ser 100% testavel. Aplica cada promocao por conjunto completo encontrado.
/// </summary>
public static class PricingEngine
{
    public static List<DescontoAplicado> Calcular(
        IReadOnlyList<ItemVenda> itens, IEnumerable<Promocao> promocoes, DateTime agora)
    {
        var descontos = new List<DescontoAplicado>();

        // quantidade total por produto no carrinho (ignora linhas de desconto: ProdutoId vazio)
        var qtd = itens
            .Where(i => !string.IsNullOrEmpty(i.ProdutoId))
            .GroupBy(i => i.ProdutoId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantidade));

        foreach (var p in promocoes)
        {
            if (!p.ValidaAgora(agora) || p.Itens.Count == 0 || p.ValorDescontoCentavos <= 0) continue;

            // quantos conjuntos completos cabem (minimo entre os itens exigidos)
            int conjuntos = int.MaxValue;
            foreach (var req in p.Itens)
            {
                int reqQtd = Math.Max(1, req.Quantidade);
                int temNoCarrinho = qtd.TryGetValue(req.ProdutoId, out var q) ? q : 0;
                conjuntos = Math.Min(conjuntos, temNoCarrinho / reqQtd);
                if (conjuntos == 0) break;
            }

            if (conjuntos > 0 && conjuntos != int.MaxValue)
                descontos.Add(new DescontoAplicado(p.Descricao, p.ValorDescontoCentavos * conjuntos));
        }

        return descontos;
    }
}
