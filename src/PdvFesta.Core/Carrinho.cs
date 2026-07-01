namespace PdvFesta.Core;

/// <summary>
/// Carrinho de compra em andamento. Agrupa itens iguais e mantem snapshot
/// do preco no momento em que foram adicionados (nao muda se o catalogo mudar).
/// </summary>
public sealed class Carrinho
{
    private readonly List<ItemVenda> _itens = new();
    private readonly List<DescontoAplicado> _descontos = new();

    public IReadOnlyList<ItemVenda> Itens => _itens;
    /// <summary>Descontos de combos/promocoes detectados automaticamente (linhas verdes).</summary>
    public IReadOnlyList<DescontoAplicado> Descontos => _descontos;

    public int SubtotalCentavos => _itens.Sum(i => i.SubtotalCentavos);
    public int DescontoTotalCentavos => _descontos.Sum(d => d.ValorCentavos);
    public int TotalCentavos => SubtotalCentavos - DescontoTotalCentavos;

    /// <summary>
    /// Reavalia as promocoes ativas sobre o carrinho (chamar a cada add/remove).
    /// O motor de precos e puro; aqui so guardamos o resultado.
    /// </summary>
    public void AplicarDescontos(IEnumerable<Promocao> promocoes, DateTime agora)
    {
        _descontos.Clear();
        _descontos.AddRange(PricingEngine.Calcular(_itens, promocoes, agora));
    }

    /// <summary>Adiciona um produto (ou combo). Se ja existir, soma a quantidade.</summary>
    public void Adicionar(Produto produto, int quantidade = 1)
    {
        if (quantidade <= 0) return;

        var existente = _itens.FirstOrDefault(i => i.ProdutoId == produto.Id);
        if (existente is not null)
        {
            existente.Quantidade += quantidade;
            return;
        }

        _itens.Add(new ItemVenda
        {
            ProdutoId = produto.Id,
            Nome = produto.Nome,
            PrecoUnitarioCentavos = produto.PrecoCentavos, // snapshot
            Quantidade = quantidade
        });
    }

    /// <summary>Remove a linha inteira do produto.</summary>
    public void Remover(string produtoId) =>
        _itens.RemoveAll(i => i.ProdutoId == produtoId);

    /// <summary>Diminui 1 unidade; se chegar a zero, remove a linha.</summary>
    public void DiminuirUm(string produtoId)
    {
        var item = _itens.FirstOrDefault(i => i.ProdutoId == produtoId);
        if (item is null) return;

        item.Quantidade--;
        if (item.Quantidade <= 0)
            _itens.Remove(item);
    }

    public void Limpar() { _itens.Clear(); _descontos.Clear(); }

    /// <summary>Converte o carrinho em uma Venda pronta para persistir.</summary>
    public Venda FecharVenda(FormaPagamento forma, int recebidoCentavos, string operador)
    {
        var total = TotalCentavos;
        var troco = forma == FormaPagamento.Dinheiro
            ? Caixa.CalcularTroco(total, recebidoCentavos)
            : 0;

        // itens do carrinho + linhas de desconto (ProdutoId vazio, subtotal NEGATIVO).
        // Assim o desconto persiste, imprime e entra no total, mantendo a rastreabilidade.
        var itensVenda = _itens.Select(i => new ItemVenda
        {
            ProdutoId = i.ProdutoId,
            Nome = i.Nome,
            PrecoUnitarioCentavos = i.PrecoUnitarioCentavos,
            Quantidade = i.Quantidade
        }).ToList();
        itensVenda.AddRange(_descontos.Select(d => new ItemVenda
        {
            ProdutoId = "",                       // marcador de linha de desconto
            Nome = d.Descricao,
            PrecoUnitarioCentavos = -d.ValorCentavos,
            Quantidade = 1
        }));

        return new Venda
        {
            DataHora = DateTime.Now,
            Itens = itensVenda,
            TotalCentavos = total,
            Forma = forma,
            RecebidoCentavos = forma == FormaPagamento.Dinheiro ? recebidoCentavos : 0,
            TrocoCentavos = troco,
            Operador = operador
        };
    }
}
