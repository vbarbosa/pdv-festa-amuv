using System.Text.Json;
using System.Text.Json.Serialization;

namespace PdvFesta.Core;

/// <summary>Cardapio lido do JSON (catalogo + titulo do cupom + ordem das categorias).</summary>
public sealed class Cardapio
{
    public string TituloCupom { get; set; } = "FESTA";
    public List<Produto> Produtos { get; set; } = new();
    /// <summary>Ordem de exibicao das abas (ex: Comidas, Doces, Bebidas...). Opcional.</summary>
    public List<string> Categorias { get; set; } = new();
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
        if (repo.ListarProdutos().Count == 0)
        {
            repo.SalvarCatalogo(cardapio.Produtos);
            repo.SalvarConfig("titulo_cupom", cardapio.TituloCupom);
        }

        // Categorias: semeia se a tabela estiver vazia (idempotente), inclusive em bancos
        // ja existentes (migracao suave para quem instalou antes das categorias).
        // Ordem = lista explicita do JSON + categorias vindas dos produtos (sem duplicar).
        var ordem = new List<string>(cardapio.Categorias);
        foreach (var c in cardapio.Produtos.Select(p => p.Categoria))
            if (!ordem.Contains(c)) ordem.Add(c);
        foreach (var c in repo.ListarProdutos().Select(p => p.Categoria))
            if (!ordem.Contains(c)) ordem.Add(c);
        repo.SemearCategoriasSeVazio(ordem);
    }
}
