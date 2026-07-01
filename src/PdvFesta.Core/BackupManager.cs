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
}
