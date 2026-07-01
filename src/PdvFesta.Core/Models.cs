namespace PdvFesta.Core;

/// <summary>Forma de pagamento registrada na venda (fluxo de caixa).</summary>
public enum FormaPagamento
{
    Dinheiro = 0,
    Pix = 1,
    Cartao = 2
}

/// <summary>
/// Produto do catalogo. Pode ser um item simples (Quentao) ou um combo.
/// Precos sempre em centavos (int) para evitar erros de ponto flutuante em dinheiro.
/// </summary>
public sealed class Produto
{
    public string Id { get; set; } = "";
    public string Nome { get; set; } = "";
    /// <summary>Preco unitario em CENTAVOS (ex: R$ 7,00 => 700).</summary>
    public int PrecoCentavos { get; set; }
    public string Categoria { get; set; } = "Geral";
    /// <summary>Tecla de atalho 1..9 (0 = sem atalho).</summary>
    public int Atalho { get; set; }
    public bool Ativo { get; set; } = true;

    /// <summary>
    /// Se for combo, lista os itens que o compoem (ids + quantidade).
    /// Combos tem preco proprio (PrecoCentavos), a composicao e apenas informativa/estoque.
    /// </summary>
    public List<ComboItem> Composicao { get; set; } = new();

    public bool EhCombo => Composicao.Count > 0;
}

/// <summary>Item que compoe um combo.</summary>
public sealed class ComboItem
{
    public string ProdutoId { get; set; } = "";
    public int Quantidade { get; set; } = 1;
}

/// <summary>Linha do carrinho: um produto + quantidade. Snapshot do preco no momento da venda.</summary>
public sealed class ItemVenda
{
    public string ProdutoId { get; set; } = "";
    public string Nome { get; set; } = "";
    public int PrecoUnitarioCentavos { get; set; }
    public int Quantidade { get; set; } = 1;

    public int SubtotalCentavos => PrecoUnitarioCentavos * Quantidade;
}

/// <summary>Venda finalizada e persistida.</summary>
public sealed class Venda
{
    public long Id { get; set; }
    public DateTime DataHora { get; set; } = DateTime.Now;
    public List<ItemVenda> Itens { get; set; } = new();
    public int TotalCentavos { get; set; }
    public FormaPagamento Forma { get; set; }
    /// <summary>Valor recebido (para dinheiro); 0 nas demais formas.</summary>
    public int RecebidoCentavos { get; set; }
    public int TrocoCentavos { get; set; }
    public string Operador { get; set; } = "";
}
