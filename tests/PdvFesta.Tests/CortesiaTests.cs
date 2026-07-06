using PdvFesta.App;
using PdvFesta.Core;
using Xunit;

namespace PdvFesta.Tests;

/// <summary>
/// CORTESIA (brinde): entrega os itens sem cobrar. NAO entra na gaveta nem no faturamento,
/// mas fica contada e rastreavel (nome de quem recebeu na observacao).
/// </summary>
public class CortesiaTests : IDisposable
{
    private readonly string _db;
    private readonly Servico _servico;

    public CortesiaTests()
    {
        _db = Path.Combine(Path.GetTempPath(), $"cort_{Guid.NewGuid():N}.db");
        _servico = new Servico(_db, "___inexistente___.json");
        _servico.ImpressaoSimulada = true;
        _servico.AbrirCaixa(10000, "op");   // fundo R$100
    }

    public void Dispose()
    {
        _servico.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var ext in new[] { "", "-wal", "-shm" })
            if (File.Exists(_db + ext)) { try { File.Delete(_db + ext); } catch { } }
    }

    private Venda VenderCortesia(string nome, int preco)
    {
        _servico.Carrinho.Adicionar(new Produto { Id = "q", Nome = "Quentao", PrecoCentavos = preco, Categoria = "C" });
        var (venda, _, _) = _servico.FinalizarVenda(FormaPagamento.Cortesia, 0, "op", observacao: "CORTESIA: " + nome);
        return venda;
    }

    [Fact]
    public void Cortesia_NaoEntraNaGavetaNemNoFaturamento()
    {
        // uma venda em dinheiro (entra) + uma cortesia (nao entra)
        _servico.Carrinho.Adicionar(new Produto { Id = "d", Nome = "Refri", PrecoCentavos = 600, Categoria = "C" });
        _servico.FinalizarVenda(FormaPagamento.Dinheiro, 600, "op");
        VenderCortesia("Joao Cantor", 700);

        var r = _servico.ResumoTurnoAtual();
        Assert.Equal(600, r.Vendas.FaturamentoBrutoCentavos);          // so a venda paga
        Assert.Equal(10000 + 600, r.TotalGavetaCentavos);             // cortesia NAO soma na gaveta
        Assert.Equal(1, r.Vendas.QuantidadeVendas);                   // cortesia nao conta como venda paga
        Assert.Equal(1, r.Vendas.QuantidadeCortesias);               // mas conta como cortesia
        Assert.Equal(700, r.Vendas.TotalCortesiaCentavos);           // e o valor do brinde fica registrado
    }

    [Fact]
    public void Cortesia_GuardaONomeNaObservacao()
    {
        var venda = VenderCortesia("Maria Palhaca", 500);
        var doBanco = _servico.Repo.ListarVendas().First(v => v.Id == venda.Id);
        Assert.Equal(FormaPagamento.Cortesia, doBanco.Forma);
        Assert.Contains("Maria Palhaca", doBanco.Observacao);
        Assert.True(doBanco.EhCortesia);
    }

    [Fact]
    public void Cortesia_NaoImpedeVendaCancelada_ContinuaForaDosTotais()
    {
        var venda = VenderCortesia("Ze", 700);
        // cancelar uma cortesia: some tudo (ja estava fora do faturamento)
        _servico.CancelarVenda(venda.Id);
        var r = _servico.ResumoTurnoAtual();
        Assert.Equal(0, r.Vendas.QuantidadeCortesias);
        Assert.Equal(0, r.Vendas.TotalCortesiaCentavos);
    }

    [Fact]
    public void Cortesia_ContaSeparadaDeVarias()
    {
        VenderCortesia("A", 700);
        VenderCortesia("B", 300);
        VenderCortesia("C", 1000);
        var r = _servico.ResumoTurnoAtual();
        Assert.Equal(3, r.Vendas.QuantidadeCortesias);
        Assert.Equal(2000, r.Vendas.TotalCortesiaCentavos);
        Assert.Equal(0, r.Vendas.FaturamentoBrutoCentavos);
    }
}
