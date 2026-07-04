using PdvFesta.App;
using PdvFesta.Core;
using Xunit;

namespace PdvFesta.Tests;

/// <summary>
/// Bug de campo: operador tinha itens NO CARRINHO e cadastrou a promocao depois — o desconto
/// nao aparecia na hora (so no proximo item). Garante que, apos salvar uma promo e reaplicar,
/// o carrinho ja aberto recebe o desconto imediatamente.
/// </summary>
public class PromocaoCarrinhoVivoTests : IDisposable
{
    private readonly string _db;
    private readonly Servico _servico;

    public PromocaoCarrinhoVivoTests()
    {
        _db = Path.Combine(Path.GetTempPath(), $"promoviva_{Guid.NewGuid():N}.db");
        _servico = new Servico(_db, "___inexistente___.json");
        _servico.ImpressaoSimulada = true;
    }

    public void Dispose()
    {
        _servico.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var ext in new[] { "", "-wal", "-shm" })
            if (File.Exists(_db + ext)) { try { File.Delete(_db + ext); } catch { } }
    }

    private static Produto Prod(string id, int preco) =>
        new() { Id = id, Nome = id, PrecoCentavos = preco, Categoria = "C" };

    [Fact]
    public void PromoCadastradaComItemNoCarrinho_AplicaAoReavaliar()
    {
        // 1) itens JA no carrinho, sem nenhuma promo ainda
        _servico.Carrinho.Adicionar(Prod("refri", 600));
        _servico.Carrinho.Adicionar(Prod("bolo", 400));
        _servico.AplicarPromocoes();
        Assert.Empty(_servico.Carrinho.Descontos);   // ainda sem desconto

        // 2) operador cadastra a promo AGORA (itens ja estao no carrinho)
        _servico.SalvarPromocao(new Promocao
        {
            Descricao = "COMBO Refri+Bolo", Tipo = TipoPromocao.Combo, ValorDescontoCentavos = 200,
            Ativo = true,
            Itens = { new PromocaoItem { ProdutoId = "refri", Quantidade = 1 },
                      new PromocaoItem { ProdutoId = "bolo", Quantidade = 1 } }
        });

        // 3) reavaliar (o que a tela faz ao fechar a gestao de promocoes) -> desconto aparece
        _servico.AplicarPromocoes();
        Assert.Single(_servico.Carrinho.Descontos);
        Assert.Equal(200, _servico.Carrinho.Descontos[0].ValorCentavos);
    }

    [Fact]
    public void PromoInativada_RemoveDescontoDoCarrinhoAoReavaliar()
    {
        var id = _servico.SalvarPromocao(new Promocao
        {
            Descricao = "COMBO", Tipo = TipoPromocao.Combo, ValorDescontoCentavos = 200, Ativo = true,
            Itens = { new PromocaoItem { ProdutoId = "refri", Quantidade = 1 },
                      new PromocaoItem { ProdutoId = "bolo", Quantidade = 1 } }
        });
        _servico.Carrinho.Adicionar(Prod("refri", 600));
        _servico.Carrinho.Adicionar(Prod("bolo", 400));
        _servico.AplicarPromocoes();
        Assert.Single(_servico.Carrinho.Descontos);   // com desconto

        // operador desliga a promo -> ao reavaliar, o desconto some
        _servico.InativarPromocao(id);
        _servico.AplicarPromocoes();
        Assert.Empty(_servico.Carrinho.Descontos);
    }
}
