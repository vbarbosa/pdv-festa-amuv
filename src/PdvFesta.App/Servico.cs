using PdvFesta.Core;

namespace PdvFesta.App;

/// <summary>
/// Servico de aplicacao: amarra Repositorio + carrinho atual + config.
/// Ponto unico que as telas usam (fica facil trocar/testar).
/// </summary>
public sealed class Servico : IDisposable
{
    public Repositorio Repo { get; }
    public Carrinho Carrinho { get; } = new();
    public string TituloCupom { get; private set; }
    public string CaminhoBanco { get; }

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

    public string ImpressoraPadrao => Repo.LerConfig("impressora", "");
    public void DefinirImpressora(string nome) => Repo.SalvarConfig("impressora", nome);

    /// <summary>Fecha a venda atual: persiste e imprime o cupom. Retorna a venda salva.</summary>
    public Venda FinalizarVenda(FormaPagamento forma, int recebidoCentavos, string operador)
    {
        var venda = Carrinho.FecharVenda(forma, recebidoCentavos, operador);
        Repo.SalvarVenda(venda);              // 1) grava PRIMEIRO (dado seguro)
        Carrinho.Limpar();

        // 2) imprime (se falhar, a venda ja esta salva; operador pode reimprimir)
        var impressora = ImpressoraPadrao;
        if (!string.IsNullOrWhiteSpace(impressora))
            EscPosPrinter.ImprimirCupom(impressora, venda, TituloCupom);

        return venda;
    }

    public ResumoCaixa Fechamento() => Caixa.Consolidar(Repo.ListarVendas());

    public void Dispose()
    {
        _autoBackup.Dispose();
        Repo.Dispose();
    }
}
