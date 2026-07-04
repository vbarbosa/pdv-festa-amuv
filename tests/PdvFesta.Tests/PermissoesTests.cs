using PdvFesta.App;
using Xunit;

namespace PdvFesta.Tests;

public class PermissoesTests : IDisposable
{
    private readonly string _db;
    private readonly Servico _servico;
    private readonly Permissoes _perm;

    public PermissoesTests()
    {
        _db = Path.Combine(Path.GetTempPath(), $"perm_{Guid.NewGuid():N}.db");
        _servico = new Servico(_db, cardapioPath: "___inexistente___.json");
        _perm = new Permissoes(_servico);
    }

    public void Dispose()
    {
        _servico.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var ext in new[] { "", "-wal", "-shm" })
            if (File.Exists(_db + ext)) { try { File.Delete(_db + ext); } catch { } }
    }

    [Theory]
    [InlineData(AcaoProtegida.FecharCaixa, true)]
    [InlineData(AcaoProtegida.GerenciarProdutos, true)]
    [InlineData(AcaoProtegida.EstornarVenda, true)]
    [InlineData(AcaoProtegida.Backup, true)]
    [InlineData(AcaoProtegida.SangriaSuprimento, true)]
    [InlineData(AcaoProtegida.ExportarCsv, true)]
    public void Default_AcoesCriticas_ExigemSenha(AcaoProtegida acao, bool esperado)
    {
        Assert.Equal(esperado, Permissoes.PadraoExigeSenha(acao));
        Assert.Equal(esperado, _perm.ExigeSenha(acao));
    }

    [Fact]
    public void ExportarCsv_Configuravel_PodeSerLiberada()
    {
        Assert.True(_perm.ExigeSenha(AcaoProtegida.ExportarCsv));   // padrao exige
        _perm.Definir(AcaoProtegida.ExportarCsv, false);
        Assert.False(_perm.ExigeSenha(AcaoProtegida.ExportarCsv));  // admin pode liberar
    }

    [Theory]
    [InlineData(AcaoProtegida.GerenciarCategorias)]
    [InlineData(AcaoProtegida.GerenciarPromocoes)]
    [InlineData(AcaoProtegida.ConfigImpressora)]
    [InlineData(AcaoProtegida.LayoutCupom)]
    public void Default_ConfigLeve_Liberada(AcaoProtegida acao)
    {
        Assert.False(Permissoes.PadraoExigeSenha(acao));
        Assert.False(_perm.ExigeSenha(acao));
    }

    [Fact]
    public void TelaPermissoes_SempreExigeSenha_MesmoSeDesligada()
    {
        _perm.Definir(AcaoProtegida.Permissoes, false);   // tenta desligar
        Assert.True(_perm.ExigeSenha(AcaoProtegida.Permissoes));  // ignora: sempre exige
    }

    [Fact]
    public void Definir_PersisteAEscolha()
    {
        // liga a exigencia de senha numa acao que por padrao e liberada
        _perm.Definir(AcaoProtegida.LayoutCupom, true);
        Assert.True(_perm.ExigeSenha(AcaoProtegida.LayoutCupom));

        // e desliga numa que por padrao exige
        _perm.Definir(AcaoProtegida.Backup, false);
        Assert.False(_perm.ExigeSenha(AcaoProtegida.Backup));
    }

    [Fact]
    public void Definir_Sobrevive_AReabrirOServico()
    {
        _perm.Definir(AcaoProtegida.GerenciarProdutos, false);
        _servico.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        using var servico2 = new Servico(_db, "___inexistente___.json");
        var perm2 = new Permissoes(servico2);
        Assert.False(perm2.ExigeSenha(AcaoProtegida.GerenciarProdutos));
    }

    [Fact]
    public void Rotulo_NaoVazio_ParaTodasAsAcoes()
    {
        foreach (AcaoProtegida a in Enum.GetValues(typeof(AcaoProtegida)))
            Assert.False(string.IsNullOrWhiteSpace(Permissoes.Rotulo(a)));
    }
}
