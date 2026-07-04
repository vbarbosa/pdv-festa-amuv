using System.Runtime.Versioning;
using PdvFesta.Core;

namespace PdvFesta.App;

/// <summary>
/// Central de seguranca dos dados (disaster recovery), nivel profissional:
///  - Painel de STATUS: banco atual, tamanho, nro de vendas, ultimo backup e alerta de saude.
///  - LISTA dos backups da pasta (data, tamanho) -> restaurar/limpar com 1 clique.
///  - Auto-backup a cada N min + limite de quantos manter (limpa antigos).
///  - Restaurar de arquivo avulso (.db/.zip) tambem continua disponivel.
/// Layout ancorado (Dock/TableLayoutPanel), sem coordenadas fixas.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FormBackup : Form
{
    private readonly Servico _servico;
    private readonly TextBox _txtPasta = new();
    private readonly NumericUpDown _numMin = new();
    private readonly NumericUpDown _numManter = new();
    private readonly NumericUpDown _numVendas = new();
    private readonly CheckBox _chkFechar = new();
    private readonly CheckBox _chkAbrir = new();
    private readonly CheckBox _chkSair = new();
    private readonly Label _lblSaude = new();
    private readonly ListView _lista = new();
    private readonly Label _lblStatus = new();

    public FormBackup(Servico servico)
    {
        _servico = servico;
        Text = "Segurança de Dados - Backup e Restauração";
        Name = "FormBackup";
        Icon = Marca.Icone();
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(660, 700);
        ClientSize = new Size(700, 740);
        Font = new Font("Segoe UI", 11F);

        MontarLayout();
        Carregar();
    }

    private void MontarLayout()
    {
        var titulo = new Label
        {
            Text = "SEGURANÇA DE DADOS", Dock = DockStyle.Top, Height = 44,
            TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 15F, FontStyle.Bold),
            BackColor = Color.FromArgb(60, 60, 60), ForeColor = Color.White
        };

        var raiz = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(14)
        };
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));   // saude (3 linhas + titulo do box)
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 196));   // config (pasta + gatilhos)
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));    // acoes principais
        raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100));    // lista de backups
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));    // acoes da lista + status

        raiz.Controls.Add(MontarSaude(), 0, 0);
        raiz.Controls.Add(MontarConfig(), 0, 1);
        raiz.Controls.Add(MontarAcoesPrincipais(), 0, 2);
        raiz.Controls.Add(MontarLista(), 0, 3);
        raiz.Controls.Add(MontarRodape(), 0, 4);

        Controls.Add(raiz);
        Controls.Add(titulo);
    }

    private Control MontarSaude()
    {
        var box = new GroupBox { Text = "Situação", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
        _lblSaude.Dock = DockStyle.Fill;
        _lblSaude.Font = new Font("Segoe UI", 9.5F);
        _lblSaude.Padding = new Padding(10, 4, 6, 4);
        _lblSaude.TextAlign = ContentAlignment.TopLeft;
        _lblSaude.AutoEllipsis = true;   // caminho longo vira "..." em vez de cortar/vazar
        box.Controls.Add(_lblSaude);
        return box;
    }

    private Control MontarConfig()
    {
        var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 3 };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));

        var lblPasta = new Label { Text = "Pasta de backup (ex: OneDrive):", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        t.Controls.Add(lblPasta, 0, 0); t.SetColumnSpan(lblPasta, 3);

        _txtPasta.Dock = DockStyle.Fill; _txtPasta.ReadOnly = true;
        t.Controls.Add(_txtPasta, 0, 1); t.SetColumnSpan(_txtPasta, 2);
        var btnEscolher = Botao("Escolher...", Color.FromArgb(70, 70, 90));
        btnEscolher.Click += (s, e) => EscolherPasta();
        t.Controls.Add(btnEscolher, 2, 1);

        // titulo dos gatilhos
        var lblGat = new Label { Text = "Quando fazer backup automático (marque os que quiser):",
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
        t.Controls.Add(lblGat, 0, 2); t.SetColumnSpan(lblGat, 3);

        // painel dos gatilhos (numeros + checkboxes) — empilhado
        var g = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };

        // linha 1: a cada N minutos (ate 7 dias = 10080 min) + a cada N vendas
        var l1 = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 2, 0, 0) };
        l1.Controls.Add(new Label { Text = "A cada (min):", AutoSize = true, Margin = new Padding(0, 8, 4, 0) });
        _numMin.Width = 84; _numMin.Minimum = 0; _numMin.Maximum = 10080; _numMin.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        l1.Controls.Add(_numMin);
        l1.Controls.Add(new Label { Text = "(0=desl., até 10080 = 7 dias)", AutoSize = true, Margin = new Padding(4, 8, 0, 0), ForeColor = Color.Gray });
        g.Controls.Add(l1);

        var l2 = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 2, 0, 0) };
        l2.Controls.Add(new Label { Text = "A cada N vendas:", AutoSize = true, Margin = new Padding(0, 8, 4, 0) });
        _numVendas.Width = 84; _numVendas.Minimum = 0; _numVendas.Maximum = 9999; _numVendas.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        l2.Controls.Add(_numVendas);
        l2.Controls.Add(new Label { Text = "(0=desl.)", AutoSize = true, Margin = new Padding(4, 8, 0, 0), ForeColor = Color.Gray });
        g.Controls.Add(l2);

        // checkboxes de eventos
        _chkFechar.Text = "Ao fechar o caixa (fim de turno)"; _chkFechar.AutoSize = true; _chkFechar.Margin = new Padding(0, 4, 0, 0);
        _chkAbrir.Text = "Ao abrir o programa"; _chkAbrir.AutoSize = true; _chkAbrir.Margin = new Padding(0, 2, 0, 0);
        _chkSair.Text = "Ao sair do programa"; _chkSair.AutoSize = true; _chkSair.Margin = new Padding(0, 2, 0, 0);
        g.Controls.Add(_chkFechar);
        g.Controls.Add(_chkAbrir);
        g.Controls.Add(_chkSair);

        // linha final: manter ultimos + aplicar
        var l3 = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
        l3.Controls.Add(new Label { Text = "Manter últimos:", AutoSize = true, Margin = new Padding(0, 8, 4, 0) });
        _numManter.Width = 70; _numManter.Minimum = 0; _numManter.Maximum = 999; _numManter.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        l3.Controls.Add(_numManter);
        var btnAplicar = Botao("Aplicar configurações", Color.FromArgb(0, 120, 200));
        btnAplicar.Width = 200; btnAplicar.Margin = new Padding(16, 2, 0, 2);
        btnAplicar.Click += (s, e) => AplicarConfig();
        l3.Controls.Add(btnAplicar);
        g.Controls.Add(l3);

        t.Controls.Add(g, 0, 3); t.SetColumnSpan(g, 3);
        t.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));   // pasta label
        t.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));   // pasta textbox
        t.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));   // titulo gatilhos
        t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // gatilhos
        t.RowCount = 4;
        return t;
    }

    private Control MontarAcoesPrincipais()
    {
        var barra = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        barra.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        barra.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        var b1 = Botao("Gerar Backup Agora (.zip)", Color.FromArgb(0, 120, 200));
        b1.Click += (s, e) => BackupAgora();
        var b2 = Botao("Restaurar de arquivo (.db/.zip)", Color.FromArgb(90, 90, 90));
        b2.Click += (s, e) => RestaurarDeArquivo();
        barra.Controls.Add(b1, 0, 0);
        barra.Controls.Add(b2, 1, 0);
        return barra;
    }

    private Control MontarLista()
    {
        var box = new GroupBox { Text = "Backups na pasta (mais recente no topo)", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
        _lista.Dock = DockStyle.Fill;
        _lista.View = View.Details;
        _lista.FullRowSelect = true;
        _lista.MultiSelect = false;
        _lista.GridLines = true;
        _lista.Font = new Font("Segoe UI", 10F);
        _lista.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        _lista.Columns.Add("Arquivo", 300);
        _lista.Columns.Add("Data", 150);
        _lista.Columns.Add("Tamanho", 90);
        box.Controls.Add(_lista);
        return box;
    }

    private Control MontarRodape()
    {
        var wrap = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        wrap.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        wrap.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var barra = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        var bRest = Botao("Restaurar selecionado", Color.FromArgb(180, 100, 0)); bRest.Width = 200;
        bRest.Click += (s, e) => RestaurarSelecionado();
        var bLimpar = Botao("Limpar antigos", Color.FromArgb(150, 60, 0)); bLimpar.Width = 150;
        bLimpar.Click += (s, e) => LimparAntigos();
        var bAtualizar = Botao("Atualizar", Color.FromArgb(70, 70, 90)); bAtualizar.Width = 120;
        bAtualizar.Click += (s, e) => Carregar();
        barra.Controls.Add(bRest); barra.Controls.Add(bLimpar); barra.Controls.Add(bAtualizar);
        wrap.Controls.Add(barra, 0, 0);

        _lblStatus.Dock = DockStyle.Fill; _lblStatus.ForeColor = Color.FromArgb(60, 60, 60);
        _lblStatus.TextAlign = ContentAlignment.MiddleLeft;
        wrap.Controls.Add(_lblStatus, 0, 1);
        return wrap;
    }

    private static Button Botao(string texto, Color cor) => new()
    {
        Text = texto, Dock = DockStyle.Fill, Height = 34, Margin = new Padding(3),
        BackColor = cor, ForeColor = Color.White, FlatStyle = FlatStyle.Flat,
        Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
    };

    // ---------------------------------------------------------------- dados

    private void Carregar()
    {
        _txtPasta.Text = _servico.PastaBackup;
        _numMin.Value = Math.Clamp(_servico.IntervaloBackupMin, 0, 10080);
        _numVendas.Value = Math.Clamp(_servico.BackupACadaVendas, 0, 9999);
        _numManter.Value = Math.Clamp(_servico.BackupsManter, 0, 999);
        _chkFechar.Checked = _servico.BackupAoFecharCaixa;
        _chkAbrir.Checked = _servico.BackupAoAbrirApp;
        _chkSair.Checked = _servico.BackupAoSairApp;
        AtualizarSaude();
        AtualizarLista();
    }

    private void AtualizarSaude()
    {
        long bytes = 0;
        try { if (File.Exists(_servico.CaminhoBanco)) bytes = new FileInfo(_servico.CaminhoBanco).Length; } catch { }
        string tam = bytes >= 1024 * 1024 ? $"{bytes / 1024d / 1024d:0.0} MB" : $"{Math.Max(1, bytes / 1024)} KB";

        var recente = BackupManager.MaisRecente(_servico.PastaBackup);
        string ultimo; Color cor;
        if (recente is BackupManager.BackupInfo b)
        {
            var horas = (DateTime.Now - b.Data).TotalHours;
            ultimo = $"{b.Data:dd/MM/yyyy HH:mm} ({b.TamanhoLegivel})";
            cor = horas > 24 ? Color.FromArgb(180, 90, 0) : Color.FromArgb(0, 120, 0);
            if (horas > 24) ultimo += "  ⚠ faz mais de 1 dia!";
        }
        else { ultimo = "NENHUM backup ainda ⚠"; cor = Color.FromArgb(180, 0, 0); }

        int vendas = 0; try { vendas = _servico.TotalVendas(); } catch { }
        _lblSaude.Text =
            $"Banco: {tam}   |   Vendas: {vendas}\n" +
            $"Último backup: {ultimo}\n" +
            $"Arquivo: {_servico.CaminhoBanco}";
        _lblSaude.ForeColor = cor;
    }

    private void AtualizarLista()
    {
        _lista.BeginUpdate();
        _lista.Items.Clear();
        foreach (var b in BackupManager.Listar(_servico.PastaBackup))
        {
            var it = new ListViewItem(b.Nome);
            it.SubItems.Add(b.Data.ToString("dd/MM/yyyy HH:mm"));
            it.SubItems.Add(b.TamanhoLegivel);
            it.Tag = b.Caminho;
            _lista.Items.Add(it);
        }
        _lista.EndUpdate();
    }

    // ---------------------------------------------------------------- acoes

    private void EscolherPasta()
    {
        using var dlg = new FolderBrowserDialog { Description = "Escolha a pasta de backup (ex: OneDrive/Drive)" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        _txtPasta.Text = dlg.SelectedPath;
        _servico.DefinirPastaBackup(dlg.SelectedPath);
        Info("Pasta de backup salva.", Color.FromArgb(0, 120, 0));
        Carregar();
    }

    private void AplicarConfig()
    {
        _servico.DefinirIntervaloBackup((int)_numMin.Value);
        _servico.DefinirBackupACadaVendas((int)_numVendas.Value);
        _servico.DefinirBackupsManter((int)_numManter.Value);
        _servico.DefinirBackupAoFecharCaixa(_chkFechar.Checked);
        _servico.DefinirBackupAoAbrirApp(_chkAbrir.Checked);
        _servico.DefinirBackupAoSairApp(_chkSair.Checked);

        // resume os gatilhos ligados
        var ligados = new List<string>();
        if (_numMin.Value > 0) ligados.Add($"a cada {(int)_numMin.Value} min");
        if (_numVendas.Value > 0) ligados.Add($"a cada {(int)_numVendas.Value} vendas");
        if (_chkFechar.Checked) ligados.Add("ao fechar caixa");
        if (_chkAbrir.Checked) ligados.Add("ao abrir");
        if (_chkSair.Checked) ligados.Add("ao sair");
        Info(ligados.Count == 0
            ? $"Backup automático DESLIGADO. Mantendo últimos {(int)_numManter.Value}."
            : $"Backup: {string.Join(" + ", ligados)}. Mantendo últimos {(int)_numManter.Value}.",
            Color.FromArgb(0, 120, 0));
    }

    private void BackupAgora()
    {
        try
        {
            string destino = _servico.PastaBackup;
            if (string.IsNullOrWhiteSpace(destino))
            {
                using var dlg = new FolderBrowserDialog { Description = "Onde salvar o backup?" };
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                destino = dlg.SelectedPath;
                _servico.DefinirPastaBackup(destino);
            }
            var zip = BackupManager.GerarZip(_servico.CaminhoBanco, destino);
            Info("Backup gerado: " + Path.GetFileName(zip), Color.FromArgb(0, 120, 0));
            Carregar();
        }
        catch (Exception ex) { Info("Erro no backup: " + ex.Message, Color.FromArgb(180, 0, 0)); }
    }

    private void RestaurarSelecionado()
    {
        if (_lista.SelectedItems.Count == 0) { Info("Selecione um backup na lista.", Color.FromArgb(180, 0, 0)); return; }
        var caminho = _lista.SelectedItems[0].Tag as string;
        if (string.IsNullOrWhiteSpace(caminho)) return;
        RestaurarCaminho(caminho, _lista.SelectedItems[0].Text);
    }

    private void RestaurarDeArquivo()
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "Backup (*.zip;*.db)|*.zip;*.db|Zip (*.zip)|*.zip|Banco (*.db)|*.db",
            InitialDirectory = Directory.Exists(_servico.PastaBackup) ? _servico.PastaBackup : ""
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        RestaurarCaminho(dlg.FileName, Path.GetFileName(dlg.FileName));
    }

    private void RestaurarCaminho(string caminho, string nome)
    {
        var r = MessageBox.Show(
            $"Restaurar \"{nome}\" vai SUBSTITUIR o banco atual.\n" +
            "O banco atual será guardado como .bak antes.\n\nO programa será reiniciado. Continuar?",
            "Restaurar Backup", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (r != DialogResult.Yes) return;
        try
        {
            BackupManager.RestaurarAuto(caminho, _servico.CaminhoBanco);
            MessageBox.Show("Backup restaurado! O programa vai reiniciar.", "Restauração",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            Application.Restart();
            Environment.Exit(0);
        }
        catch (Exception ex) { Info("Erro ao restaurar: " + ex.Message, Color.FromArgb(180, 0, 0)); }
    }

    private void LimparAntigos()
    {
        int manter = (int)_numManter.Value;
        var total = BackupManager.Listar(_servico.PastaBackup).Count;
        if (total <= manter) { Info($"Nada a limpar ({total} backup(s), mantendo {manter}).", Color.FromArgb(0, 120, 0)); return; }

        var r = MessageBox.Show(
            $"Apagar os backups mais antigos, mantendo só os {manter} mais recentes?\n" +
            $"({total - manter} arquivo(s) serão apagados)",
            "Limpar antigos", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (r != DialogResult.Yes) return;

        int apagados = BackupManager.LimparAntigos(_servico.PastaBackup, manter);
        Info($"{apagados} backup(s) antigo(s) apagado(s).", Color.FromArgb(0, 120, 0));
        Carregar();
    }

    private void Info(string texto, Color cor)
    {
        _lblStatus.ForeColor = cor;
        _lblStatus.Text = texto;
    }
}
