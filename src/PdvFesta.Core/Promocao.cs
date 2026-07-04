namespace PdvFesta.Core;

/// <summary>Tipo de promocao.</summary>
public enum TipoPromocao
{
    /// <summary>Preco especial de UM item (desconto por unidade), geralmente por horario.</summary>
    PrecoEspecial = 0,
    /// <summary>Combo: um conjunto de itens da um desconto fixo por conjunto completo.</summary>
    Combo = 1
}

/// <summary>
/// Dias da semana em que a promocao vale (flags combinaveis). Nenhum/Todos = vale todo dia.
/// </summary>
[Flags]
public enum DiasSemana
{
    Nenhum  = 0,
    Domingo = 1 << 0,
    Segunda = 1 << 1,
    Terca   = 1 << 2,
    Quarta  = 1 << 3,
    Quinta  = 1 << 4,
    Sexta   = 1 << 5,
    Sabado  = 1 << 6,
    Todos   = Domingo | Segunda | Terca | Quarta | Quinta | Sexta | Sabado
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
    /// <summary>Janela de HORARIO (null/null = qualquer hora enquanto Ativa).</summary>
    public TimeSpan? HoraInicio { get; set; }
    public TimeSpan? HoraFim { get; set; }
    /// <summary>Intervalo de DATAS (null = sem limite daquele lado). Data unica: Inicio==Fim.</summary>
    public DateTime? DataInicio { get; set; }
    public DateTime? DataFim { get; set; }
    /// <summary>Dias da semana em que vale. Nenhum/Todos = todos os dias.</summary>
    public DiasSemana Dias { get; set; } = DiasSemana.Todos;
    public bool Ativo { get; set; } = true;
    public List<PromocaoItem> Itens { get; set; } = new();

    /// <summary>
    /// Valida se a promocao pode ser aplicada no instante dado. Combina 4 filtros (todos
    /// devem passar): Ativa + dentro do INTERVALO DE DATAS + no DIA DA SEMANA + na HORA.
    /// Cada filtro so restringe se estiver configurado (null/Todos = nao restringe).
    /// </summary>
    public bool ValidaAgora(DateTime agora)
    {
        if (!Ativo) return false;
        if (!DentroDoIntervaloDeDatas(agora)) return false;
        if (!NoDiaDaSemana(agora)) return false;
        return NoHorario(agora);
    }

    /// <summary>A data de 'agora' esta no intervalo [DataInicio, DataFim]? (compara so a DATA).</summary>
    private bool DentroDoIntervaloDeDatas(DateTime agora)
    {
        var hoje = agora.Date;
        if (DataInicio is DateTime ini && hoje < ini.Date) return false;
        if (DataFim is DateTime fim && hoje > fim.Date) return false;
        return true;
    }

    /// <summary>O dia da semana de 'agora' esta habilitado? (Nenhum/Todos = qualquer dia).</summary>
    private bool NoDiaDaSemana(DateTime agora)
    {
        if (Dias == DiasSemana.Nenhum || Dias == DiasSemana.Todos) return true;
        var bit = agora.DayOfWeek switch
        {
            DayOfWeek.Sunday => DiasSemana.Domingo,
            DayOfWeek.Monday => DiasSemana.Segunda,
            DayOfWeek.Tuesday => DiasSemana.Terca,
            DayOfWeek.Wednesday => DiasSemana.Quarta,
            DayOfWeek.Thursday => DiasSemana.Quinta,
            DayOfWeek.Friday => DiasSemana.Sexta,
            _ => DiasSemana.Sabado
        };
        return (Dias & bit) != 0;
    }

    /// <summary>A hora de 'agora' esta na janela [HoraInicio, HoraFim]? (null/null = qualquer hora).</summary>
    private bool NoHorario(DateTime agora)
    {
        if (HoraInicio is null && HoraFim is null) return true;
        var t = agora.TimeOfDay;
        var ini = HoraInicio ?? TimeSpan.Zero;
        var fim = HoraFim ?? new TimeSpan(23, 59, 59);
        // janela normal (08:00-20:00) ou que cruza a meia-noite (22:00-02:00)
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
