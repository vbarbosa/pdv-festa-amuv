namespace PdvFesta.Core;

/// <summary>
/// Carrinho de compra em andamento. Agrupa itens iguais e mantem snapshot
/// do preco no momento em que foram adicionados (nao muda se o catalogo mudar).
/// </summary>
public sealed class Carrinho
{
    private readonly List<ItemVenda> _itens = new();

    public IReadOnlyList<ItemVenda> Itens => _itens;

    public int TotalCentavos => _itens.Sum(i => i.SubtotalCentavos);

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

    public void Limpar() => _itens.Clear();

    /// <summary>Converte o carrinho em uma Venda pronta para persistir.</summary>
    public Venda FecharVenda(FormaPagamento forma, int recebidoCentavos, string operador)
    {
        var total = TotalCentavos;
        var troco = forma == FormaPagamento.Dinheiro
            ? Caixa.CalcularTroco(total, recebidoCentavos)
            : 0;

        return new Venda
        {
            DataHora = DateTime.Now,
            Itens = _itens.Select(i => new ItemVenda
            {
                ProdutoId = i.ProdutoId,
                Nome = i.Nome,
                PrecoUnitarioCentavos = i.PrecoUnitarioCentavos,
                Quantidade = i.Quantidade
            }).ToList(),
            TotalCentavos = total,
            Forma = forma,
            RecebidoCentavos = forma == FormaPagamento.Dinheiro ? recebidoCentavos : 0,
            TrocoCentavos = troco,
            Operador = operador
        };
    }
}
