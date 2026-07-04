using PdvFesta.App;
using PdvFesta.Core;
using Xunit;

namespace PdvFesta.Tests;

/// <summary>
/// Valida os DADOS que alimentam o Painel em Tempo Real (F4): Total em Gaveta, faturamento,
/// nro de vendas e itens mais vendidos. A parte visual (graficos GDI) e coberta no E2E.
/// </summary>
public class DashboardDadosTests : IDisposable
{
    private readonly string _db;
    private readonly Servico _servico;

    public DashboardDadosTests()
    {
        _db = Path.Combine(Path.GetTempPath(), $"dash_{Guid.NewGuid():N}.db");
        _servico = new Servico(_db, "___inexistente___.json");
        _servico.ImpressaoSimulada = true;   // nunca imprime na impressora real durante o teste
    }

    public void Dispose()
    {
        _servico.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var ext in new[] { "", "-wal", "-shm" })
            if (File.Exists(_db + ext)) { try { File.Delete(_db + ext); } catch { } }
    }

    private void Vender(string id, string nome, int preco, int qtd, FormaPagamento forma)
    {
        var prod = new Produto { Id = id, Nome = nome, PrecoCentavos = preco, Categoria = "C" };
        for (int i = 0; i < qtd; i++) _servico.Carrinho.Adicionar(prod);
        _servico.FinalizarVenda(forma, preco * qtd, "op");
    }

    [Fact]
    public void ResumoTurnoAtual_RefleteVendasEFundo()
    {
        _servico.AbrirCaixa(10000, "op");                  // fundo R$100
        Vender("q", "Quentao", 700, 2, FormaPagamento.Dinheiro);   // +14 dinheiro
        Vender("r", "Refri", 600, 1, FormaPagamento.Pix);          // pix nao entra na gaveta

        var r = _servico.ResumoTurnoAtual();
        Assert.Equal(10000 + 1400, r.TotalGavetaCentavos);   // fundo + dinheiro
        Assert.Equal(2000, r.Vendas.FaturamentoBrutoCentavos);
        Assert.Equal(2, r.Vendas.QuantidadeVendas);
        Assert.Equal(1400, r.Vendas.TotalDinheiroCentavos);
        Assert.Equal(600, r.Vendas.TotalPixCentavos);
    }

    [Fact]
    public void CanceladasDoTurno_ContadasSeparado_ForaDoFaturamento()
    {
        _servico.AbrirCaixa(0, "op");
        Vender("q", "Quentao", 700, 1, FormaPagamento.Dinheiro);
        Vender("r", "Refri", 600, 1, FormaPagamento.Dinheiro);

        // cancela a 1a venda
        var venda = _servico.VendasDoTurno().First();
        _servico.CancelarVenda(venda.Id);

        var vendas = _servico.VendasDoTurno();
        int canceladas = vendas.Count(v => v.Cancelada);
        Assert.Equal(1, canceladas);                                 // painel mostra 1 cancelada

        var r = _servico.ResumoTurnoAtual();
        Assert.Equal(1, r.Vendas.QuantidadeVendas);                  // so a valida conta
        Assert.Equal(600, r.Vendas.FaturamentoBrutoCentavos);        // cancelada fora do faturamento
    }

    [Fact]
    public void SangriaESuprimento_AjustamGavetaDoTurno()
    {
        _servico.AbrirCaixa(10000, "op");                            // fundo R$100
        Vender("q", "Quentao", 700, 1, FormaPagamento.Dinheiro);    // +7 -> gaveta 107
        _servico.RegistrarMovimento(TipoMovimento.Suprimento, 5000, "troco");   // +50 -> 157
        _servico.RegistrarMovimento(TipoMovimento.Sangria, 3000, "cofre");      // -30 -> 127

        var r = _servico.ResumoTurnoAtual();
        Assert.Equal(5000, r.SuprimentosCentavos);
        Assert.Equal(3000, r.SangriasCentavos);
        Assert.Equal(10000 + 700 + 5000 - 3000, r.TotalGavetaCentavos);   // 127,00
    }

    [Fact]
    public void ItensVendidosTurno_AgrupadoEOrdenadoPorValor()
    {
        _servico.AbrirCaixa(0, "op");
        Vender("cachorro", "Cachorro", 1000, 3, FormaPagamento.Dinheiro);   // R$30
        Vender("agua", "Agua", 500, 3, FormaPagamento.Dinheiro);           // R$15

        var itens = _servico.ItensVendidosTurno();
        Assert.Equal(2, itens.Count);
        Assert.Equal("Cachorro", itens[0].Nome);     // mais vendido (valor) primeiro
        Assert.Equal(3, itens[0].Quantidade);
        Assert.Equal(3000, itens[0].TotalCentavos);
    }

    [Fact]
    public void SemCaixaAberto_ResumoZerado()
    {
        var r = _servico.ResumoTurnoAtual();
        Assert.Equal(0, r.Vendas.QuantidadeVendas);
        Assert.Empty(_servico.ItensVendidosTurno());
    }

    [Fact]
    public void EstornoNaoContaNoDashboard()
    {
        var t = _servico.AbrirCaixa(0, "op");
        Vender("x", "X", 1000, 1, FormaPagamento.Dinheiro);
        var venda = _servico.Repo.ListarVendasPorCaixa(t.Id).First();
        _servico.CancelarVenda(venda.Id);

        var r = _servico.ResumoTurnoAtual();
        Assert.Equal(0, r.Vendas.QuantidadeVendas);
        Assert.Equal(0, r.Vendas.TotalDinheiroCentavos);
    }
}
