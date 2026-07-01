using System.Runtime.Versioning;

namespace PdvFesta.App;

/// <summary>
/// Gestao do ciclo de vida das janelas secundarias:
///  - SINGLETON: impede abrir duas instancias da mesma tela (ex: dois Backups).
///  - MODAL + DISPOSE: toda tela abre com ShowDialog (trava o caixa atras) e e
///    liberada da memoria (using) ao fechar, evitando lentidao em 12h de festa.
///  - Trava de ADMIN: exige senha antes de abrir telas sensiveis.
/// </summary>
[SupportedOSPlatform("windows")]
public static class Dialogos
{
    private static readonly HashSet<Type> _abertas = new();

    /// <summary>Abre a tela como modal, garantindo instancia unica e Dispose ao fechar.</summary>
    public static DialogResult Modal<T>(IWin32Window owner, Func<T> criar) where T : Form
    {
        if (!_abertas.Add(typeof(T)))
            return DialogResult.None;               // ja existe uma aberta -> ignora

        try
        {
            using var f = criar();
            AjusteLayout.Blindar(f);       // anti-corte: nenhuma tela abre com botoes escondidos
            return f.ShowDialog(owner);
        }
        finally
        {
            _abertas.Remove(typeof(T));             // Dispose ja ocorreu pelo using
        }
    }

    /// <summary>
    /// Pede a senha de administrador. Retorna true se liberou.
    /// Usado antes de abrir Configuracoes, Produtos, Layout, Fechamento etc.
    /// </summary>
    public static bool LiberarAdmin(IWin32Window owner, Servico servico)
    {
        using var f = new FormSenhaAdmin(servico);
        AjusteLayout.Blindar(f);
        return f.ShowDialog(owner) == DialogResult.OK;
    }

    /// <summary>
    /// Libera uma ACAO conforme a politica de permissoes: se o admin marcou que a acao
    /// exige senha, pede a senha; se liberou, passa direto (retorna true). Centraliza a
    /// decisao — as telas so dizem QUAL acao vao fazer.
    /// </summary>
    public static bool LiberarAcao(IWin32Window owner, Servico servico, AcaoProtegida acao)
    {
        var perm = new Permissoes(servico);
        if (!perm.ExigeSenha(acao)) return true;      // liberada -> sem senha
        return LiberarAdmin(owner, servico);          // exige -> pede a senha
    }
}
