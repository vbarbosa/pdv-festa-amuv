using PdvFesta.Core;
using Xunit;

namespace PdvFesta.Tests;

/// <summary>
/// TDD - estorno/cancelamento de venda: soft delete (status Cancelada), exclusao dos
/// totais/gaveta/Leitura Z e migracao da coluna status em bancos antigos.
/// </summary>
public class EstornoTests : IDisposable
{
    private readonly string _db;
    public EstornoTests() => _db = Path.Combine(Path.GetTempPath(), $"estorno_{Guid.NewGuid():N}.db");
    public void Dispose()
    {
        foreach (var ext in new[] { "", "-wal", "-shm" })
            if (File.Exists(_db + ext)) { try { File.Delete(_db + ext); } catch { } }
    }
    private Repositorio NovoRepo() { var r = new Repositorio(_db); r.Inicializar(); return r; }

    [Fact]
    public void Cancelar_MarcaComoCancelada_NaoApagaDoBanco()
    {
        using var repo = NovoRepo();
        long id = repo.SalvarVenda(new Venda { TotalCentavos = 1000, Forma = FormaPagamento.Dinheiro });
        repo.CancelarVenda(id);

        var vendas = repo.ListarVendas();
        Assert.Single(vendas);                        // continua no banco (auditoria)
        Assert.True(vendas[0].Cancelada);
        Assert.Equal(StatusVenda.Cancelada, vendas[0].Status);
    }

    [Fact]
    public void Consolidar_IgnoraVendasCanceladas()
    {
        var vendas = new List<Venda>
        {
            new() { TotalCentavos = 1000, Forma = FormaPagamento.Dinheiro, Status = StatusVenda.Concluida },
            new() { TotalCentavos = 3000, Forma = FormaPagamento.Dinheiro, Status = StatusVenda.Cancelada }, // fora
            new() { TotalCentavos = 500,  Forma = FormaPagamento.Pix,      Status = StatusVenda.Concluida },
        };
        var r = Caixa.Consolidar(vendas);
        Assert.Equal(1000, r.TotalDinheiroCentavos);   // a de 3000 foi cancelada
        Assert.Equal(500, r.TotalPixCentavos);
        Assert.Equal(1500, r.FaturamentoBrutoCentavos);
        Assert.Equal(2, r.QuantidadeVendas);           // conta so as validas
    }

    [Fact]
    public void TotalGaveta_DescontaVendaCanceladaEmDinheiro()
    {
        using var repo = NovoRepo();
        var t = repo.AbrirCaixa(fundoCentavos: 0, operador: "");
        var id1 = repo.SalvarVenda(new Venda { TotalCentavos = 1000, Forma = FormaPagamento.Dinheiro, CaixaId = t.Id });
        repo.SalvarVenda(new Venda { TotalCentavos = 2000, Forma = FormaPagamento.Dinheiro, CaixaId = t.Id });
        repo.CancelarVenda(id1);   // estorna a de 1000

        var resumo = Caixa.ConsolidarTurno(t, repo.ListarVendasPorCaixa(t.Id), repo.ListarMovimentos(t.Id));
        Assert.Equal(2000, resumo.Vendas.TotalDinheiroCentavos);  // so a valida
        Assert.Equal(2000, resumo.TotalGavetaCentavos);
    }

    [Fact]
    public void ContarItens_IgnoraCanceladas()
    {
        var vendas = new List<Venda>
        {
            new() { Status = StatusVenda.Concluida, Itens = { new ItemVenda { Nome = "Quentao", PrecoUnitarioCentavos = 700, Quantidade = 2 } } },
            new() { Status = StatusVenda.Cancelada, Itens = { new ItemVenda { Nome = "Quentao", PrecoUnitarioCentavos = 700, Quantidade = 5 } } },
        };
        var itens = Caixa.ContarItens(vendas);
        Assert.Equal(2, itens.Single(i => i.Nome == "Quentao").Quantidade);  // 5 cancelados nao contam
    }

    [Fact]
    public void Migracao_AdicionaColunaStatus_EmBancoAntigoSemStatus()
    {
        // schema antigo: vendas com caixa_id mas SEM status
        using (var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_db}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
CREATE TABLE vendas (id INTEGER PRIMARY KEY AUTOINCREMENT, data_hora TEXT NOT NULL,
  total_cent INTEGER NOT NULL, forma INTEGER NOT NULL, recebido_cent INTEGER NOT NULL DEFAULT 0,
  troco_cent INTEGER NOT NULL DEFAULT 0, operador TEXT NOT NULL DEFAULT '', caixa_id INTEGER NULL);
INSERT INTO vendas (data_hora, total_cent, forma) VALUES ('2026-07-01T10:00:00.0000000', 700, 0);";
            cmd.ExecuteNonQuery();
        }
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        using var repo = NovoRepo();
        var vendas = repo.ListarVendas();
        Assert.Single(vendas);
        Assert.False(vendas[0].Cancelada);           // status default = Concluida
    }
}
