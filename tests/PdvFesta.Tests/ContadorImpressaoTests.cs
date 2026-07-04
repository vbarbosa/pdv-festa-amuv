using PdvFesta.App;
using PdvFesta.Core;
using Xunit;

namespace PdvFesta.Tests;

/// <summary>
/// Contador de impressoes por venda + regra de seguranca: venda CANCELADA nao reimprime.
/// Usa o Servico com banco real (PDV_DEMO liga o "imprimiu OK" sem hardware) para exercitar
/// o caminho de ponta a ponta (RegistrarImpressao no banco).
/// </summary>
public class ContadorImpressaoTests : IDisposable
{
    private readonly string _db;
    private readonly Servico _servico;

    public ContadorImpressaoTests()
    {
        _db = Path.Combine(Path.GetTempPath(), $"impr_{Guid.NewGuid():N}.db");
        _servico = new Servico(_db, "___inexistente___.json");
        _servico.ImpressaoSimulada = true;   // flag de INSTANCIA: finge imprimir, mas AINDA conta (sem tocar impressora)
        _servico.AbrirCaixa(0, "op");
    }

    public void Dispose()
    {
        _servico.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var ext in new[] { "", "-wal", "-shm" })
            if (File.Exists(_db + ext)) { try { File.Delete(_db + ext); } catch { } }
    }

    private Venda Vender()
    {
        var prod = new Produto { Id = "q", Nome = "Quentao", PrecoCentavos = 700, Categoria = "C" };
        _servico.Carrinho.Adicionar(prod);
        var (venda, _, _) = _servico.FinalizarVenda(FormaPagamento.Dinheiro, 700, "op");
        return venda;
    }

    [Fact]
    public void PrimeiraVia_ContaComoUmaImpressao()
    {
        var venda = Vender();
        Assert.Equal(1, venda.Impressoes);

        // e persiste no banco
        var doBanco = _servico.Repo.ListarVendas().First(v => v.Id == venda.Id);
        Assert.Equal(1, doBanco.Impressoes);
    }

    [Fact]
    public void Reimprimir_IncrementaOContador()
    {
        var venda = Vender();                 // 1a via -> 1
        _servico.ImprimirVenda(venda);        // reimpressao -> 2
        _servico.ImprimirVenda(venda);        // reimpressao -> 3
        Assert.Equal(3, venda.Impressoes);

        var doBanco = _servico.Repo.ListarVendas().First(v => v.Id == venda.Id);
        Assert.Equal(3, doBanco.Impressoes);
    }

    [Fact]
    public void VendaCancelada_NaoReimprime_ENaoIncrementa()
    {
        var venda = Vender();                 // 1a via -> 1
        _servico.CancelarVenda(venda.Id);

        // recarrega a venda ja cancelada e tenta reimprimir
        var cancelada = _servico.Repo.ListarVendas().First(v => v.Id == venda.Id);
        Assert.True(cancelada.Cancelada);

        var (ok, msg) = _servico.ImprimirVenda(cancelada);
        Assert.False(ok);
        Assert.Contains("CANCELADA", msg, StringComparison.OrdinalIgnoreCase);

        // contador NAO mudou (segue 1 da 1a via, antes do cancelamento)
        var doBanco = _servico.Repo.ListarVendas().First(v => v.Id == venda.Id);
        Assert.Equal(1, doBanco.Impressoes);
    }

    [Fact]
    public void RegistrarImpressao_Persiste_ERetornaNovoTotal()
    {
        var venda = Vender();                 // ja em 1
        int total = _servico.Repo.RegistrarImpressao(venda.Id);
        Assert.Equal(2, total);
    }
}
