using PdvFesta.Core;
using Xunit;

namespace PdvFesta.Tests;

/// <summary>TDD - resolucao do diretorio de dados com fallback.</summary>
public class AppPathsTests
{
    [Fact]
    public void DiretorioDados_RetornaCaminhoGravavel()
    {
        var dir = AppPaths.DiretorioDados();
        Assert.False(string.IsNullOrWhiteSpace(dir));
        Assert.True(AppPaths.TentarCriarEEscrever(dir), "diretorio retornado deve ser gravavel");
    }

    [Fact]
    public void CaminhoBanco_TerminaComNomeDoBanco()
    {
        var caminho = AppPaths.CaminhoBanco();
        Assert.EndsWith(AppPaths.NomeBanco, caminho);
    }

    [Fact]
    public void TentarCriarEEscrever_PastaValida_True()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"paths_{Guid.NewGuid():N}");
        try
        {
            Assert.True(AppPaths.TentarCriarEEscrever(tmp));
            Assert.True(Directory.Exists(tmp));
        }
        finally { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); }
    }

    [Fact]
    public void TentarCriarEEscrever_CaminhoInvalido_False()
    {
        // caractere invalido no Windows -> nao cria
        Assert.False(AppPaths.TentarCriarEEscrever("Z:\\<>|caminho:invalido"));
    }
}
