using PdvFesta.Core;
using Xunit;

namespace PdvFesta.Tests;

public class PrinterDiscoveryTests
{
    [Theory]
    [InlineData("MPT-II 58mm", true)]
    [InlineData("POS58 Printer", true)]
    [InlineData("Thermal Receipt 80mm", true)]
    [InlineData("ZJiang ZJ-58", true)]
    [InlineData("Generic 58 Printer", true)]
    [InlineData("Microsoft Print to PDF", false)]
    [InlineData("HP DeskJet 2700", false)]
    [InlineData("OneNote (Desktop)", false)]
    public void PareceTermica_ReconheceTermicas(string nome, bool esperado)
    {
        Assert.Equal(esperado, PrinterDiscovery.PareceTermica(nome));
    }

    [Theory]
    [InlineData("COM6 (Bluetooth)")]
    [InlineData("COM3")]
    [InlineData("com7 (serial)")]
    public void EstaOnline_PortasCom_SempreOnline(string alvo)
    {
        // portas serial/Bluetooth nao tem fila do Windows: assume online (best-effort).
        Assert.True(PrinterDiscovery.EstaOnline(alvo));
    }

    [Fact]
    public void EstaOnline_AlvoVazio_False()
    {
        Assert.False(PrinterDiscovery.EstaOnline(""));
        Assert.False(PrinterDiscovery.EstaOnline("   "));
    }

    [Fact]
    public void ListarImpressoras_NaoLancaEnaoNulo()
    {
        // no ambiente de teste pode nao haver impressora; so garante robustez (sem excecao).
        var lista = PrinterDiscovery.ListarImpressoras();
        Assert.NotNull(lista);
    }

    [Fact]
    public void ListarPortasCom_RotulaBluetoothOuSerial()
    {
        var portas = PrinterDiscovery.ListarPortasCom();
        Assert.NotNull(portas);
        // toda porta listada deve vir rotulada (Bluetooth) ou (serial).
        Assert.All(portas, p => Assert.True(
            p.Contains("(Bluetooth)") || p.Contains("(serial)"),
            $"porta sem rotulo: {p}"));
    }

    [Fact]
    public void StatusFila_AlvoVazio_NaoOnlineComDescricao()
    {
        var st = PrinterDiscovery.StatusFila("");
        Assert.False(st.PareceOnline);
        Assert.False(st.ConfirmavelSoImprimindo);
        Assert.False(string.IsNullOrWhiteSpace(st.Descricao));
    }

    [Theory]
    [InlineData("COM6 (Bluetooth)")]
    [InlineData("COM3")]
    public void StatusFila_PortaCom_OnlineEConfirmavel(string alvo)
    {
        var st = PrinterDiscovery.StatusFila(alvo);
        Assert.True(st.PareceOnline);
        Assert.True(st.ConfirmavelSoImprimindo);   // porta pareada: envio confirma
    }

    [Fact]
    public void StatusFila_FilaUsb_NuncaMenteProntaSemImprimir()
    {
        // para uma fila USB (mesmo inexistente no ambiente de teste), a descricao NAO pode
        // afirmar "PRONTA/online" categoricamente — o status USB so e confirmado ao imprimir.
        var st = PrinterDiscovery.StatusFila("MPT-II 58mm (USB001)");
        Assert.DoesNotContain("PRONTA", st.Descricao, System.StringComparison.OrdinalIgnoreCase);
        // se parece online, tem que ser explicitamente "confirmavel so imprimindo".
        if (st.PareceOnline) Assert.True(st.ConfirmavelSoImprimindo);
    }
}
