using PdvFesta.Core;
using Xunit;

namespace PdvFesta.Tests;

/// <summary>
/// Gerenciamento de backups: listar (mais novo primeiro), tamanho legivel, mais recente,
/// e limpar antigos mantendo os N mais novos. Sem hardware — so arquivos numa pasta temp.
/// </summary>
public class BackupGerenciamentoTests : IDisposable
{
    private readonly string _dir;

    public BackupGerenciamentoTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"bkptest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    /// <summary>Cria um backup falso com data de escrita controlada.</summary>
    private string Criar(string nome, DateTime data, int bytes = 1024)
    {
        var caminho = Path.Combine(_dir, nome);
        File.WriteAllBytes(caminho, new byte[bytes]);
        File.SetLastWriteTime(caminho, data);
        return caminho;
    }

    [Fact]
    public void Listar_PastaInexistente_Vazia()
    {
        Assert.Empty(BackupManager.Listar(Path.Combine(_dir, "nao_existe")));
        Assert.Empty(BackupManager.Listar(""));
    }

    [Fact]
    public void Listar_OrdenaDoMaisNovoParaOMaisAntigo()
    {
        Criar("backup_pdv_20260701_100000.zip", new DateTime(2026, 7, 1, 10, 0, 0));
        Criar("backup_pdv_20260703_100000.zip", new DateTime(2026, 7, 3, 10, 0, 0));
        Criar("backup_pdv_20260702_100000.zip", new DateTime(2026, 7, 2, 10, 0, 0));

        var lista = BackupManager.Listar(_dir);
        Assert.Equal(3, lista.Count);
        Assert.Equal(new DateTime(2026, 7, 3, 10, 0, 0), lista[0].Data);   // mais novo primeiro
        Assert.Equal(new DateTime(2026, 7, 1, 10, 0, 0), lista[2].Data);   // mais antigo por ultimo
    }

    [Fact]
    public void Listar_IgnoraArquivosQueNaoSaoBackup()
    {
        Criar("backup_pdv_20260701_100000.zip", new DateTime(2026, 7, 1));
        Criar("outra_coisa.txt", new DateTime(2026, 7, 1));
        Criar("cardapio_2026.json", new DateTime(2026, 7, 1));

        var lista = BackupManager.Listar(_dir);
        Assert.Single(lista);
        Assert.StartsWith("backup_pdv_", lista[0].Nome);
    }

    [Fact]
    public void Listar_AceitaDbEZip()
    {
        Criar("backup_pdv_20260701_100000.zip", new DateTime(2026, 7, 1));
        Criar("backup_pdv_20260702_100000.db", new DateTime(2026, 7, 2));
        Assert.Equal(2, BackupManager.Listar(_dir).Count);
    }

    [Fact]
    public void TamanhoLegivel_FormataKbEMb()
    {
        var kb = Criar("backup_pdv_20260701_100000.zip", new DateTime(2026, 7, 1), bytes: 300 * 1024);
        var mb = Criar("backup_pdv_20260702_100000.zip", new DateTime(2026, 7, 2), bytes: 2 * 1024 * 1024);

        var lista = BackupManager.Listar(_dir);
        Assert.Contains(lista, b => b.TamanhoLegivel.Contains("MB"));
        Assert.Contains(lista, b => b.TamanhoLegivel.Contains("KB"));
    }

    [Fact]
    public void MaisRecente_RetornaOTopo_OuNull()
    {
        Assert.Null(BackupManager.MaisRecente(_dir));
        Criar("backup_pdv_20260705_120000.zip", new DateTime(2026, 7, 5, 12, 0, 0));
        Criar("backup_pdv_20260701_120000.zip", new DateTime(2026, 7, 1, 12, 0, 0));
        var r = BackupManager.MaisRecente(_dir);
        Assert.NotNull(r);
        Assert.Equal(new DateTime(2026, 7, 5, 12, 0, 0), r!.Value.Data);
    }

    [Fact]
    public void LimparAntigos_MantemApenasOsNMaisRecentes()
    {
        for (int d = 1; d <= 5; d++)
            Criar($"backup_pdv_2026070{d}_100000.zip", new DateTime(2026, 7, d, 10, 0, 0));

        int apagados = BackupManager.LimparAntigos(_dir, manter: 2);
        Assert.Equal(3, apagados);

        var restantes = BackupManager.Listar(_dir);
        Assert.Equal(2, restantes.Count);
        // os 2 mais recentes (dias 5 e 4) permaneceram
        Assert.Equal(new DateTime(2026, 7, 5, 10, 0, 0), restantes[0].Data);
        Assert.Equal(new DateTime(2026, 7, 4, 10, 0, 0), restantes[1].Data);
    }

    [Fact]
    public void LimparAntigos_ManterMaiorQueTotal_NaoApagaNada()
    {
        Criar("backup_pdv_20260701_100000.zip", new DateTime(2026, 7, 1));
        Criar("backup_pdv_20260702_100000.zip", new DateTime(2026, 7, 2));
        Assert.Equal(0, BackupManager.LimparAntigos(_dir, manter: 10));
        Assert.Equal(2, BackupManager.Listar(_dir).Count);
    }

    [Fact]
    public void LimparAntigos_ManterZero_ApagaTodos()
    {
        Criar("backup_pdv_20260701_100000.zip", new DateTime(2026, 7, 1));
        Criar("backup_pdv_20260702_100000.zip", new DateTime(2026, 7, 2));
        Assert.Equal(2, BackupManager.LimparAntigos(_dir, manter: 0));
        Assert.Empty(BackupManager.Listar(_dir));
    }
}
