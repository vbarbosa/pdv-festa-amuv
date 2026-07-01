namespace PdvFesta.Core;

/// <summary>Modo de impressao do cupom de consumo.</summary>
public enum ModoCupom
{
    /// <summary>Recibo completo: cabecalho, itens com valor, total, pagamento e troco.</summary>
    Completo = 0,
    /// <summary>
    /// Ficha de consumo (economico): so quantidade + nome em fonte EXPANDIDA,
    /// sem valores. Leitura instantanea na barraca; gasta menos bobina.
    /// </summary>
    FichaConsumo = 1
}

/// <summary>Estilo grafico de uma linha do cupom (a camada ESC/POS traduz em bytes).</summary>
public enum EstiloLinha
{
    /// <summary>Texto normal, alinhado a esquerda (&lt;= 32 colunas).</summary>
    Normal = 0,
    /// <summary>Titulo centralizado em fonte dupla (&lt;= 32 colunas, sera 16 na pratica).</summary>
    Titulo = 1,
    /// <summary>Fonte expandida (dupla altura+largura); largura util cai para 16 colunas.</summary>
    Expandida = 2,
    /// <summary>Marcador de CORTE/separacao de ficha (nao imprime texto).</summary>
    Corte = 3
}

/// <summary>Uma linha do cupom pronta para impressao: texto + como renderizar.</summary>
public sealed record LinhaCupom(string Texto, EstiloLinha Estilo = EstiloLinha.Normal)
{
    public static readonly LinhaCupom CorteFicha = new("", EstiloLinha.Corte);
}

/// <summary>
/// Configuracao do layout do cupom impresso, persistida no SQLite (tabela config).
/// Controla o que sai na bobina termica de 58mm para economizar papel e agilizar
/// a entrega nas barracas.
/// </summary>
public sealed class ConfigCupom
{
    /// <summary>Nome do evento no topo (ex: "Festa Junina Familia"). Vazio = nao imprime.</summary>
    public string Evento { get; set; } = "";
    /// <summary>Subtitulo (ex: "Caixa 01"). Vazio = nao imprime.</summary>
    public string Subtitulo { get; set; } = "";
    public ModoCupom Modo { get; set; } = ModoCupom.Completo;
    /// <summary>No modo Ficha: separa/corta uma ficha por item (barracas diferentes).</summary>
    public bool SepararPorItem { get; set; }
    /// <summary>Mensagem de rodape (agradecimento/senha). Vazio = nao imprime.</summary>
    public string Rodape { get; set; } = "Obrigado! Bom Arraia!";

    // ---- chaves de persistencia (tabela config chave/valor) ----
    private const string KEvento = "cupom_evento";
    private const string KSubtitulo = "cupom_subtitulo";
    private const string KModo = "cupom_modo";
    private const string KSeparar = "cupom_separar";
    private const string KRodape = "cupom_rodape";

    /// <summary>Le a configuracao do cupom do banco (com defaults sensatos).</summary>
    public static ConfigCupom Ler(Repositorio repo, string tituloPadrao = "FESTA")
    {
        return new ConfigCupom
        {
            Evento = repo.LerConfig(KEvento, tituloPadrao),
            Subtitulo = repo.LerConfig(KSubtitulo, ""),
            Modo = repo.LerConfig(KModo, "0") == "1" ? ModoCupom.FichaConsumo : ModoCupom.Completo,
            SepararPorItem = repo.LerConfig(KSeparar, "0") == "1",
            Rodape = repo.LerConfig(KRodape, "Obrigado! Bom Arraia!")
        };
    }

    /// <summary>Persiste a configuracao do cupom no banco.</summary>
    public void Salvar(Repositorio repo)
    {
        repo.SalvarConfig(KEvento, Evento);
        repo.SalvarConfig(KSubtitulo, Subtitulo);
        repo.SalvarConfig(KModo, Modo == ModoCupom.FichaConsumo ? "1" : "0");
        repo.SalvarConfig(KSeparar, SepararPorItem ? "1" : "0");
        repo.SalvarConfig(KRodape, Rodape);
    }
}
