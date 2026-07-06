namespace PdvFesta.Core;

/// <summary>
/// Forma de pagamento registrada na venda (fluxo de caixa).
/// Os valores sao ESTAVEIS (gravados como int no SQLite): nunca reordenar.
/// Cartao(2) e o generico legado; Debito/Credito foram acrescentados depois
/// para o dashboard financeiro separar as bandeiras.
/// </summary>
public enum FormaPagamento
{
    Dinheiro = 0,
    Pix = 1,
    Cartao = 2,          // legado / cartao generico
    CartaoDebito = 3,
    CartaoCredito = 4,
    Cortesia = 5         // brinde/cortesia: entrega os itens SEM cobrar (nao entra na gaveta)
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

/// <summary>
/// Categoria do cardapio (aba na tela do caixa). Identidade pelo Nome; a Ordem
/// controla a posicao da aba e Ativo permite ocultar (soft delete) a categoria.
/// </summary>
public sealed class Categoria
{
    public string Nome { get; set; } = "";
    public int Ordem { get; set; }
    public bool Ativo { get; set; } = true;
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

/// <summary>Situacao de uma venda (estorno = soft delete, nunca apaga do banco).</summary>
public enum StatusVenda
{
    Concluida = 0,
    Cancelada = 1
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
    /// <summary>Turno de caixa ao qual a venda pertence (null = venda legada sem turno).</summary>
    public long? CaixaId { get; set; }
    /// <summary>Concluida ou Cancelada (estornada). Canceladas saem dos totais do caixa.</summary>
    public StatusVenda Status { get; set; } = StatusVenda.Concluida;

    /// <summary>Quantas vezes o cupom desta venda ja foi impresso (1a via + reimpressoes).</summary>
    public int Impressoes { get; set; }

    /// <summary>Observacao livre da venda. Em CORTESIA, guarda o NOME de quem recebeu.</summary>
    public string Observacao { get; set; } = "";

    public bool Cancelada => Status == StatusVenda.Cancelada;
    /// <summary>Cortesia (brinde): itens entregues sem cobranca. Nao entra na gaveta/faturamento.</summary>
    public bool EhCortesia => Forma == FormaPagamento.Cortesia;
}

/// <summary>Situacao de um turno/sessao de caixa.</summary>
public enum StatusCaixa
{
    Aberto = 0,
    Fechado = 1
}

/// <summary>
/// Turno (sessao) de caixa: tem abertura com fundo (troco inicial), opera durante
/// o dia e e fechado no fim, gerando a Leitura Z. Vendas ficam ligadas ao turno,
/// permitindo contabilidade separada por dia (sabado x domingo no mesmo banco).
/// </summary>
public sealed class Turno
{
    public long Id { get; set; }
    public DateTime Abertura { get; set; } = DateTime.Now;
    public DateTime? Fechamento { get; set; }
    /// <summary>Fundo de caixa (troco inicial) em CENTAVOS.</summary>
    public int FundoCentavos { get; set; }
    public string Operador { get; set; } = "";
    public StatusCaixa Status { get; set; } = StatusCaixa.Aberto;

    public bool EstaAberto => Status == StatusCaixa.Aberto;
}

/// <summary>Tipo de movimento de dinheiro na gaveta (fora vendas).</summary>
public enum TipoMovimento
{
    /// <summary>Retirada de dinheiro do caixa (seguranca).</summary>
    Sangria = 0,
    /// <summary>Entrada de dinheiro/troco no caixa.</summary>
    Suprimento = 1
}

/// <summary>Movimento manual de caixa (sangria ou suprimento) dentro de um turno.</summary>
public sealed class MovimentoCaixa
{
    public long Id { get; set; }
    public long CaixaId { get; set; }
    public TipoMovimento Tipo { get; set; }
    /// <summary>Valor em CENTAVOS (sempre positivo; o sinal vem do Tipo).</summary>
    public int ValorCentavos { get; set; }
    public string Motivo { get; set; } = "";
    public DateTime DataHora { get; set; } = DateTime.Now;
}
