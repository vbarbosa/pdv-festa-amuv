using PdvFesta.App;
using PdvFesta.Core;
using Xunit;

namespace PdvFesta.Tests;

/// <summary>
/// Modulo gerencial DESACOPLADO do caixa (time travel): consulta vendas por periodo de datas
/// direto do banco, mesmo com o caixa FECHADO. Cobre precos praticados (mudanca de preco) e
/// exclusao de caixa de teste.
/// </summary>
public class GerencialPeriodoTests : IDisposable
{
    private readonly string _db;
    private readonly Servico _servico;

    public GerencialPeriodoTests()
    {
        _db = Path.Combine(Path.GetTempPath(), $"ger_{Guid.NewGuid():N}.db");
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

    /// <summary>Grava uma venda com data_hora especifica direto no banco (simula dias passados).</summary>
    private void VendaEmData(DateTime quando, string produto, int preco, int qtd)
    {
        var venda = new Venda
        {
            DataHora = quando, Forma = FormaPagamento.Dinheiro, TotalCentavos = preco * qtd,
            RecebidoCentavos = preco * qtd,
            Itens = { new ItemVenda { ProdutoId = produto, Nome = produto, PrecoUnitarioCentavos = preco, Quantidade = qtd } }
        };
        _servico.Repo.SalvarVenda(venda);
    }

    [Fact]
    public void VendasPorPeriodo_FiltraPorData_MesmoComCaixaFechado()
    {
        VendaEmData(new DateTime(2026, 7, 4, 20, 0, 0), "Chopp", 1000, 2);   // festa
        VendaEmData(new DateTime(2026, 7, 5, 12, 0, 0), "Refri", 600, 1);    // dia seguinte

        // filtra SO o dia 04 (nao ha caixa aberto — antes isso vinha zerado)
        var dia04 = _servico.VendasPorPeriodo(new DateTime(2026, 7, 4), new DateTime(2026, 7, 4, 23, 59, 59));
        Assert.Single(dia04);
        Assert.Equal(2000, dia04[0].TotalCentavos);

        // filtra so o dia 05
        var dia05 = _servico.VendasPorPeriodo(new DateTime(2026, 7, 5), new DateTime(2026, 7, 5, 23, 59, 59));
        Assert.Single(dia05);
        Assert.Equal(600, dia05[0].TotalCentavos);

        // "tudo" traz as duas
        Assert.Equal(2, _servico.VendasPorPeriodo(null, null).Count);
    }

    [Fact]
    public void ResumoPorPeriodo_SomaSoOPeriodo()
    {
        VendaEmData(new DateTime(2026, 7, 4, 20, 0, 0), "Chopp", 1000, 3);   // 30
        VendaEmData(new DateTime(2026, 7, 5, 12, 0, 0), "Refri", 600, 1);    // 6

        var r = _servico.ResumoPorPeriodo(new DateTime(2026, 7, 4), new DateTime(2026, 7, 4, 23, 59, 59));
        Assert.Equal(3000, r.FaturamentoBrutoCentavos);
        Assert.Equal(1, r.QuantidadeVendas);
    }

    [Fact]
    public void PrecosPraticados_ItemQueMudouDePreco_ApareceEmLinhasSeparadas()
    {
        // Chopp vendido a R$10 (manha) e depois R$12 (noite) — cenario do usuario
        VendaEmData(new DateTime(2026, 7, 4, 11, 0, 0), "Chopp", 1000, 5);   // 5 a R$10
        VendaEmData(new DateTime(2026, 7, 4, 21, 0, 0), "Chopp", 1200, 3);   // 3 a R$12

        var precos = _servico.PrecosPraticadosPorPeriodo(new DateTime(2026, 7, 4), new DateTime(2026, 7, 4, 23, 59, 59));
        var chopp = precos.Where(p => p.Nome == "Chopp").OrderBy(p => p.PrecoUnitarioCentavos).ToList();

        Assert.Equal(2, chopp.Count);                       // dois precos distintos
        Assert.Equal(1000, chopp[0].PrecoUnitarioCentavos);
        Assert.Equal(5, chopp[0].Quantidade);
        Assert.Equal(5000, chopp[0].TotalCentavos);
        Assert.Equal(1200, chopp[1].PrecoUnitarioCentavos);
        Assert.Equal(3, chopp[1].Quantidade);
        Assert.Equal(3600, chopp[1].TotalCentavos);
    }

    [Fact]
    public void PrecosPraticados_ItemComPrecoUnico_UmaLinhaSo()
    {
        VendaEmData(new DateTime(2026, 7, 4, 11, 0, 0), "Refri", 600, 4);
        VendaEmData(new DateTime(2026, 7, 4, 15, 0, 0), "Refri", 600, 2);   // mesmo preco

        var refri = _servico.PrecosPraticadosPorPeriodo(null, null).Where(p => p.Nome == "Refri").ToList();
        Assert.Single(refri);                               // 1 preco -> 1 linha
        Assert.Equal(6, refri[0].Quantidade);               // 4+2 agrupados
    }

    [Fact]
    public void ExcluirCaixaDeTeste_SemVendas_Apaga()
    {
        _servico.AbrirCaixa(0, "teste");
        var t = _servico.TurnoAtual!;
        _servico.FecharCaixa();                              // fechado, sem vendas

        Assert.Equal(0, _servico.VendasNoCaixa(t.Id));
        Assert.True(_servico.ExcluirCaixaDeTeste(t.Id));
        Assert.Empty(_servico.ConsolidarTodosOsTurnos());   // sumiu do historico
    }

    [Fact]
    public void ExcluirCaixaDeTeste_ComVendas_NaoApaga()
    {
        var t = _servico.AbrirCaixa(0, "op");
        _servico.Carrinho.Adicionar(new Produto { Id = "x", Nome = "X", PrecoCentavos = 700, Categoria = "C" });
        _servico.FinalizarVenda(FormaPagamento.Dinheiro, 700, "op");
        _servico.FecharCaixa();

        Assert.False(_servico.ExcluirCaixaDeTeste(t.Id));   // metodo "so vazio" -> protegido
        Assert.Single(_servico.ConsolidarTodosOsTurnos());  // continua no historico
    }

    [Fact]
    public void ExcluirCaixaComVendas_ApagaTurnoEvendas_SemAfetarOutros()
    {
        // turno 1 (que sera excluido) com vendas de teste
        var t1 = _servico.AbrirCaixa(0, "teste");
        _servico.Carrinho.Adicionar(new Produto { Id = "x", Nome = "X", PrecoCentavos = 700, Categoria = "C" });
        _servico.FinalizarVenda(FormaPagamento.Dinheiro, 700, "teste");
        _servico.Carrinho.Adicionar(new Produto { Id = "y", Nome = "Y", PrecoCentavos = 300, Categoria = "C" });
        _servico.FinalizarVenda(FormaPagamento.Pix, 300, "teste");
        _servico.FecharCaixa();
        // turno 2 (real, que deve PERMANECER)
        _servico.AbrirCaixa(0, "real");
        _servico.Carrinho.Adicionar(new Produto { Id = "z", Nome = "Z", PrecoCentavos = 1000, Categoria = "C" });
        _servico.FinalizarVenda(FormaPagamento.Dinheiro, 1000, "real");

        int totalAntes = _servico.VendasPorPeriodo(null, null).Count;
        Assert.Equal(3, totalAntes);

        int apagadas = _servico.ExcluirCaixaComVendas(t1.Id);
        Assert.Equal(2, apagadas);                          // as 2 vendas do turno de teste

        // turno 1 sumiu; turno 2 (real) intacto
        Assert.Single(_servico.ConsolidarTodosOsTurnos());
        var restantes = _servico.VendasPorPeriodo(null, null);
        Assert.Single(restantes);
        Assert.Equal(1000, restantes[0].TotalCentavos);     // a venda real
    }

    [Fact]
    public void ExcluirCaixaComVendas_CaixaAberto_Recusa()
    {
        var t = _servico.AbrirCaixa(0, "op");               // ABERTO agora
        _servico.Carrinho.Adicionar(new Produto { Id = "x", Nome = "X", PrecoCentavos = 700, Categoria = "C" });
        _servico.FinalizarVenda(FormaPagamento.Dinheiro, 700, "op");

        Assert.Equal(-1, _servico.ExcluirCaixaComVendas(t.Id));   // nao apaga o caixa aberto
        Assert.Single(_servico.ConsolidarTodosOsTurnos());
    }
}
