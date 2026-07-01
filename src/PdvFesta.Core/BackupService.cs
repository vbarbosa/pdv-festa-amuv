using System.Text.Json;
using System.Text.Json.Serialization;

namespace PdvFesta.Core;

/// <summary>Pacote de backup: catalogo + vendas num unico JSON portatil.</summary>
public sealed class BackupPackage
{
    public string Versao { get; set; } = "1.0";
    public DateTime GeradoEm { get; set; } = DateTime.Now;
    public List<Produto> Produtos { get; set; } = new();
    public List<Venda> Vendas { get; set; } = new();
}

/// <summary>
/// Exporta/importa TODO o estado (catalogo + vendas) para um arquivo JSON unico.
/// Serve de "dump do banco" para continuar a festa em outro PC se um caixa quebrar.
/// </summary>
public static class BackupService
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Exportar(Repositorio repo)
    {
        var pkg = new BackupPackage
        {
            Produtos = repo.ListarProdutos(),
            Vendas = repo.ListarVendas()
        };
        return JsonSerializer.Serialize(pkg, Opts);
    }

    /// <summary>
    /// Importa SUBSTITUINDO os dados atuais (nao soma), para o total do caixa
    /// nunca ser contado em dobro ao restaurar.
    /// </summary>
    public static void Importar(Repositorio repo, string json)
    {
        var pkg = JsonSerializer.Deserialize<BackupPackage>(json, Opts)
                  ?? throw new InvalidDataException("Backup invalido ou vazio.");
        repo.SalvarCatalogo(pkg.Produtos);
        repo.SubstituirVendas(pkg.Vendas);
    }

    public static void ExportarParaArquivo(Repositorio repo, string caminho) =>
        File.WriteAllText(caminho, Exportar(repo));

    public static void ImportarDeArquivo(Repositorio repo, string caminho) =>
        Importar(repo, File.ReadAllText(caminho));
}
