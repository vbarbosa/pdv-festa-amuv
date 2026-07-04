using PdvFesta.Core;
using Xunit;

namespace PdvFesta.Tests;

/// <summary>
/// Agendamento avancado da promocao: intervalo de datas + dias da semana, combinados com a
/// janela de horario. Todos os filtros configurados devem passar ao mesmo tempo.
/// </summary>
public class AgendamentoPromocaoTests
{
    // 2026-07-04 e um SABADO; 2026-07-06 e uma SEGUNDA.
    private static readonly DateTime Sabado = new(2026, 7, 4, 19, 0, 0);
    private static readonly DateTime Segunda = new(2026, 7, 6, 19, 0, 0);

    private static Promocao Base() => new()
    {
        Descricao = "Combo", ValorDescontoCentavos = 200, Ativo = true,
        Itens = { new PromocaoItem { ProdutoId = "x", Quantidade = 1 } }
    };

    // ---------------- intervalo de datas ----------------

    [Fact]
    public void SemDatas_ValeQualquerDia()
    {
        var p = Base();
        Assert.True(p.ValidaAgora(Sabado));
        Assert.True(p.ValidaAgora(Segunda));
    }

    [Fact]
    public void IntervaloDeDatas_SoValeDentroDoPeriodo()
    {
        var p = Base();
        p.DataInicio = new DateTime(2026, 7, 4);
        p.DataFim = new DateTime(2026, 7, 5);   // sab e dom

        Assert.True(p.ValidaAgora(Sabado));                          // dentro
        Assert.False(p.ValidaAgora(Segunda));                        // fora (2 dias depois)
        Assert.False(p.ValidaAgora(new DateTime(2026, 7, 3, 12, 0, 0)));  // antes
    }

    [Fact]
    public void DataUnica_InicioIgualFim_SoValeNaqueleDia()
    {
        var p = Base();
        p.DataInicio = p.DataFim = new DateTime(2026, 7, 4);
        Assert.True(p.ValidaAgora(Sabado));
        Assert.False(p.ValidaAgora(new DateTime(2026, 7, 5, 19, 0, 0)));
    }

    [Fact]
    public void SoDataInicio_ValeDaqueleDiaEmDiante()
    {
        var p = Base();
        p.DataInicio = new DateTime(2026, 7, 5);
        Assert.False(p.ValidaAgora(Sabado));    // 04/07 < 05/07
        Assert.True(p.ValidaAgora(Segunda));    // 06/07 >= 05/07
    }

    [Fact]
    public void HoraNoLimiteDoDia_ContaPelaData_NaoPelaHora()
    {
        var p = Base();
        p.DataFim = new DateTime(2026, 7, 4);
        // 23:59 do dia 04 ainda conta como dentro (compara so a DATA)
        Assert.True(p.ValidaAgora(new DateTime(2026, 7, 4, 23, 59, 0)));
        Assert.False(p.ValidaAgora(new DateTime(2026, 7, 5, 0, 1, 0)));
    }

    // ---------------- dias da semana ----------------

    [Fact]
    public void DiasDaSemana_SoValeNosDiasMarcados()
    {
        var p = Base();
        p.Dias = DiasSemana.Sabado | DiasSemana.Domingo;   // so fim de semana
        Assert.True(p.ValidaAgora(Sabado));
        Assert.False(p.ValidaAgora(Segunda));
    }

    [Fact]
    public void DiasTodos_ValeTodoDia()
    {
        var p = Base();
        p.Dias = DiasSemana.Todos;
        Assert.True(p.ValidaAgora(Sabado));
        Assert.True(p.ValidaAgora(Segunda));
    }

    // ---------------- combinacoes ----------------

    [Fact]
    public void Combinacao_Data_Dia_Hora_TodosDevemPassar()
    {
        var p = Base();
        p.DataInicio = new DateTime(2026, 7, 1);
        p.DataFim = new DateTime(2026, 7, 31);
        p.Dias = DiasSemana.Sabado;
        p.HoraInicio = new TimeSpan(18, 0, 0);
        p.HoraFim = new TimeSpan(23, 0, 0);

        Assert.True(p.ValidaAgora(new DateTime(2026, 7, 4, 19, 0, 0)));   // sab, julho, 19h -> OK
        Assert.False(p.ValidaAgora(new DateTime(2026, 7, 4, 12, 0, 0)));  // sab, julho, 12h -> fora da hora
        Assert.False(p.ValidaAgora(new DateTime(2026, 7, 6, 19, 0, 0)));  // seg -> fora do dia
        Assert.False(p.ValidaAgora(new DateTime(2026, 8, 1, 19, 0, 0)));  // agosto -> fora da data
    }

    [Fact]
    public void Inativa_NuncaVale_MesmoNoAgendamento()
    {
        var p = Base();
        p.Ativo = false;
        p.Dias = DiasSemana.Sabado;
        Assert.False(p.ValidaAgora(Sabado));
    }

    // ---------------- persistencia ----------------

    [Fact]
    public void Persiste_DatasEDias_NoBanco()
    {
        var db = Path.Combine(Path.GetTempPath(), $"promo_{Guid.NewGuid():N}.db");
        try
        {
            using var repo = new Repositorio(db);
            repo.Inicializar();
            var p = Base();
            p.DataInicio = new DateTime(2026, 7, 4);
            p.DataFim = new DateTime(2026, 7, 6);
            p.Dias = DiasSemana.Sexta | DiasSemana.Sabado;
            var id = repo.SalvarPromocao(p);

            var lida = repo.ListarPromocoes(incluirInativas: true).First(x => x.Id == id);
            Assert.Equal(new DateTime(2026, 7, 4), lida.DataInicio);
            Assert.Equal(new DateTime(2026, 7, 6), lida.DataFim);
            Assert.Equal(DiasSemana.Sexta | DiasSemana.Sabado, lida.Dias);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var ext in new[] { "", "-wal", "-shm" })
                if (File.Exists(db + ext)) { try { File.Delete(db + ext); } catch { } }
        }
    }

    [Fact]
    public void JsonSeed_ParsearDias_ETextoDeData()
    {
        var seed = new PromocaoSeed
        {
            Descricao = "Fim de semana", Tipo = "Combo", DescontoCentavos = 200,
            DataInicio = "2026-07-04", DataFim = "2026-07-05", Dias = "Sabado,Domingo",
            Itens = { new PromocaoItem { ProdutoId = "x", Quantidade = 1 } }
        };
        var p = seed.ParaPromocao();
        Assert.Equal(new DateTime(2026, 7, 4), p.DataInicio);
        Assert.Equal(new DateTime(2026, 7, 5), p.DataFim);
        Assert.Equal(DiasSemana.Sabado | DiasSemana.Domingo, p.Dias);
    }

    [Fact]
    public void JsonSeed_SemDias_ViraTodos()
    {
        var seed = new PromocaoSeed { Descricao = "X", Tipo = "Combo", DescontoCentavos = 100 };
        Assert.Equal(DiasSemana.Todos, seed.ParaPromocao().Dias);
    }
}
