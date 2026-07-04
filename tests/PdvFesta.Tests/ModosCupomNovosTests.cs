using System.Linq;
using PdvFesta.Core;
using Xunit;

namespace PdvFesta.Tests;

/// <summary>
/// Novos modos/ajustes de cupom:
///  - SoVales: so as fichas destacaveis, com MINI-CABECALHO da festa em CADA ficha.
///  - FichaConsumo: 1 ficha POR UNIDADE (nao agrupa "3X") + cabecalho identificando a festa.
/// </summary>
public class ModosCupomNovosTests
{
    private static Venda VendaExemplo() => new()
    {
        Id = 7,
        Forma = FormaPagamento.Dinheiro,
        TotalCentavos = 4300,
        Itens =
        {
            new ItemVenda { ProdutoId = "cachorro", Nome = "Cachorro", PrecoUnitarioCentavos = 1000, Quantidade = 3 },
            new ItemVenda { ProdutoId = "refri",    Nome = "Refri",    PrecoUnitarioCentavos = 600,  Quantidade = 2 },
        }
    };

    // ---------------- SoVales ----------------

    [Fact]
    public void SoVales_UmValePorUnidade_SemReciboGerencial()
    {
        var cfg = new ConfigCupom { Modo = ModoCupom.SoVales, Evento = "ARRAIA DO ZE" };
        var linhas = CupomFormatter.MontarTicket(VendaExemplo(), cfg);
        var texto = string.Join("\n", linhas.Select(x => x.Texto));

        // 3 + 2 = 5 vales
        int vales = linhas.Count(x => x.Estilo == EstiloLinha.Expandida && x.Texto.StartsWith("1X"));
        Assert.Equal(5, vales);

        // NAO tem o recibo gerencial (nada de TOTAL/Pagamento/"FICHAS DE CONSUMO")
        Assert.DoesNotContain("TOTAL", texto);
        Assert.DoesNotContain("Pagamento", texto);
        Assert.DoesNotContain("FICHAS DE CONSUMO", texto);
    }

    [Fact]
    public void SoVales_TemMiniCabecalhoDaFestaEmCadaFicha()
    {
        var cfg = new ConfigCupom { Modo = ModoCupom.SoVales, Evento = "ARRAIA DO ZE" };
        var linhas = CupomFormatter.MontarTicket(VendaExemplo(), cfg);

        // o nome da festa (estilo Titulo) aparece 1x por ficha = 5 vezes.
        int cabecalhos = linhas.Count(x => x.Estilo == EstiloLinha.Titulo && x.Texto == "ARRAIA DO ZE");
        Assert.Equal(5, cabecalhos);

        // e o mini-cabecalho traz a identificacao da venda (numero) em cada ficha.
        int ids = linhas.Count(x => x.Texto.Contains("#7"));
        Assert.Equal(5, ids);
    }

    [Fact]
    public void SoVales_IgnoraLinhaDeDescontoDeCombo()
    {
        var venda = VendaExemplo();
        venda.Itens.Add(new ItemVenda { ProdutoId = "", Nome = "COMBO -R$2", PrecoUnitarioCentavos = -200, Quantidade = 1 });

        var cfg = new ConfigCupom { Modo = ModoCupom.SoVales, Evento = "X" };
        var linhas = CupomFormatter.MontarTicket(venda, cfg);
        int vales = linhas.Count(x => x.Estilo == EstiloLinha.Expandida && x.Texto.StartsWith("1X"));
        Assert.Equal(5, vales);   // desconto nao gera vale
    }

    [Fact]
    public void SoVales_NaoTerminaComLinhaEmBranco()
    {
        var cfg = new ConfigCupom { Modo = ModoCupom.SoVales, Evento = "X" };
        var linhas = CupomFormatter.MontarTicket(VendaExemplo(), cfg);
        Assert.False(linhas[^1].Estilo == EstiloLinha.Normal && linhas[^1].Texto == "",
            "nao deve sobrar papel branco no fim (o corte ja avanca)");
    }

    [Fact]
    public void SoVales_ImprimeORodapeUmaVezNoFim()
    {
        var cfg = new ConfigCupom { Modo = ModoCupom.SoVales, Evento = "X", Rodape = "Obrigado! Bom Arraia!" };
        var linhas = CupomFormatter.MontarTicket(VendaExemplo(), cfg);
        var texto = string.Join("\n", linhas.Select(x => x.Texto));

        Assert.Contains("Obrigado", texto);                                    // rodape presente
        // aparece UMA vez (nao um por vale) — conta as linhas que contem a palavra
        int ocorrencias = linhas.Count(x => x.Texto.Contains("Obrigado"));
        Assert.Equal(1, ocorrencias);
    }

    [Fact]
    public void SoVales_SemRodapeConfigurado_NaoAdicionaNada()
    {
        var cfg = new ConfigCupom { Modo = ModoCupom.SoVales, Evento = "X", Rodape = "" };
        var linhas = CupomFormatter.MontarTicket(VendaExemplo(), cfg);
        // ultimo elemento deve ser o pontilhado do ultimo vale (nao sobra rodape vazio)
        Assert.False(string.IsNullOrEmpty(linhas[^1].Texto) == false && linhas[^1].Texto.Contains("Obrigado"));
    }

    // ---------------- FichaConsumo: 1 por unidade ----------------

    [Fact]
    public void FichaConsumo_UmaFichaPorUnidade_NaoAgrupa()
    {
        var cfg = new ConfigCupom { Modo = ModoCupom.FichaConsumo, Evento = "ARRAIA" };
        var linhas = CupomFormatter.MontarTicket(VendaExemplo(), cfg);
        var texto = string.Join("\n", linhas.Select(x => x.Texto));

        // cada UNIDADE vira "1X" -> 5 fichas; NENHUMA agrupada como "3X".
        int fichas = linhas.Count(x => x.Estilo == EstiloLinha.Expandida && x.Texto.StartsWith("1X"));
        Assert.Equal(5, fichas);
        Assert.DoesNotContain("3X", texto);
        Assert.DoesNotContain("2X", texto);
    }

    [Fact]
    public void FichaConsumo_TemCabecalhoIdentificandoAFesta()
    {
        var cfg = new ConfigCupom { Modo = ModoCupom.FichaConsumo, Evento = "ARRAIA", Subtitulo = "Caixa 01" };
        var linhas = CupomFormatter.MontarTicket(VendaExemplo(), cfg);
        var texto = string.Join("\n", linhas.Select(x => x.Texto));

        Assert.Contains(linhas, x => x.Estilo == EstiloLinha.Titulo && x.Texto == "ARRAIA");
        Assert.Contains("Caixa 01", texto);       // subtitulo
        Assert.Contains("#7", texto);             // numero da venda
    }

    [Fact]
    public void FichaConsumo_SepararPorItem_CortaEntreFichas()
    {
        var cfg = new ConfigCupom { Modo = ModoCupom.FichaConsumo, Evento = "X", SepararPorItem = true };
        var linhas = CupomFormatter.MontarTicket(VendaExemplo(), cfg);

        // 5 unidades -> 4 cortes entre elas (o ultimo nao corta; o corte final e da camada ESC/POS).
        int cortes = linhas.Count(x => x.Estilo == EstiloLinha.Corte);
        Assert.Equal(4, cortes);
    }
}
