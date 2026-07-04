using System.Runtime.Versioning;
using PdvFesta.Core;

namespace PdvFesta.App;

/// <summary>
/// Central de Impressora (F12): gerencia a termica de cupom de forma profissional.
/// Semaforo de status ao vivo, detalhes tecnicos (porta/driver/padrao), fila de impressao
/// com "Limpar travados", e acoes rapidas (detectar, salvar, testar, padrao do Windows).
/// Layout 100% ancorado (Dock/TableLayoutPanel), sem coordenadas fixas.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FormPrinterConfig : Form
{
    private readonly Servico _servico;
    private readonly ComboBox _cmb = new();
    private readonly Label _lblStatus = new();          // rodape: ultima acao

    // semaforo ao vivo
    private readonly Panel _bolha = new();
    private readonly Label _lblSemaforo = new();
    private readonly Label _lblDica = new();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 3000 };

    // detalhes tecnicos
    private readonly Label _lblDetalhes = new();

    // fila de impressao
    private readonly ListView _fila = new();
    private readonly Button _btnLimpar = new();

    public FormPrinterConfig(Servico servico)
    {
        _servico = servico;

        Text = "Central de Impressora";
        Name = "FormPrinterConfig";
        Icon = Marca.Icone();
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        ClientSize = new Size(740, 620);
        Font = new Font("Segoe UI", 11F);

        MontarLayout();
        CarregarLista();
        _cmb.SelectedIndexChanged += (s, e) => AtualizarTudo();

        _timer.Tick += (s, e) => AtualizarStatusEFila();   // status ao vivo, sem travar a tela
        _timer.Start();
        FormClosed += (s, e) => _timer.Stop();

        AtualizarTudo();
    }

    // ---------------------------------------------------------------- layout

    private void MontarLayout()
    {
        var raiz = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(16)
        };
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));    // seletor
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));    // semaforo
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));    // detalhes tecnicos
        raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100));    // fila
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 122));   // acoes + status

        raiz.Controls.Add(MontarSeletor(), 0, 0);
        raiz.Controls.Add(MontarSemaforo(), 0, 1);
        raiz.Controls.Add(MontarDetalhes(), 0, 2);
        raiz.Controls.Add(MontarFila(), 0, 3);
        raiz.Controls.Add(MontarAcoes(), 0, 4);

        Controls.Add(raiz);
    }

    private Control MontarSeletor()
    {
        var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        t.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        t.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        var lbl = new Label { Text = "Impressora / Porta:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        t.Controls.Add(lbl, 0, 0);
        t.SetColumnSpan(lbl, 2);

        _cmb.Dock = DockStyle.Fill;
        _cmb.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmb.Font = new Font("Segoe UI", 12F);
        t.Controls.Add(_cmb, 0, 1);

        var btnAtualizar = new Button { Text = "Atualizar", Dock = DockStyle.Fill, Margin = new Padding(6, 0, 0, 0) };
        btnAtualizar.Click += (s, e) => { CarregarLista(); AtualizarTudo(); };
        t.Controls.Add(btnAtualizar, 1, 1);
        return t;
    }

    /// <summary>Semaforo grande (bolinha + rotulo) + dica de acao, visivel a distancia.</summary>
    private Control MontarSemaforo()
    {
        var card = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(248, 248, 250), Padding = new Padding(14, 10, 14, 10) };

        _bolha.Size = new Size(46, 46);
        _bolha.Location = new Point(16, 24);
        _bolha.BackColor = Color.Gray;
        // desenha um circulo (nao um quadrado) via regiao
        _bolha.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var br = new SolidBrush(_bolha.BackColor);
            e.Graphics.FillEllipse(br, 0, 0, _bolha.Width - 1, _bolha.Height - 1);
        };
        _bolha.BackColorChanged += (s, e) => _bolha.Invalidate();

        _lblSemaforo.Location = new Point(78, 18);
        _lblSemaforo.AutoSize = true;
        _lblSemaforo.Font = new Font("Segoe UI", 15F, FontStyle.Bold);
        _lblSemaforo.Text = "—";

        _lblDica.Location = new Point(80, 52);
        _lblDica.AutoSize = true;
        _lblDica.MaximumSize = new Size(600, 0);
        _lblDica.ForeColor = Color.FromArgb(90, 90, 90);
        _lblDica.Font = new Font("Segoe UI", 10F);
        _lblDica.Text = "";

        card.Controls.Add(_bolha);
        card.Controls.Add(_lblSemaforo);
        card.Controls.Add(_lblDica);
        return card;
    }

    private Control MontarDetalhes()
    {
        var box = new GroupBox { Text = "Detalhes tecnicos", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
        _lblDetalhes.Dock = DockStyle.Fill;
        _lblDetalhes.Font = new Font("Consolas", 10F);
        _lblDetalhes.Padding = new Padding(10, 6, 6, 6);
        _lblDetalhes.TextAlign = ContentAlignment.TopLeft;
        box.Controls.Add(_lblDetalhes);
        return box;
    }

    private Control MontarFila()
    {
        var box = new GroupBox { Text = "Fila de impressao", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };

        var inner = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(6) };
        inner.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        inner.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        _fila.Dock = DockStyle.Fill;
        _fila.View = View.Details;
        _fila.FullRowSelect = true;
        _fila.GridLines = true;
        _fila.MultiSelect = false;
        _fila.Font = new Font("Segoe UI", 10F);
        _fila.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        _fila.Columns.Add("#", 44);
        _fila.Columns.Add("Documento", 300);
        _fila.Columns.Add("Status", 220);
        inner.Controls.Add(_fila, 0, 0);

        _btnLimpar.Text = "Limpar fila (destravar cupom preso)";
        _btnLimpar.Dock = DockStyle.Fill;
        _btnLimpar.Margin = new Padding(0, 6, 0, 0);
        _btnLimpar.FlatStyle = FlatStyle.Flat;
        _btnLimpar.BackColor = Color.FromArgb(150, 60, 0);
        _btnLimpar.ForeColor = Color.White;
        _btnLimpar.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        _btnLimpar.Click += (s, e) => LimparFila();
        inner.Controls.Add(_btnLimpar, 0, 1);

        box.Controls.Add(inner);
        return box;
    }

    private Control MontarAcoes()
    {
        var wrap = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        wrap.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));   // linha 1 de botoes
        wrap.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));   // linha 2 de botoes
        wrap.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // status

        // linha 1: detectar + salvar
        var l1 = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        l1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        l1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));

        var btnAuto = BotaoAcao("Detectar automaticamente", Color.FromArgb(70, 70, 90));
        btnAuto.Click += (s, e) => DetectarAutomatico();
        var btnSalvar = BotaoAcao("Salvar como impressora do PDV", Color.FromArgb(0, 120, 200));
        btnSalvar.Click += (s, e) => Salvar();
        l1.Controls.Add(btnAuto, 0, 0);
        l1.Controls.Add(btnSalvar, 1, 0);

        // linha 2: testar + padrao do Windows
        var l2 = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        l2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        l2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));

        var btnTeste = BotaoAcao("Imprimir teste (confirma saida)", Color.FromArgb(0, 150, 0));
        btnTeste.Click += async (s, e) =>
        {
            btnTeste.Enabled = false;
            try { ImprimirTeste(); }
            finally { await System.Threading.Tasks.Task.Delay(2000); btnTeste.Enabled = true; }
        };
        var btnPadrao = BotaoAcao("Definir padrao do Windows", Color.FromArgb(90, 90, 90));
        btnPadrao.Click += (s, e) => DefinirPadraoWindows();
        l2.Controls.Add(btnTeste, 0, 0);
        l2.Controls.Add(btnPadrao, 1, 0);

        wrap.Controls.Add(l1, 0, 0);
        wrap.Controls.Add(l2, 0, 1);

        _lblStatus.Dock = DockStyle.Fill;
        _lblStatus.ForeColor = Color.FromArgb(60, 60, 60);
        _lblStatus.TextAlign = ContentAlignment.MiddleLeft;
        wrap.Controls.Add(_lblStatus, 0, 2);
        return wrap;
    }

    private static Button BotaoAcao(string texto, Color cor) => new()
    {
        Text = texto, Dock = DockStyle.Fill, Margin = new Padding(3, 3, 3, 3),
        BackColor = cor, ForeColor = Color.White, FlatStyle = FlatStyle.Flat,
        Font = new Font("Segoe UI", 10F, FontStyle.Bold)
    };

    // ---------------------------------------------------------------- dados

    private void CarregarLista()
    {
        var sel = Selecionada();
        _cmb.Items.Clear();
        foreach (var p in PrinterDiscovery.ListarImpressoras())
            _cmb.Items.Add(p);
        foreach (var com in PrinterDiscovery.ListarPortasCom())
            _cmb.Items.Add(com);

        var atual = sel ?? _servico.ImpressoraPadrao;
        if (string.IsNullOrWhiteSpace(atual))
            atual = PrinterDiscovery.SugerirTermica() ?? "";

        if (!string.IsNullOrWhiteSpace(atual) && _cmb.Items.Contains(atual))
            _cmb.SelectedItem = atual;
        else if (_cmb.Items.Count > 0)
            _cmb.SelectedIndex = 0;

        Info($"Impressora do PDV: {(_servico.ImpressoraPadrao == "" ? "(nenhuma salva)" : _servico.ImpressoraPadrao)}", Color.FromArgb(60, 60, 60));
    }

    private string? Selecionada() => _cmb.SelectedItem?.ToString();

    private void AtualizarTudo()
    {
        AtualizarSemaforo();
        AtualizarDetalhes();
        AtualizarFila();
    }

    /// <summary>Refresh leve (chamado pelo Timer): so status/semaforo e fila, sem recarregar a lista.</summary>
    private void AtualizarStatusEFila()
    {
        AtualizarSemaforo();
        AtualizarFila();
    }

    private void AtualizarSemaforo()
    {
        var nome = Selecionada();
        if (string.IsNullOrWhiteSpace(nome))
        {
            _bolha.BackColor = Color.Gray;
            _lblSemaforo.Text = "Sem impressora";
            _lblDica.Text = "Selecione uma impressora ou porta acima.";
            return;
        }

        var st = PrinterDiscovery.StatusFila(nome);
        (_bolha.BackColor, var rotulo) = st.Nivel switch
        {
            PrinterDiscovery.Semaforo.Verde   => (Color.FromArgb(0, 160, 60), "PRONTA"),
            PrinterDiscovery.Semaforo.Amarelo => (Color.FromArgb(220, 160, 0), "ATENCAO"),
            _                                  => (Color.FromArgb(200, 40, 40), "PARADA"),
        };
        _lblSemaforo.Text = rotulo;
        _lblSemaforo.ForeColor = _bolha.BackColor;
        _lblDica.Text = st.Descricao;
    }

    private void AtualizarDetalhes()
    {
        var nome = Selecionada();
        if (string.IsNullOrWhiteSpace(nome)) { _lblDetalhes.Text = "—"; return; }

        string tipo = nome.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
            ? (nome.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase) ? "Bluetooth (porta serial)" : "Serial (porta COM)")
            : "USB / fila do Windows";

        var d = PrinterDiscovery.DetalhesTecnicos(nome);
        bool ehPadraoPdv = string.Equals(nome, _servico.ImpressoraPadrao, StringComparison.OrdinalIgnoreCase);

        _lblDetalhes.Text =
            $"Tipo    : {tipo}\n" +
            $"Porta   : {d.Porta}    Driver: {d.Driver}\n" +
            $"Padrao  : PDV [{(ehPadraoPdv ? "SIM" : "nao")}]   Windows [{(d.PadraoDoWindows ? "SIM" : "nao")}]";
    }

    private void AtualizarFila()
    {
        var nome = Selecionada();
        var jobs = string.IsNullOrWhiteSpace(nome)
            ? new List<PrinterDiscovery.JobFila>()
            : PrinterDiscovery.ListarFila(nome);

        _fila.BeginUpdate();
        _fila.Items.Clear();
        foreach (var j in jobs)
        {
            var it = new ListViewItem(j.Id.ToString());
            it.SubItems.Add(j.Documento);
            it.SubItems.Add(j.Status);
            if (j.Status.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
                j.Status.Contains("Offline", StringComparison.OrdinalIgnoreCase) ||
                j.Status.Contains("Retained", StringComparison.OrdinalIgnoreCase) ||
                j.Status.Contains("Blocked", StringComparison.OrdinalIgnoreCase))
                it.ForeColor = Color.FromArgb(190, 0, 0);
            _fila.Items.Add(it);
        }
        _fila.EndUpdate();

        bool temFila = jobs.Count > 0;
        _btnLimpar.Enabled = temFila;
        _btnLimpar.Text = temFila
            ? $"Limpar fila ({jobs.Count} pendente{(jobs.Count > 1 ? "s" : "")})"
            : "Limpar fila (vazia)";
    }

    // ---------------------------------------------------------------- acoes

    private void DetectarAutomatico()
    {
        CarregarLista();
        var auto = PrinterDiscovery.SugerirTermica();
        if (string.IsNullOrWhiteSpace(auto))
        {
            Info("Nenhuma impressora termica encontrada. Ligue/conecte e clique Atualizar.", Color.FromArgb(180, 0, 0));
            return;
        }
        if (_cmb.Items.Contains(auto)) _cmb.SelectedItem = auto;
        Info($"Detectada: {auto}. Clique 'Salvar como impressora do PDV' para usar.", Color.FromArgb(0, 120, 0));
        AtualizarTudo();
    }

    private void Salvar()
    {
        var nome = Selecionada();
        if (string.IsNullOrWhiteSpace(nome)) { Info("Selecione uma impressora antes de salvar.", Color.FromArgb(180, 0, 0)); return; }
        _servico.DefinirImpressora(nome);
        Info($"Salvo! Impressora do PDV: {nome}", Color.FromArgb(0, 120, 0));
        AtualizarDetalhes();
    }

    private void DefinirPadraoWindows()
    {
        var nome = Selecionada();
        if (string.IsNullOrWhiteSpace(nome)) { Info("Selecione uma impressora primeiro.", Color.FromArgb(180, 0, 0)); return; }
        if (nome.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
        {
            Info("Portas COM/Bluetooth nao sao 'padrao do Windows' (nao tem fila).", Color.FromArgb(180, 0, 0));
            return;
        }
        bool ok = PrinterDiscovery.DefinirPadraoDoWindows(nome);
        Info(ok ? $"Definida como padrao do Windows: {nome}" : "Nao foi possivel definir como padrao (driver recusou).",
             ok ? Color.FromArgb(0, 120, 0) : Color.FromArgb(180, 0, 0));
        AtualizarDetalhes();
    }

    private void LimparFila()
    {
        var nome = Selecionada();
        if (string.IsNullOrWhiteSpace(nome)) return;
        int n = PrinterDiscovery.LimparFila(nome);
        Info(n > 0 ? $"Fila limpa: {n} trabalho(s) cancelado(s)." : "Nada para limpar na fila.",
             Color.FromArgb(0, 120, 0));
        AtualizarStatusEFila();
    }

    private void ImprimirTeste()
    {
        var nome = Selecionada();
        if (string.IsNullOrWhiteSpace(nome)) { Info("Selecione uma impressora para testar.", Color.FromArgb(180, 0, 0)); return; }

        Info("Testando impressao... aguarde a confirmacao.", Color.FromArgb(90, 90, 90));
        _lblStatus.Refresh();

        var (ok, msg) = EscPosPrinter.ImprimirTeste(nome);
        Info(ok ? "OK — saiu papel! Impressora funcionando." : "Falhou: " + msg,
             ok ? Color.FromArgb(0, 120, 0) : Color.FromArgb(180, 0, 0));
        AtualizarTudo();   // reflete o status apurado (ex.: virou PARADA apos o job travar)
    }

    private void Info(string texto, Color cor)
    {
        _lblStatus.ForeColor = cor;
        _lblStatus.Text = texto;
    }
}
