using System.IO.Compression;
using Microsoft.Data.Sqlite;

namespace PdvFesta.Core;

/// <summary>
/// Backup/restore FISICO do arquivo SQLite (disaster recovery).
/// Diferente do BackupService (que exporta JSON logico), aqui copiamos o .db
/// inteiro — mais rapido e fiel, ideal para "trocar de PC em 2 minutos".
/// </summary>
public static class BackupManager
{
    /// <summary>
    /// Faz checkpoint do WAL para garantir que o .db no disco esteja completo
    /// antes de copiar (senao dados recentes poderiam estar so no arquivo -wal).
    /// </summary>
    public static void Checkpoint(string dbPath)
    {
        if (!File.Exists(dbPath)) return;
        var cs = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
        using (var conn = new SqliteConnection(cs))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            cmd.ExecuteNonQuery();
        }
        // Libera o handle do arquivo do pool para permitir File.Copy/zip logo em seguida.
        SqliteConnection.ClearAllPools();
    }

    private static string Timestamp() => DateTime.Now.ToString("yyyyMMdd_HHmmss");

    /// <summary>Copia o banco para 'destinoDir' com nome backup_pdv_TIMESTAMP.db.</summary>
    public static string CopiarComTimestamp(string dbPath, string destinoDir)
    {
        Directory.CreateDirectory(destinoDir);
        Checkpoint(dbPath);
        var nome = $"backup_pdv_{Timestamp()}.db";
        var destino = Path.Combine(destinoDir, nome);
        File.Copy(dbPath, destino, overwrite: true);
        return destino;
    }

    /// <summary>Empacota o banco num .zip (backup_pdv_TIMESTAMP.zip) em 'destinoDir'.</summary>
    public static string GerarZip(string dbPath, string destinoDir)
    {
        Directory.CreateDirectory(destinoDir);
        Checkpoint(dbPath);
        var zipPath = Path.Combine(destinoDir, $"backup_pdv_{Timestamp()}.zip");
        if (File.Exists(zipPath)) File.Delete(zipPath);

        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        zip.CreateEntryFromFile(dbPath, AppPathsNomeBanco(dbPath));
        return zipPath;
    }

    private static string AppPathsNomeBanco(string dbPath) => Path.GetFileName(dbPath);

    /// <summary>
    /// Restaura substituindo o banco em uso pelo 'origemDb'. Antes de sobrescrever,
    /// guarda o atual como .bak (seguranca). Remove WAL/SHM orfaos do destino.
    /// </summary>
    public static void RestaurarArquivo(string origemDb, string destinoDb)
    {
        if (!File.Exists(origemDb))
            throw new FileNotFoundException("Arquivo de backup nao encontrado.", origemDb);

        // guarda o atual antes de sobrescrever
        if (File.Exists(destinoDb))
        {
            var bak = $"{destinoDb}.{Timestamp()}.bak";
            File.Copy(destinoDb, bak, overwrite: true);
        }

        // remove WAL/SHM do destino para nao misturar com o restaurado
        foreach (var ext in new[] { "-wal", "-shm" })
        {
            var f = destinoDb + ext;
            if (File.Exists(f)) { try { File.Delete(f); } catch { } }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destinoDb)!);
        File.Copy(origemDb, destinoDb, overwrite: true);
    }

    /// <summary>Restaura a partir de um .zip: extrai o primeiro .db e restaura.</summary>
    public static void RestaurarDeZip(string zipPath, string destinoDb)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        var entry = zip.Entries.FirstOrDefault(e => e.Name.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidDataException("O .zip nao contem um banco (.db).");

        var temp = Path.Combine(Path.GetTempPath(), $"restore_{Guid.NewGuid():N}.db");
        try
        {
            entry.ExtractToFile(temp, overwrite: true);
            RestaurarArquivo(temp, destinoDb);
        }
        finally
        {
            if (File.Exists(temp)) { try { File.Delete(temp); } catch { } }
        }
    }

    /// <summary>Restaura automaticamente detectando se e .db ou .zip pela extensao.</summary>
    public static void RestaurarAuto(string arquivo, string destinoDb)
    {
        if (arquivo.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            RestaurarDeZip(arquivo, destinoDb);
        else
            RestaurarArquivo(arquivo, destinoDb);
    }

    // ------------------------------------------------------------- gerenciamento

    /// <summary>Um backup existente na pasta (para a tela listar/restaurar/limpar).</summary>
    public readonly record struct BackupInfo(string Caminho, string Nome, DateTime Data, long Bytes)
    {
        /// <summary>Tamanho legivel: "1,2 MB", "340 KB".</summary>
        public string TamanhoLegivel => Bytes >= 1024 * 1024
            ? $"{Bytes / 1024d / 1024d:0.0} MB"
            : $"{Math.Max(1, Bytes / 1024)} KB";
    }

    /// <summary>
    /// Lista os backups (backup_pdv_*.zip e *.db) de uma pasta, do MAIS NOVO para o mais
    /// antigo. Usa a data de escrita do arquivo. Retorna vazio se a pasta nao existe.
    /// </summary>
    public static List<BackupInfo> Listar(string pastaDir)
    {
        var lista = new List<BackupInfo>();
        if (string.IsNullOrWhiteSpace(pastaDir) || !Directory.Exists(pastaDir)) return lista;
        try
        {
            foreach (var f in Directory.EnumerateFiles(pastaDir, "backup_pdv_*.*"))
            {
                var ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext is not (".zip" or ".db")) continue;
                var fi = new FileInfo(f);
                lista.Add(new BackupInfo(f, fi.Name, fi.LastWriteTime, fi.Length));
            }
        }
        catch { /* pasta inacessivel: retorna o que conseguiu */ }
        return lista.OrderByDescending(b => b.Data).ToList();
    }

    /// <summary>O backup mais recente da pasta, ou null se nao ha nenhum.</summary>
    public static BackupInfo? MaisRecente(string pastaDir) => Listar(pastaDir) is { Count: > 0 } l ? l[0] : null;

    /// <summary>
    /// Apaga os backups mais antigos, mantendo apenas os 'manter' mais recentes. Retorna
    /// quantos foram apagados. Seguro: nunca lanca (best-effort por arquivo).
    /// </summary>
    public static int LimparAntigos(string pastaDir, int manter)
    {
        if (manter < 0) manter = 0;
        var todos = Listar(pastaDir);
        int apagados = 0;
        foreach (var b in todos.Skip(manter))
            try { File.Delete(b.Caminho); apagados++; } catch { /* ignora arquivo travado */ }
        return apagados;
    }
}
