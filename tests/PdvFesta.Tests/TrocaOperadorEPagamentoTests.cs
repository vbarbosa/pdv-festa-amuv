using PdvFesta.App;
using PdvFesta.Core;
using Xunit;

namespace PdvFesta.Tests;

public class TrocaOperadorEPagamentoTests : IDisposable
{
    private readonly string _db;
    private readonly Servico _servico;

    public TrocaOperadorEPagamentoTests()
    {
        _db = Path.Combine(Path.GetTempPath(), $"troca_{Guid.NewGuid():N}.db");
        _servico = new Servico(_db, "___inexistente___.json");
    }

    public void Dispose()
    {
        _servico.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var ext in new[] { "", "-wal", "-shm" })
            if (File.Exists(_db + ext)) { try { File.Delete(_db + ext); } catch { } }
    }

    [Fact]
    public void TrocarOperador_FechaTurnoAtual_AbreNovoComOperadorEFundo()
    {
        var t1 = _servico.AbrirCaixa(10000, "joao");
        var t2 = _servico.TrocarOperador("maria", 15000);

        Assert.NotEqual(t1.Id, t2.Id);              // turno novo
        Assert.Equal("maria", t2.Operador);
        Assert.Equal(15000, t2.FundoCentavos);
        Assert.True(_servico.CaixaAberto);          // segue com caixa aberto (o novo)
        Assert.Equal(t2.Id, _servico.TurnoAtual!.Id);
    }

    [Fact]
    public void TrocarOperador_TurnoAntigoFicaFechado()
    {
        var t1 = _servico.AbrirCaixa(0, "a");
        // registra uma venda no turno 1
        _servico.Carrinho.Adicionar(new Produto { Id = "x", Nome = "X", PrecoCentavos = 500, Categoria = "C" });
        _servico.FinalizarVenda(FormaPagamento.Dinheiro, 500, "a");
        _servico.TrocarOperador("b", 0);

        // as vendas do turno 1 continuam la (nao migram) e o turno 1 esta fechado.
        var vendasT1 = _servico.Repo.ListarVendasPorCaixa(t1.Id);
        Assert.Single(vendasT1);
        Assert.Null(_servico.Repo.CaixaAberto() is { } aberto && aberto.Id == t1.Id ? (object?)aberto : null);
    }

    [Fact]
    public void BaterGaveta_AposVendas_CalculaEsperadoCorreto()
    {
        _servico.AbrirCaixa(10000, "op");                       // fundo 100
        _servico.Carrinho.Adicionar(new Produto { Id = "q", Nome = "Quentao", PrecoCentavos = 700, Categoria = "Bebidas" });
        _servico.FinalizarVenda(FormaPagamento.Dinheiro, 700, "op");   // +7 na gaveta

        var r = _servico.BaterGaveta(10700);
        Assert.True(r.Bate);
        Assert.Equal(10700, r.EsperadoCentavos);
    }

    // ---- soma de notas do pagamento (a mesma regra do FormPagamento: recebido += nota) ----
    [Theory]
    [InlineData(new[] { 5000, 2000 }, 7000)]        // R$50 + R$20
    [InlineData(new[] { 100, 200, 500 }, 800)]      // moedas 1+2+5
    [InlineData(new[] { 10000 }, 10000)]            // uma nota
    [InlineData(new[] { 200, 200, 200 }, 600)]      // tres de R$2
    public void SomaDeNotas_AcumulaOValorRecebido(int[] notas, int esperado)
    {
        // simula o comportamento do botao "R$X que SOMA": recebido comeca 0 e soma cada nota.
        int recebido = 0;
        foreach (var n in notas)
        {
            var atual = Dinheiro.ParseCentavos(Dinheiro.FormatarSemSimbolo(recebido)) ?? 0;
            recebido = atual + n;
        }
        Assert.Equal(esperado, recebido);
    }

    [Fact]
    public void Exato_PreencheORecebidoComOTotal_TrocoZero()
    {
        int total = 1100;
        int recebido = total;   // botao "Exato"
        Assert.Equal(0, Caixa.CalcularTroco(total, recebido));
    }
}
