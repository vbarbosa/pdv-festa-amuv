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

    /// <summary>
    /// Fluxo ROBUSTO e unificado de exportacao: pergunta as OPCOES (formato CSV/XLSX/abas, como
    /// listar itens, quais secoes), escolhe o destino e grava. Usado pelo Relatorio Gerencial e
    /// pelo export do turno (menu Arquivo/Historico). 'vendas' e 'periodoDescr' definem o conteudo.
    /// 'prefixo' compoe o nome do arquivo (com as datas -> nunca sobrescreve).
    /// </summary>
    public static void ExportarComDialogo(IWin32Window owner, Servico servico,
        IReadOnlyList<PdvFesta.Core.Venda> vendas, string periodoDescr, string prefixo)
    {
        // trava configuravel: exportar dados financeiros pode exigir senha de admin.
        if (!LiberarAcao(owner, servico, AcaoProtegida.ExportarCsv)) return;

        // 1) opcoes
        using var opt = new DialogoExport();
        opt.ShowDialog(owner);
        if (!opt.Confirmado) return;

        // 2) destino (pasta para varios CSV; arquivo para os demais)
        string destino;
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (opt.ExigePasta)
        {
            using var fbd = new FolderBrowserDialog { Description = "Pasta para os arquivos CSV" };
            if (Directory.Exists(desktop)) fbd.SelectedPath = desktop;
            if (fbd.ShowDialog(owner) != DialogResult.OK) return;
            destino = fbd.SelectedPath;
        }
        else
        {
            using var sfd = new SaveFileDialog
            {
                InitialDirectory = Directory.Exists(desktop) ? desktop : "",
                FileName = $"{prefixo}.{opt.Extensao}",
                Filter = opt.Extensao == "csv" ? "CSV (*.csv)|*.csv" : "Excel (*.xlsx)|*.xlsx"
            };
            if (sfd.ShowDialog(owner) != DialogResult.OK) return;
            destino = sfd.FileName;
        }

        // 3) monta as tabelas e grava
        try
        {
            var tabelas = PdvFesta.Core.RelatorioBuilder.Montar(vendas, periodoDescr, opt.ModoItens, opt.Secoes);
            var gerados = PdvFesta.Core.ExportadorArquivos.Gravar(tabelas, opt.Formato, destino, prefixo);
            MessageBox.Show(
                $"Exportado ({vendas.Count} vendas):\n\n" + string.Join("\n", gerados.Select(g => "• " + Path.GetFileName(g))) +
                $"\n\nEm: {Path.GetDirectoryName(gerados[0])}",
                "Exportar", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Erro ao exportar: " + ex.Message, "Exportar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    /// <summary>Export do TURNO ATUAL (menu Arquivo/Historico) — usa o fluxo robusto unificado.</summary>
    public static void ExportarCsvComDialogo(IWin32Window owner, Servico servico)
    {
        var vendas = servico.VendasDoTurno();
        var descr = servico.TurnoAtual is { } t ? $"Turno #{t.Id} — {t.Abertura:dd/MM/yyyy}" : "Turno atual";
        var prefixo = $"festa-turno-{DateTime.Now:yyyyMMdd-HHmm}";
        ExportarComDialogo(owner, servico, vendas, descr, prefixo);
    }
}
