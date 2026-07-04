using PdvFesta.App;
using PdvFesta.Core;
using Xunit;

namespace PdvFesta.Tests;

/// <summary>
/// Visao gerencial GLOBAL (Balanco Geral): consolida TODOS os turnos com seu resumo, e o
/// ListarTurnos do repositorio traz abertos e fechados. Cobre o livro-caixa do administrador.
/// </summary>
public class BalancoGeralTests : IDisposable
{
    private readonly string _db;
    private readonly Servico _servico;

    public BalancoGeralTests()
    {
        _db = Path.Combine(Path.GetTempPath(), $"balanco_{Guid.NewGuid():N}.db");
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

    private void Vender(int preco, FormaPagamento forma)
    {
        _servico.Carrinho.Adicionar(new Produto { Id = "x", Nome = "X", PrecoCentavos = preco, Categoria = "C" });
        _servico.FinalizarVenda(forma, preco, "op");
    }

    [Fact]
    public void ConsolidarTodosOsTurnos_TrazTodosOsCaixas_MaisRecentePrimeiro()
    {
        // turno 1 (fechado)
        _servico.AbrirCaixa(10000, "Ana");
        Vender(700, FormaPagamento.Dinheiro);
        _servico.FecharCaixa();
        // turno 2 (aberto)
        _servico.AbrirCaixa(5000, "Bruno");
        Vender(600, FormaPagamento.Pix);

        var todos = _servico.ConsolidarTodosOsTurnos();
        Assert.Equal(2, todos.Count);
        // mais recente primeiro
        Assert.Equal("Bruno", todos[0].Turno.Operador);
        Assert.Equal(StatusCaixa.Aberto, todos[0].Turno.Status);
        Assert.Equal("Ana", todos[1].Turno.Operador);
        Assert.Equal(StatusCaixa.Fechado, todos[1].Turno.Status);
    }

    [Fact]
    public void ConsolidarTodosOsTurnos_CadaTurnoTemSeusPropriosTotais()
    {
        _servico.AbrirCaixa(10000, "Ana");
        Vender(700, FormaPagamento.Dinheiro);   // gaveta 107
        _servico.FecharCaixa();
        _servico.AbrirCaixa(0, "Bruno");
        Vender(600, FormaPagamento.Dinheiro);   // gaveta 6

        var todos = _servico.ConsolidarTodosOsTurnos();
        var ana = todos.First(r => r.Turno.Operador == "Ana");
        var bruno = todos.First(r => r.Turno.Operador == "Bruno");

        Assert.Equal(10000 + 700, ana.TotalGavetaCentavos);
        Assert.Equal(600, bruno.TotalGavetaCentavos);
        Assert.Equal(1, ana.Vendas.QuantidadeVendas);
        Assert.Equal(1, bruno.Vendas.QuantidadeVendas);
    }

    [Fact]
    public void ConsolidarTodosOsTurnos_IncluiSangriaESuprimentoNoResumo()
    {
        _servico.AbrirCaixa(10000, "Ana");
        _servico.RegistrarMovimento(TipoMovimento.Suprimento, 3000, "troco");
        _servico.RegistrarMovimento(TipoMovimento.Sangria, 2000, "cofre");

        var r = _servico.ConsolidarTodosOsTurnos().First();
        Assert.Equal(3000, r.SuprimentosCentavos);
        Assert.Equal(2000, r.SangriasCentavos);
        Assert.Equal(10000 + 3000 - 2000, r.TotalGavetaCentavos);
    }

    [Fact]
    public void ListarTurnos_Vazio_QuandoNaoHaCaixa()
    {
        Assert.Empty(_servico.ConsolidarTodosOsTurnos());
    }

    [Fact]
    public void MovimentosDoTurno_RetornaOsMovimentosDaqueleCaixa()
    {
        var t = _servico.AbrirCaixa(0, "op");
        _servico.RegistrarMovimento(TipoMovimento.Sangria, 500, "teste");
        var movs = _servico.MovimentosDoTurno(t.Id);
        Assert.Single(movs);
        Assert.Equal(TipoMovimento.Sangria, movs[0].Tipo);
        Assert.Equal(500, movs[0].ValorCentavos);
    }
}
