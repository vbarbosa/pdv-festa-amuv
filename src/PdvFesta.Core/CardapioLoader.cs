using System.Text.Json;
using System.Text.Json.Serialization;

namespace PdvFesta.Core;

/// <summary>Cardapio lido do JSON (catalogo + titulo do cupom).</summary>
public sealed class Cardapio
{
    public string TituloCupom { get; set; } = "FESTA";
    public List<Produto> Produtos { get; set; } = new();
}

/// <summary>Carrega o cardapio do arquivo JSON e faz o seed inicial do banco.</summary>
public static class CardapioLoader
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static Cardapio CarregarDeArquivo(string caminho) =>
        CarregarDeTexto(File.ReadAllText(caminho));

    public static Cardapio CarregarDeTexto(string json) =>
        JsonSerializer.Deserialize<Cardapio>(json, Opts)
        ?? throw new InvalidDataException("cardapio.json invalido.");

    /// <summary>
    /// Popula o catalogo com o cardapio SOMENTE se o banco ainda nao tiver produtos.
    /// Assim, alteracoes feitas pelo usuario no app nao sao perdidas a cada abertura.
    /// </summary>
    public static void SemearSeVazio(Repositorio repo, Cardapio cardapio)
    {
        if (repo.ListarProdutos().Count > 0) return;
        repo.SalvarCatalogo(cardapio.Produtos);
        repo.SalvarConfig("titulo_cupom", cardapio.TituloCupom);
    }
}
