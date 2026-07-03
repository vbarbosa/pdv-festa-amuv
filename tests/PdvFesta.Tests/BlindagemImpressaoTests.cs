using System.Diagnostics;
using PdvFesta.Core;
using Xunit;

namespace PdvFesta.Tests;

/// <summary>
/// BLINDAGEM da impressao: garante que o app NUNCA e derrubado nem congelado por uma
/// impressora com problema (cabo solto, fila travada, firmware embolado). Regras testadas
/// SEM depender de hardware — so a robustez do contrato de EnviarRaw.
///
/// Contexto real: um comando de corte incompativel (GS V 66 n) travou a MPT-II em campo,
/// e o job "Error | Printing" agarrou no spooler e bloqueou a fila. As protecoes: teto de
/// tempo (15s), nunca lançar, e retorno (false, msg) que deixa o caixa seguir.
/// </summary>
public class BlindagemImpressaoTests
{
    [Fact]
    public void EnviarRaw_AlvoVazio_RetornaFalseSemLancar()
    {
        var (ok, msg) = EscPosPrinter.EnviarRaw("", new byte[] { 0x1B, 0x40 });
        Assert.False(ok);
        Assert.False(string.IsNullOrWhiteSpace(msg));
    }

    [Fact]
    public void EnviarRaw_ImpressoraInexistente_NaoLancaERetornaErro()
    {
        // impressora que nao existe: o contrato e (false, msg) — nunca excecao.
        var ex = Record.Exception(() =>
        {
            var (ok, _) = EscPosPrinter.EnviarRaw("___NAO_EXISTE___ 58mm (USBZZZ)", new byte[] { 0x1B, 0x40, 0x0A });
            Assert.False(ok);   // sem hardware, tem que falhar graciosamente
        });
        Assert.Null(ex);        // e sem derrubar nada
    }

    [Fact]
    public void EnviarRaw_RespeitaTetoDeTempo_NaoCongelaOcaixa()
    {
        // mesmo no pior caso (alvo estranho que forca caminhos de erro), a chamada tem que
        // RETORNAR rapido — nunca pendurar o caixa. Teto de projeto: 15s; folga generosa aqui.
        var sw = Stopwatch.StartNew();
        var (ok, _) = EscPosPrinter.EnviarRaw("Fila\\Invalida:Que*Nao?Abre", new byte[] { 0x1B, 0x40 });
        sw.Stop();

        Assert.False(ok);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(20),
            $"EnviarRaw demorou {sw.Elapsed.TotalSeconds:0.0}s — deveria respeitar o teto e nao congelar o caixa.");
    }

    [Fact]
    public void EnviarRaw_PayloadVazio_NaoLanca()
    {
        var ex = Record.Exception(() => EscPosPrinter.EnviarRaw("Qualquer 58mm", System.Array.Empty<byte>()));
        Assert.Null(ex);
    }

    [Fact]
    public void ImprimirTeste_ImpressoraInexistente_FalhaGraciosa()
    {
        // o teste da tela tambem nao pode derrubar o app quando nao ha impressora real.
        var ex = Record.Exception(() =>
        {
            var (ok, msg) = EscPosPrinter.ImprimirTeste("___SEM_IMPRESSORA___");
            Assert.False(ok);
            Assert.False(string.IsNullOrWhiteSpace(msg));
        });
        Assert.Null(ex);
    }
}
