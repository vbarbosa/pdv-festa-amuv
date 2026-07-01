using PdvFesta.Core;
using Xunit;

namespace PdvFesta.Tests;

/// <summary>
/// TDD - os DOIS modos de cupom: Recibo Completo e Ficha de Consumo (fonte expandida).
/// Regra dura: linha expandida tem largura util de 16 colunas (fonte dupla).
/// </summary>
public class TicketTests
{
    private static Venda VendaExemplo() => new()
    {
        Id = 7,
        TotalCentavos = 1700,
        Forma = FormaPagamento.Dinheiro,
        RecebidoCentavos = 2000,
        TrocoCentavos = 300,
        Itens =
        {
            new ItemVenda { Nome = "Quentao", PrecoUnitarioCentavos = 700, Quantidade = 1 },
            new ItemVenda { Nome = "Bolo de Milho", PrecoUnitarioCentavos = 500, Quantidade = 2 },
        }
    };

    [Fact]
    public void Completo_TemTotalEPagamento_ELinhasDentroDe32()
    {
        var cfg = new ConfigCupom { Evento = "ARRAIA", Modo = ModoCupom.Completo };
        var linhas = CupomFormatter.MontarTicket(VendaExemplo(), cfg);

        Assert.Contains(linhas, l => l.Texto.StartsWith("TOTAL"));
        Assert.Contains(linhas, l => l.Texto.Contains("DINHEIRO"));
        // linhas normais nunca passam de 32 colunas
        Assert.All(linhas.Where(l => l.Estilo == EstiloLinha.Normal),
            l => Assert.True(l.Texto.Length <= 32, $"'{l.Texto}' = {l.Texto.Length} col"));
    }

    [Fact]
    public void Ficha_OcultaValores_UsaFonteExpandida_Max16Colunas()
    {
        var cfg = new ConfigCupom { Modo = ModoCupom.FichaConsumo };
        var linhas = CupomFormatter.MontarTicket(VendaExemplo(), cfg);

        // nenhuma linha mostra "R$" (ficha economica nao imprime valores)
        Assert.DoesNotContain(linhas, l => l.Texto.Contains("R$"));
        // deve existir a ficha do bolo com "2X"
        Assert.Contains(linhas, l => l.Estilo == EstiloLinha.Expandida && l.Texto.Contains("2X"));
        // toda linha expandida respeita as 16 colunas da fonte dupla
        Assert.All(linhas.Where(l => l.Estilo == EstiloLinha.Expandida),
            l => Assert.True(l.Texto.Length <= CupomFormatter.LarguraExpandida,
                $"'{l.Texto}' = {l.Texto.Length} col (max {CupomFormatter.LarguraExpandida})"));
    }

    [Fact]
    public void Ficha_SepararPorItem_InsereCorteEntreItens()
    {
        var cfg = new ConfigCupom { Modo = ModoCupom.FichaConsumo, SepararPorItem = true };
        var linhas = CupomFormatter.MontarTicket(VendaExemplo(), cfg);
        // 2 itens -> 1 corte entre eles
        Assert.Equal(1, linhas.Count(l => l.Estilo == EstiloLinha.Corte));
    }

    [Fact]
    public void Ficha_SemSeparar_NaoInsereCorteDeFicha()
    {
        var cfg = new ConfigCupom { Modo = ModoCupom.FichaConsumo, SepararPorItem = false };
        var linhas = CupomFormatter.MontarTicket(VendaExemplo(), cfg);
        Assert.DoesNotContain(linhas, l => l.Estilo == EstiloLinha.Corte);
    }

    [Fact]
    public void NomeExpandidoLongo_QuebraSemUltrapassar16()
    {
        var venda = new Venda
        {
            Itens = { new ItemVenda { Nome = "Cachorro-Quente Especial", PrecoUnitarioCentavos = 1000, Quantidade = 1 } }
        };
        var cfg = new ConfigCupom { Modo = ModoCupom.FichaConsumo };
        var linhas = CupomFormatter.MontarTicket(venda, cfg);
        Assert.All(linhas.Where(l => l.Estilo == EstiloLinha.Expandida),
            l => Assert.True(l.Texto.Length <= 16));
    }

    [Fact]
    public void Completo_SempreMostraQuantidade_ComPrefixoNx_Alinhado()
    {
        var venda = new Venda
        {
            TotalCentavos = 1700, Forma = FormaPagamento.Pix,
            Itens =
            {
                new ItemVenda { Nome = "Pipoca", PrecoUnitarioCentavos = 500, Quantidade = 2 },
                new ItemVenda { Nome = "Quentao", PrecoUnitarioCentavos = 700, Quantidade = 1 },
            }
        };
        var linhas = CupomFormatter.MontarTicket(venda, new ConfigCupom { Modo = ModoCupom.Completo });
        // qtd SEMPRE presente como prefixo, inclusive para 1 unidade
        Assert.Contains(linhas, l => l.Texto.StartsWith("2x Pipoca"));
        Assert.Contains(linhas, l => l.Texto.StartsWith("1x Quentao"));
        // e a linha do item (esquerda) + preco (direita) fica alinhada em 32 col
        var linhaPipoca = linhas.First(l => l.Texto.StartsWith("2x Pipoca"));
        Assert.Equal(32, linhaPipoca.Texto.Length);
        Assert.EndsWith("R$ 10,00", linhaPipoca.Texto);
    }

    [Fact]
    public void Completo_LinhasImportantesNuncaQuebram_MesmoComNomeLongo()
    {
        // nome gigante deve ser TRUNCADO, nunca gerar linha > 32 (que a termica quebraria)
        var venda = new Venda
        {
            TotalCentavos = 10000, Forma = FormaPagamento.Dinheiro, RecebidoCentavos = 10000, TrocoCentavos = 0,
            Itens = { new ItemVenda { Nome = "Cachorro-Quente Especial da Casa com Bacon, Batata e Cheddar",
                                      PrecoUnitarioCentavos = 10000, Quantidade = 1 } }
        };
        var linhas = CupomFormatter.MontarTicket(venda, new ConfigCupom { Modo = ModoCupom.Completo });
        // toda linha nao-titulo (itens, TOTAL, Pagamento, Troco, divisorias) cabe em 32 col
        Assert.All(linhas.Where(l => l.Estilo != EstiloLinha.Titulo),
            l => Assert.True(l.Texto.Length <= 32, $"linha vazaria: '{l.Texto}' = {l.Texto.Length} col"));
    }

    [Fact]
    public void ConfigCupom_Persiste_NoBanco()
    {
        var db = Path.Combine(Path.GetTempPath(), $"cupom_{Guid.NewGuid():N}.db");
        try
        {
            using var repo = new Repositorio(db);
            repo.Inicializar();
            new ConfigCupom
            {
                Evento = "Festa Junina Familia", Subtitulo = "Caixa 01",
                Modo = ModoCupom.FichaConsumo, SepararPorItem = true, Rodape = "Volte sempre"
            }.Salvar(repo);

            var lido = ConfigCupom.Ler(repo);
            Assert.Equal("Festa Junina Familia", lido.Evento);
            Assert.Equal("Caixa 01", lido.Subtitulo);
            Assert.Equal(ModoCupom.FichaConsumo, lido.Modo);
            Assert.True(lido.SepararPorItem);
            Assert.Equal("Volte sempre", lido.Rodape);
        }
        finally
        {
            foreach (var ext in new[] { "", "-wal", "-shm" })
                if (File.Exists(db + ext)) { try { File.Delete(db + ext); } catch { } }
        }
    }
}
