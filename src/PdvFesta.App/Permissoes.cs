using PdvFesta.Core;

namespace PdvFesta.App;

/// <summary>Acoes protegiveis por senha de admin (o admin decide quais exigem senha).</summary>
public enum AcaoProtegida
{
    AbrirCaixa,
    FecharCaixa,
    TrocarOperador,
    SangriaSuprimento,
    GerenciarProdutos,
    GerenciarCategorias,
    GerenciarPromocoes,
    EstornarVenda,
    Backup,
    ExportarCsv,      // exportar dados/relatorios (CSV) do turno
    ConfigImpressora,
    LayoutCupom,
    Permissoes,       // a propria tela de permissoes SEMPRE exige senha (nao configuravel)
}

/// <summary>
/// Politica de permissoes: para cada <see cref="AcaoProtegida"/>, se ela EXIGE a senha de
/// admin ou esta LIBERADA para o operador. O admin configura na tela de Permissoes; fica
/// salvo no banco (chave "perm_&lt;acao&gt;" = "1" exige / "0" liberado). Defaults sensatos:
/// dinheiro/dados/exclusao exigem senha; config leve fica liberada.
/// </summary>
public sealed class Permissoes
{
    private readonly Servico _servico;
    public Permissoes(Servico servico) => _servico = servico;

    /// <summary>Default de cada acao quando nunca foi configurada (true = exige senha).</summary>
    public static bool PadraoExigeSenha(AcaoProtegida a) => a switch
    {
        // criticas (dinheiro / dados / exclusao) -> exigem senha por padrao
        AcaoProtegida.AbrirCaixa => true,
        AcaoProtegida.FecharCaixa => true,
        AcaoProtegida.TrocarOperador => true,
        AcaoProtegida.SangriaSuprimento => true,
        AcaoProtegida.GerenciarProdutos => true,
        AcaoProtegida.EstornarVenda => true,
        AcaoProtegida.Backup => true,
        AcaoProtegida.ExportarCsv => true,  // dados financeiros saindo -> exige senha por padrao
        AcaoProtegida.Permissoes => true,   // sempre (reforcado em ExigeSenha)
        // configuracao leve -> liberada por padrao
        AcaoProtegida.GerenciarCategorias => false,
        AcaoProtegida.GerenciarPromocoes => false,
        AcaoProtegida.ConfigImpressora => false,
        AcaoProtegida.LayoutCupom => false,
        _ => true
    };

    /// <summary>Rotulo amigavel da acao (para a tela de permissoes).</summary>
    public static string Rotulo(AcaoProtegida a) => a switch
    {
        AcaoProtegida.AbrirCaixa => "Abrir caixa",
        AcaoProtegida.FecharCaixa => "Fechar caixa (Leitura Z)",
        AcaoProtegida.TrocarOperador => "Trocar operador",
        AcaoProtegida.SangriaSuprimento => "Sangria / Suprimento",
        AcaoProtegida.GerenciarProdutos => "Gerenciar produtos",
        AcaoProtegida.GerenciarCategorias => "Gerenciar categorias",
        AcaoProtegida.GerenciarPromocoes => "Gerenciar promoções/combos",
        AcaoProtegida.EstornarVenda => "Estornar venda",
        AcaoProtegida.Backup => "Backup / Restauração",
        AcaoProtegida.ExportarCsv => "Exportar CSV (relatórios)",
        AcaoProtegida.ConfigImpressora => "Configurar impressora",
        AcaoProtegida.LayoutCupom => "Layout do cupom",
        AcaoProtegida.Permissoes => "Tela de permissões",
        _ => a.ToString()
    };

    private static string Chave(AcaoProtegida a) => "perm_" + a;

    /// <summary>Esta acao exige a senha de admin? (a tela de Permissoes SEMPRE exige.)</summary>
    public bool ExigeSenha(AcaoProtegida a)
    {
        if (a == AcaoProtegida.Permissoes) return true;   // nao pode ser desligada
        var v = _servico.Repo.LerConfig(Chave(a), PadraoExigeSenha(a) ? "1" : "0");
        return v == "1";
    }

    public void Definir(AcaoProtegida a, bool exigeSenha) =>
        _servico.Repo.SalvarConfig(Chave(a), exigeSenha ? "1" : "0");
}
