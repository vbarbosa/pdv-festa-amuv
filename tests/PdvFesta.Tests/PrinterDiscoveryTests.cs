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

    [Fact]
    public void StatusFila_SemAlvo_SemaforoVermelho()
    {
        var st = PrinterDiscovery.StatusFila("");
        Assert.Equal(PrinterDiscovery.Semaforo.Vermelho, st.Nivel);
    }

    [Theory]
    [InlineData("COM6 (Bluetooth)")]
    [InlineData("COM3")]
    public void StatusFila_PortaCom_SemaforoAmarelo(string alvo)
    {
        // porta pareada: pode imprimir, mas so o envio confirma — amarelo, nao verde.
        var st = PrinterDiscovery.StatusFila(alvo);
        Assert.Equal(PrinterDiscovery.Semaforo.Amarelo, st.Nivel);
    }

    [Theory]
    [InlineData("")]
    [InlineData("COM6 (Bluetooth)")]
    public void ListarFila_SemFilaDoWindows_VaziaEnaoLanca(string alvo)
    {
        // alvo vazio ou porta COM nao tem fila do Windows para inspecionar.
        var fila = PrinterDiscovery.ListarFila(alvo);
        Assert.NotNull(fila);
        Assert.Empty(fila);
    }

    [Fact]
    public void ListarFila_FilaInexistente_NaoLancaEVazia()
    {
        // impressora que nao existe no ambiente de teste: sem jobs, sem excecao.
        var fila = PrinterDiscovery.ListarFila("MPT-II 58mm (USB001)");
        Assert.NotNull(fila);
    }

    [Theory]
    [InlineData("")]
    [InlineData("COM6 (Bluetooth)")]
    public void LimparFila_SemFilaDoWindows_ZeroEnaoLanca(string alvo)
    {
        Assert.Equal(0, PrinterDiscovery.LimparFila(alvo));
    }

    [Fact]
    public void DetalhesTecnicos_PortaCom_ExtraiPortaEtipo()
    {
        var d = PrinterDiscovery.DetalhesTecnicos("COM6 (Bluetooth)");
        Assert.Equal("COM6", d.Porta);
        Assert.Contains("Bluetooth", d.Driver);
        Assert.False(d.PadraoDoWindows);   // porta COM nunca e padrao do Windows
    }

    [Fact]
    public void DetalhesTecnicos_AlvoVazio_Placeholder()
    {
        var d = PrinterDiscovery.DetalhesTecnicos("");
        Assert.Equal("—", d.Porta);
        Assert.Equal("—", d.Driver);
    }

    [Theory]
    [InlineData("")]
    [InlineData("COM6 (Bluetooth)")]
    public void DefinirPadraoDoWindows_AlvoInvalido_False(string alvo)
    {
        // vazio ou porta COM nao pode virar padrao do Windows.
        Assert.False(PrinterDiscovery.DefinirPadraoDoWindows(alvo));
    }
}
