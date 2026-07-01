using PdvFesta.Core;
using Xunit;

namespace PdvFesta.Tests;

/// <summary>
/// TDD - regras do CARRINHO: soma, multiplicacao por quantidade, combos.
/// Tudo em centavos (int) para nao ter erro de arredondamento com dinheiro.
/// </summary>
public class CarrinhoTests
{
    private static Produto Quentao => new() { Id = "quentao", Nome = "Quentao", PrecoCentavos = 700 };
    private static Produto Pipoca => new() { Id = "pipoca", Nome = "Pipoca", PrecoCentavos = 500 };
    private static Produto Bingo => new() { Id = "bingo", Nome = "Cartela Bingo", PrecoCentavos = 600 };

    [Fact]
    public void CarrinhoVazio_TotalZero()
    {
        var c = new Carrinho();
        Assert.Equal(0, c.TotalCentavos);
        Assert.Empty(c.Itens);
    }

    [Fact]
    public void AdicionarUmItem_SomaPreco()
    {
        var c = new Carrinho();
        c.Adicionar(Quentao);
        Assert.Equal(700, c.TotalCentavos);
        Assert.Single(c.Itens);
    }

    [Fact]
    public void AdicionarMesmoItemDuasVezes_AgrupaEQuantidadeVira2()
    {
        var c = new Carrinho();
        c.Adicionar(Quentao);
        c.Adicionar(Quentao);
        Assert.Single(c.Itens);               // agrupa, nao duplica linha
        Assert.Equal(2, c.Itens[0].Quantidade);
        Assert.Equal(1400, c.TotalCentavos);   // 2 x 700
    }

    [Fact]
    public void AdicionarItensDiferentes_SomaCorreta()
    {
        var c = new Carrinho();
        c.Adicionar(Quentao); // 700
        c.Adicionar(Pipoca);  // 500
        c.Adicionar(Bingo);   // 600
        Assert.Equal(3, c.Itens.Count);
        Assert.Equal(1800, c.TotalCentavos);
    }

    [Fact]
    public void AdicionarComQuantidade_MultiplicaCerto()
    {
        var c = new Carrinho();
        c.Adicionar(Pipoca, 3); // 3 x 500 = 1500
        Assert.Equal(1500, c.TotalCentavos);
        Assert.Equal(3, c.Itens[0].Quantidade);
    }

    [Fact]
    public void RemoverItem_AtualizaTotal()
    {
        var c = new Carrinho();
        c.Adicionar(Quentao);
        c.Adicionar(Pipoca);
        c.Remover("quentao");
        Assert.Single(c.Itens);
        Assert.Equal(500, c.TotalCentavos);
    }

    [Fact]
    public void DiminuirQuantidade_AteZero_RemoveLinha()
    {
        var c = new Carrinho();
        c.Adicionar(Quentao, 2);
        c.DiminuirUm("quentao"); // fica 1
        Assert.Equal(1, c.Itens[0].Quantidade);
        c.DiminuirUm("quentao"); // fica 0 -> remove
        Assert.Empty(c.Itens);
        Assert.Equal(0, c.TotalCentavos);
    }

    [Fact]
    public void Limpar_EsvaziaCarrinho()
    {
        var c = new Carrinho();
        c.Adicionar(Quentao);
        c.Adicionar(Pipoca);
        c.Limpar();
        Assert.Empty(c.Itens);
        Assert.Equal(0, c.TotalCentavos);
    }

    [Fact]
    public void Combo_UsaPrecoDoCombo_NaoSomaDosItens()
    {
        // Combo: 2 cachorros (1000 cada) + 1 refri (600) = 2600 avulso,
        // mas o combo tem preco promocional de 2000.
        var combo = new Produto
        {
            Id = "combo1", Nome = "2 Dog + Refri", PrecoCentavos = 2000,
            Composicao =
            {
                new ComboItem { ProdutoId = "dog", Quantidade = 2 },
                new ComboItem { ProdutoId = "refri", Quantidade = 1 }
            }
        };
        var c = new Carrinho();
        c.Adicionar(combo);
        Assert.True(combo.EhCombo);
        Assert.Equal(2000, c.TotalCentavos); // usa preco do combo, nao 2600
    }

    [Fact]
    public void SnapshotDePreco_NaoMudaSeProdutoMudarDepois()
    {
        var p = new Produto { Id = "x", Nome = "X", PrecoCentavos = 700 };
        var c = new Carrinho();
        c.Adicionar(p);
        p.PrecoCentavos = 999; // alterou o catalogo depois
        Assert.Equal(700, c.TotalCentavos); // carrinho manteve o preco do momento
    }
}
