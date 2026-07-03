using PdvFesta.Core;

namespace PdvFesta.App;

/// <summary>
/// Servico de aplicacao: amarra Repositorio + carrinho atual + turno de caixa + config.
/// Ponto unico que as telas usam (fica facil trocar/testar).
/// </summary>
public sealed class Servico : IDisposable
{
    public Repositorio Repo { get; }
    public Carrinho Carrinho { get; } = new();
    public string TituloCupom { get; private set; }
    public string CaminhoBanco { get; }

    /// <summary>Turno de caixa aberto no momento (null = caixa fechado).</summary>
    public Turno? TurnoAtual { get; private set; }
    public bool CaixaAberto => TurnoAtual is { EstaAberto: true };

    private readonly AutoBackupTimer _autoBackup;

    public Servico(string dbPath, string cardapioPath)
    {
        CaminhoBanco = dbPath;
        Repo = new Repositorio(dbPath);
        Repo.Inicializar();

        // Seed do cardapio na 1a execucao (nao sobrescreve se ja tiver dados)
        if (File.Exists(cardapioPath))
        {
            var cardapio = CardapioLoader.CarregarDeArquivo(cardapioPath);
            CardapioLoader.SemearSeVazio(Repo, cardapio);
        }
        TituloCupom = Repo.LerConfig("titulo_cupom", "FESTA");

        // PLUG-AND-PLAY: sem impressora configurada, tenta detectar a termica automaticamente
        // (USB tem prioridade sobre Bluetooth). Assim, ao plugar e abrir, o caixa ja imprime.
        if (!TemImpressora)
        {
            var auto = PrinterDiscovery.SugerirTermica();
            if (!string.IsNullOrWhiteSpace(auto)) DefinirImpressora(auto);
        }

        // Retoma um turno que ficou aberto (ex: queda de energia no meio da festa).
        TurnoAtual = Repo.CaixaAberto();

        // Backup automatico em background (le config do banco a cada disparo)
        _autoBackup = new AutoBackupTimer(
            dbPath,
            obterPastaDestino: () => Repo.LerConfig("backup_pasta", ""),
            log: _ => { /* silencioso; erros nao derrubam o caixa */ });
        IniciarAutoBackup();
    }

    /// <summary>(Re)aplica o intervalo de auto-backup salvo (minutos). 0 = desligado.</summary>
    public void IniciarAutoBackup()
    {
        int min = int.TryParse(Repo.LerConfig("backup_intervalo_min", "0"), out var m) ? m : 0;
        _autoBackup.Configurar(min);
    }

    // ----- config de backup expostas para a tela -----
    public string PastaBackup => Repo.LerConfig("backup_pasta", "");
    public int IntervaloBackupMin => int.TryParse(Repo.LerConfig("backup_intervalo_min", "0"), out var m) ? m : 0;
    public void DefinirPastaBackup(string p) => Repo.SalvarConfig("backup_pasta", p);
    public void DefinirIntervaloBackup(int min) { Repo.SalvarConfig("backup_intervalo_min", min.ToString()); IniciarAutoBackup(); }

    public List<Produto> Produtos() => Repo.ListarProdutos().Where(p => p.Ativo).ToList();
    /// <summary>Catalogo completo (inclui inativos) para a tela de gestao.</summary>
    public List<Produto> ProdutosTodos() => Repo.ListarProdutos();

    // ----- promocoes / combos -----
    private List<Promocao>? _promosCache;
    public List<Promocao> Promocoes() => Repo.ListarPromocoes(incluirInativas: true);
    public List<Promocao> PromocoesAtivas() => _promosCache ??= Repo.ListarPromocoes(incluirInativas: false);
    public long SalvarPromocao(Promocao p) { var id = Repo.SalvarPromocao(p); RecarregarPromocoes(); return id; }
    public void InativarPromocao(long id) { Repo.InativarPromocao(id); RecarregarPromocoes(); }
    public void ExcluirPromocao(long id) { Repo.ExcluirPromocao(id); RecarregarPromocoes(); }
    public void RecarregarPromocoes() => _promosCache = Repo.ListarPromocoes(incluirInativas: false);

    /// <summary>Reavalia os combos/promocoes sobre o carrinho atual (chamar a cada add/remove).</summary>
    public void AplicarPromocoes() => Carrinho.AplicarDescontos(PromocoesAtivas(), DateTime.Now);

    // ----- categorias -----
    public List<Categoria> Categorias() => Repo.ListarCategorias(incluirInativas: true);
    public List<Categoria> CategoriasAtivas() => Repo.ListarCategorias(incluirInativas: false);
    public void SalvarCategoria(Categoria c) => Repo.SalvarCategoria(c);
    public void InativarCategoria(string nome) => Repo.InativarCategoria(nome);
    public int ContarProdutosDaCategoria(string nome) => Repo.ContarProdutosDaCategoria(nome);
    public void ExcluirCategoria(string nome) => Repo.ExcluirCategoria(nome);

    // ----- versionamento do cardapio (exportar/importar .json) -----
    public string ExportarCardapio(string pastaDestino) => CardapioLoader.ExportarParaPasta(Repo, pastaDestino);
    public int ImportarCardapio(string caminho) => CardapioLoader.ImportarDeArquivo(Repo, caminho);

    /// <summary>Garante que a categoria exista (cria no fim da ordem se for nova).</summary>
    public void GarantirCategoria(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) return;
        var todas = Repo.ListarCategorias(incluirInativas: true);
        if (todas.Any(c => string.Equals(c.Nome, nome, StringComparison.OrdinalIgnoreCase))) return;
        int prox = todas.Count == 0 ? 0 : todas.Max(c => c.Ordem) + 1;
        Repo.SalvarCategoria(new Categoria { Nome = nome, Ordem = prox, Ativo = true });
    }

    public string ImpressoraPadrao => Repo.LerConfig("impressora", "");

    /// <summary>Ha uma impressora configurada? (sem ela, o caixa NAO deve travar em popup.)</summary>
    public bool TemImpressora => !string.IsNullOrWhiteSpace(ImpressoraPadrao);
    public void DefinirImpressora(string nome) => Repo.SalvarConfig("impressora", nome);

    // ----- configuracao do cupom -----
    public ConfigCupom LerConfigCupom() => ConfigCupom.Ler(Repo, TituloCupom);
    public void SalvarConfigCupom(ConfigCupom cfg) { cfg.Salvar(Repo); TituloCupom = string.IsNullOrWhiteSpace(cfg.Evento) ? TituloCupom : cfg.Evento; }

    // ----- seguranca (senha de admin) -----
    public const string SenhaAdminPadrao = "0000";
    public string SenhaAdmin => Repo.LerConfig("senha_admin", SenhaAdminPadrao);
    public void DefinirSenhaAdmin(string s) => Repo.SalvarConfig("senha_admin", s);
    public bool ValidarSenhaAdmin(string tentativa) => string.Equals(tentativa, SenhaAdmin, StringComparison.Ordinal);

    // ----- turno de caixa -----
    public Turno AbrirCaixa(int fundoCentavos, string operador)
    {
        TurnoAtual = Repo.AbrirCaixa(fundoCentavos, operador);
        Log.Info($"Caixa ABERTO #{TurnoAtual.Id} fundo={fundoCentavos}c operador='{operador}'");
        return TurnoAtual;
    }

    public void FecharCaixa()
    {
        if (TurnoAtual is null) return;
        var id = TurnoAtual.Id;
        Repo.FecharCaixa(id);
        TurnoAtual.Status = StatusCaixa.Fechado;
        TurnoAtual.Fechamento = DateTime.Now;
        TurnoAtual = null;
        Log.Info($"Caixa FECHADO #{id}");
    }

    /// <summary>
    /// TROCA DE OPERADOR: fecha o turno atual (com sua Leitura Z / audit) e abre um novo
    /// para o proximo operador, SEM parar a festa. O fundo do novo turno e o dinheiro que
    /// fica na gaveta (por padrao, o Total em Gaveta contado/esperado do turno que fechou).
    /// Retorna o novo turno.
    /// </summary>
    public Turno TrocarOperador(string novoOperador, int fundoNovoCentavos)
    {
        var anterior = TurnoAtual?.Id;
        FecharCaixa();
        var novo = AbrirCaixa(fundoNovoCentavos, novoOperador);
        Log.Info($"TROCA DE OPERADOR: turno #{anterior} -> #{novo.Id} operador='{novoOperador}' fundo={fundoNovoCentavos}c");
        return novo;
    }

    /// <summary>Bate a gaveta do turno atual contra o valor CONTADO fisicamente (conferencia).</summary>
    public ResultadoBatimento BaterGaveta(int contadoCentavos) =>
        Caixa.Bater(ResumoTurnoAtual(), contadoCentavos);

    public void RegistrarMovimento(TipoMovimento tipo, int valorCentavos, string motivo)
    {
        if (TurnoAtual is null) return;
        Repo.RegistrarMovimento(new MovimentoCaixa
        {
            CaixaId = TurnoAtual.Id, Tipo = tipo, ValorCentavos = valorCentavos, Motivo = motivo
        });
        Log.Info($"Movimento {tipo} {valorCentavos}c caixa #{TurnoAtual.Id} motivo='{motivo}'");
    }

    /// <summary>Espelho financeiro do turno atual (dashboard em tempo real).</summary>
    public ResumoTurno ResumoTurnoAtual()
    {
        var t = TurnoAtual ?? new Turno { Id = 0 };
        var vendas = TurnoAtual is null ? new List<Venda>() : Repo.ListarVendasPorCaixa(t.Id);
        var movs = TurnoAtual is null ? new List<MovimentoCaixa>() : Repo.ListarMovimentos(t.Id);
        return Caixa.ConsolidarTurno(t, vendas, movs);
    }

    /// <summary>Itens vendidos no turno atual (auditoria das barracas na Leitura Z).</summary>
    public List<ItemVendido> ItensVendidosTurno()
    {
        if (TurnoAtual is null) return new();
        return Caixa.ContarItens(Repo.ListarVendasPorCaixa(TurnoAtual.Id));
    }

    /// <summary>Vendas do turno atual (para a tela de historico/estorno).</summary>
    public List<Venda> VendasDoTurno()
    {
        if (TurnoAtual is null) return new();
        return Repo.ListarVendasPorCaixa(TurnoAtual.Id);
    }

    /// <summary>Estorna (cancela) uma venda: soft delete + rastro no log.</summary>
    public void CancelarVenda(long vendaId)
    {
        Repo.CancelarVenda(vendaId);
        Log.Aviso($"Venda #{vendaId} CANCELADA (estorno) no caixa #{TurnoAtual?.Id}");
    }

    /// <summary>
    /// Fecha a venda atual: persiste (dado seguro PRIMEIRO) e imprime o cupom.
    /// Retorna a venda salva + o resultado da impressao (ok, msg) para a UI decidir
    /// se mostra o dialogo de "tentar imprimir de novo".
    /// </summary>
    public (Venda venda, bool impressaoOk, string impressaoMsg) FinalizarVenda(
        FormaPagamento forma, int recebidoCentavos, string operador)
    {
        var venda = Carrinho.FecharVenda(forma, recebidoCentavos, operador);
        venda.CaixaId = TurnoAtual?.Id;       // vincula ao turno atual
        Repo.SalvarVenda(venda);              // 1) grava PRIMEIRO (dado seguro)
        Carrinho.Limpar();
        Log.Info($"Venda #{venda.Id} {forma} total={venda.TotalCentavos}c troco={venda.TrocoCentavos}c caixa={venda.CaixaId}");

        // 2) imprime (se falhar, a venda ja esta salva; operador pode reimprimir)
        var (ok, msg) = ImprimirVenda(venda);
        if (!ok) Log.Aviso($"Impressao falhou venda #{venda.Id}: {msg}");
        return (venda, ok, msg);
    }

    /// <summary>
    /// MODO DEMONSTRACAO (gravacao do video): PDV_DEMO=1 faz o app fingir que imprimiu
    /// com sucesso, sem tocar em hardware. Assim a demo nunca abre o popup de "erro na
    /// impressora" — nao ha impressora conectada porque e so uma gravacao.
    /// </summary>
    private static bool ModoDemo => Environment.GetEnvironmentVariable("PDV_DEMO") == "1";

    /// <summary>
    /// Imprime (ou reimprime) o cupom de uma venda usando o layout configurado.
    /// SEGURANCA: nao imprime venda CANCELADA (estornada). Ao imprimir com sucesso, registra
    /// a impressao (contador de vias) para o Historico mostrar quantas vezes a nota saiu.
    /// </summary>
    public (bool ok, string msg) ImprimirVenda(Venda venda)
    {
        if (venda.Cancelada)
            return (false, "Venda CANCELADA (estornada) nao pode ser reimpressa.");
        if (ModoDemo) { venda.Impressoes = Repo.RegistrarImpressao(venda.Id); return (true, "OK (demo)"); }
        var impressora = ImpressoraPadrao;
        if (string.IsNullOrWhiteSpace(impressora))
            return (false, "Nenhuma impressora configurada (F12).");
        var (ok, msg) = EscPosPrinter.ImprimirTicket(impressora, venda, LerConfigCupom());
        if (ok) venda.Impressoes = Repo.RegistrarImpressao(venda.Id);   // conta 1a via + reimpressoes
        return (ok, msg);
    }

    /// <summary>Imprime a Leitura Z do turno atual.</summary>
    public (bool ok, string msg) ImprimirFechamentoZ(ResumoTurno resumo, IEnumerable<ItemVendido> itens)
    {
        if (ModoDemo) return (true, "OK (demo)");
        var impressora = ImpressoraPadrao;
        if (string.IsNullOrWhiteSpace(impressora))
            return (false, "Nenhuma impressora configurada (F12).");
        return EscPosPrinter.ImprimirFechamentoZ(impressora, resumo, itens, LerConfigCupom());
    }

    public ResumoCaixa Fechamento() => Caixa.Consolidar(Repo.ListarVendas());

    public void Dispose()
    {
        _autoBackup.Dispose();
        Repo.Dispose();
    }
}
