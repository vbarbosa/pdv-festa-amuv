using System.Text.Json;
using System.Text.Json.Serialization;

namespace PdvFesta.Core;

/// <summary>Cardapio lido do JSON (catalogo + titulo do cupom + ordem das categorias + promocoes).</summary>
public sealed class Cardapio
{
    public string TituloCupom { get; set; } = "FESTA";
    public List<Produto> Produtos { get; set; } = new();
    /// <summary>Ordem de exibicao das abas (ex: Comidas, Doces, Bebidas...). Opcional.</summary>
    public List<string> Categorias { get; set; } = new();
    /// <summary>Combos/promocoes iniciais (ex: os combos "ate 20h" do cartaz). Opcional.</summary>
    public List<PromocaoSeed> Promocoes { get; set; } = new();
}

/// <summary>Semente de promocao no JSON (horarios como texto "HH:mm", faceis de editar).</summary>
public sealed class PromocaoSeed
{
    public string Descricao { get; set; } = "";
    public string Tipo { get; set; } = "Combo";
    public int DescontoCentavos { get; set; }
    public string? HoraInicio { get; set; }
    public string? HoraFim { get; set; }
    /// <summary>Datas "yyyy-MM-dd" (opcionais); dias "Sabado,Domingo" ou "Todos" (opcional).</summary>
    public string? DataInicio { get; set; }
    public string? DataFim { get; set; }
    public string? Dias { get; set; }
    public List<PromocaoItem> Itens { get; set; } = new();

    public Promocao ParaPromocao() => new()
    {
        Descricao = Descricao,
        Tipo = string.Equals(Tipo, "PrecoEspecial", StringComparison.OrdinalIgnoreCase)
            ? TipoPromocao.PrecoEspecial : TipoPromocao.Combo,
        ValorDescontoCentavos = DescontoCentavos,
        HoraInicio = TimeSpan.TryParse(HoraInicio, out var hi) ? hi : null,
        HoraFim = TimeSpan.TryParse(HoraFim, out var hf) ? hf : null,
        DataInicio = DateTime.TryParse(DataInicio, out var di) ? di.Date : null,
        DataFim = DateTime.TryParse(DataFim, out var df) ? df.Date : null,
        Dias = ParsearDias(Dias),
        Ativo = true,
        Itens = Itens
    };

    /// <summary>"Sabado,Domingo" -> flags; vazio/"Todos" -> Todos (vale todo dia).</summary>
    private static DiasSemana ParsearDias(string? txt)
    {
        if (string.IsNullOrWhiteSpace(txt)) return DiasSemana.Todos;
        var res = DiasSemana.Nenhum;
        foreach (var parte in txt.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (Enum.TryParse<DiasSemana>(parte, ignoreCase: true, out var d)) res |= d;
        return res == DiasSemana.Nenhum ? DiasSemana.Todos : res;
    }
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

        // Promocoes/combos iniciais (ex: os combos "ate 20h" do cartaz) - so se vazio.
        repo.SemearPromocoesSeVazio(cardapio.Promocoes.Select(s => s.ParaPromocao()));
    }

    private static readonly JsonSerializerOptions OptsExport = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// EXPORTA o cardapio atual do banco (produtos + categorias em ordem + titulo) para um
    /// arquivo .json VERSIONADO (com data/hora no nome). Serve pra salvar, mover para outro
    /// PC ou voltar a uma versao anterior. Retorna o caminho gerado.
    /// </summary>
    public static string ExportarParaPasta(Repositorio repo, string pastaDestino)
    {
        Directory.CreateDirectory(pastaDestino);
        var cardapio = new Cardapio
        {
            TituloCupom = repo.LerConfig("titulo_cupom", "FESTA"),
            Produtos = repo.ListarProdutos(),
            Categorias = repo.ListarCategorias(incluirInativas: true).Select(c => c.Nome).ToList()
            // promocoes ficam no seed/JSON original; o versionamento foca em produtos/categorias.
        };
        var nome = $"cardapio_{DateTime.Now:yyyy-MM-dd_HHmmss}.json";
        var caminho = Path.Combine(pastaDestino, nome);
        File.WriteAllText(caminho, JsonSerializer.Serialize(cardapio, OptsExport));
        return caminho;
    }

    /// <summary>
    /// IMPORTA um cardapio de um arquivo .json, SUBSTITUINDO o catalogo atual (produtos e
    /// categorias). Nao apaga vendas nem turnos — so o cardapio. Retorna quantos produtos
    /// foram importados.
    /// </summary>
    public static int ImportarDeArquivo(Repositorio repo, string caminho)
    {
        var cardapio = CarregarDeArquivo(caminho);
        repo.SalvarCatalogo(cardapio.Produtos);   // DELETE + INSERT (substitui o catalogo)
        if (!string.IsNullOrWhiteSpace(cardapio.TituloCupom))
            repo.SalvarConfig("titulo_cupom", cardapio.TituloCupom);
        // categorias: garante que todas existam (na ordem do arquivo).
        int ordem = 0;
        foreach (var nome in cardapio.Categorias)
            repo.SalvarCategoria(new Categoria { Nome = nome, Ordem = ordem++, Ativo = true });
        return cardapio.Produtos.Count;
    }
}
