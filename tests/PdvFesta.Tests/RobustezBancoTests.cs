using PdvFesta.Core;
using Xunit;

namespace PdvFesta.Tests;

public class RobustezBancoTests : IDisposable
{
    private readonly string _db;
    private readonly Repositorio _repo;

    public RobustezBancoTests()
    {
        _db = Path.Combine(Path.GetTempPath(), $"rob_{Guid.NewGuid():N}.db");
        _repo = new Repositorio(_db);
        _repo.Inicializar();
    }

    public void Dispose()
    {
        _repo.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var ext in new[] { "", "-wal", "-shm" })
            if (File.Exists(_db + ext)) { try { File.Delete(_db + ext); } catch { } }
    }

    private Venda VendaDinheiro(int total) => new()
    {
        Forma = FormaPagamento.Dinheiro, TotalCentavos = total, RecebidoCentavos = total, TrocoCentavos = 0,
        Itens = { new ItemVenda { ProdutoId = "x", Nome = "X", PrecoUnitarioCentavos = total, Quantidade = 1 } }
    };

    [Fact]
    public void Inicializar_Idempotente()
    {
        _repo.Inicializar();
        _repo.Inicializar();
        Assert.Null(_repo.CaixaAberto());
    }

    [Fact]
    public void Venda_RoundTrip_PreservaCampos()
    {
        var turno = _repo.AbrirCaixa(10000, "joao");
        var venda = VendaDinheiro(1700);
        venda.CaixaId = turno.Id;
        venda.RecebidoCentavos = 2000; venda.TrocoCentavos = 300;
        var id = _repo.SalvarVenda(venda);

        var lida = _repo.ListarVendas().First(v => v.Id == id);
        Assert.Equal(1700, lida.TotalCentavos);
        Assert.Equal(2000, lida.RecebidoCentavos);
        Assert.Equal(300, lida.TrocoCentavos);
        Assert.Equal(turno.Id, lida.CaixaId);
        Assert.Single(lida.Itens);
    }

    [Fact]
    public void CaixaAberto_RetomaAposReabrirRepositorio()
    {
        var turno = _repo.AbrirCaixa(5000, "maria");
        _repo.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        using var repo2 = new Repositorio(_db);
        repo2.Inicializar();
        var retomado = repo2.CaixaAberto();
        Assert.NotNull(retomado);
        Assert.Equal(turno.Id, retomado!.Id);
        Assert.Equal(5000, retomado.FundoCentavos);
    }

    [Fact]
    public void FecharCaixa_CaixaAbertoFicaNull()
    {
        var turno = _repo.AbrirCaixa(0, "op");
        _repo.FecharCaixa(turno.Id);
        Assert.Null(_repo.CaixaAberto());
    }

    [Fact]
    public void DoisTurnos_CaixaAbertoRetornaOMaisRecente()
    {
        var t1 = _repo.AbrirCaixa(1000, "a");
        _repo.FecharCaixa(t1.Id);
        var t2 = _repo.AbrirCaixa(2000, "b");
        Assert.Equal(t2.Id, _repo.CaixaAberto()!.Id);
    }

    [Fact]
    public void ValorExtremo_MilhaoDeReais_Persiste()
    {
        var turno = _repo.AbrirCaixa(0, "op");
        var venda = VendaDinheiro(100_000_000);   // R$ 1.000.000,00
        venda.CaixaId = turno.Id;
        var id = _repo.SalvarVenda(venda);
        Assert.Equal(100_000_000, _repo.ListarVendas().First(v => v.Id == id).TotalCentavos);
    }

    [Fact]
    public void VendasPorCaixa_FiltraCorretamente()
    {
        var t1 = _repo.AbrirCaixa(0, "a");
        var v1 = VendaDinheiro(500); v1.CaixaId = t1.Id; _repo.SalvarVenda(v1);
        _repo.FecharCaixa(t1.Id);
        var t2 = _repo.AbrirCaixa(0, "b");
        var v2 = VendaDinheiro(700); v2.CaixaId = t2.Id; _repo.SalvarVenda(v2);

        Assert.Single(_repo.ListarVendasPorCaixa(t1.Id));
        Assert.Single(_repo.ListarVendasPorCaixa(t2.Id));
    }

    [Fact]
    public void Config_RoundTrip_ComCaracteresEspeciais()
    {
        _repo.SalvarConfig("evento", "Arraiá do João & Maria");
        Assert.Equal("Arraiá do João & Maria", _repo.LerConfig("evento", ""));
    }

    [Fact]
    public void Config_ChaveInexistente_RetornaDefault()
    {
        Assert.Equal("padrao", _repo.LerConfig("nao_existe", "padrao"));
    }

    [Fact]
    public void EstornoZeraContribuicaoNoResumo()
    {
        var turno = _repo.AbrirCaixa(0, "op");
        var v = VendaDinheiro(1000); v.CaixaId = turno.Id;
        var id = _repo.SalvarVenda(v);
        _repo.CancelarVenda(id);

        var resumo = Caixa.ConsolidarTurno(turno, _repo.ListarVendasPorCaixa(turno.Id), new List<MovimentoCaixa>());
        Assert.Equal(0, resumo.Vendas.TotalDinheiroCentavos);
        Assert.Equal(0, resumo.Vendas.QuantidadeVendas);
    }
}
