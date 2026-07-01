using PdvFesta.Core;
using Xunit;

namespace PdvFesta.Tests;

/// <summary>
/// Garante que a impressao termica sai SEM acento (impressoras baratas cospem lixo
/// em caracteres acentuados). A tela mantem acento; so o cupom e normalizado.
/// </summary>
public class ImpressaoSemAcentoTests
{
    [Theory]
    [InlineData("Pão de Queijo", "Pao de Queijo")]
    [InlineData("Açaí", "Acai")]
    [InlineData("Cachorro-Quente Especial", "Cachorro-Quente Especial")]
    [InlineData("Café com Leite", "Cafe com Leite")]
    [InlineData("Refrigerante", "Refrigerante")]
    [InlineData("Limão", "Limao")]
    [InlineData("Coração de Galinha", "Coracao de Galinha")]
    public void RemoverAcentos_TrocaAcentuadosPorAscii(string entrada, string esperado)
    {
        Assert.Equal(esperado, EscPosPrinter.RemoverAcentos(entrada));
    }

    [Fact]
    public void RemoverAcentos_NaoQuebraComVazioOuNulo()
    {
        Assert.Equal("", EscPosPrinter.RemoverAcentos(""));
        Assert.Null(EscPosPrinter.RemoverAcentos(null!));
    }

    [Fact]
    public void RemoverAcentos_ResultadoEhAsciiPuro()
    {
        var saida = EscPosPrinter.RemoverAcentos("Pãézïnho à Môda Ção");
        Assert.All(saida, ch => Assert.True(ch < 128, $"caractere nao-ASCII: '{ch}' ({(int)ch})"));
    }
}
