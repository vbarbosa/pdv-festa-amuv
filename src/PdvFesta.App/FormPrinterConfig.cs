using System.Runtime.Versioning;
using PdvFesta.Core;

namespace PdvFesta.App;

/// <summary>
/// Gestao da impressora termica (F12). Lista impressoras + portas COM, salva a
/// escolhida no SQLite e imprime um cupom de teste (ESC/POS RAW + corte).
/// Layout 100% ancorado (Dock/TableLayoutPanel), sem coordenadas fixas.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FormPrinterConfig : Form
{
    private readonly Servico _servico;
    private readonly ComboBox _cmb = new();
    private readonly Label _lblStatus = new();
    private readonly Label _lblInfo = new();

    public FormPrinterConfig(Servico servico)
    {
        _servico = servico;

        Text = "Configuração da Impressora";
        Name = "FormPrinterConfig";
        Icon = Marca.Icone();
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        ClientSize = new Size(580, 420);
        Font = new Font("Segoe UI", 11F);

        MontarLayout();
        CarregarLista();
        _cmb.SelectedIndexChanged += (s, e) => AtualizarInfo();
        AtualizarInfo();
    }

    private void MontarLayout()
    {
        var raiz = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 6, Padding = new Padding(16)
        };
        raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 78));
        raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));   // rotulo
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));   // combo + atualizar
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));   // detectar automatico
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));  // painel de info
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));   // salvar + teste
        raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // status

        var lbl = new Label { Text = "Impressora / Porta:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        raiz.Controls.Add(lbl, 0, 0);
        raiz.SetColumnSpan(lbl, 2);

        _cmb.Dock = DockStyle.Fill;
        _cmb.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmb.Font = new Font("Segoe UI", 12F);
        raiz.Controls.Add(_cmb, 0, 1);

        var btnAtualizar = new Button { Text = "Atualizar", Dock = DockStyle.Fill, Margin = new Padding(6, 0, 0, 0) };
        btnAtualizar.Click += (s, e) => CarregarLista();
        raiz.Controls.Add(btnAtualizar, 1, 1);

        // botao de deteccao automatica (plug-and-play): acha a termica sozinho (USB > Bluetooth)
        var btnAuto = new Button
        {
            Text = "Detectar impressora automaticamente", Dock = DockStyle.Fill, Margin = new Padding(0, 2, 0, 4),
            BackColor = Color.FromArgb(70, 70, 90), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10F, FontStyle.Bold)
        };
        btnAuto.Click += (s, e) => DetectarAutomatico();
        raiz.Controls.Add(btnAuto, 0, 2);
        raiz.SetColumnSpan(btnAuto, 2);

        // painel de INFO da impressora selecionada (tipo, status, se e a padrao)
        _lblInfo.Dock = DockStyle.Fill;
        _lblInfo.TextAlign = ContentAlignment.TopLeft;
        _lblInfo.BackColor = Color.FromArgb(245, 245, 245);
        _lblInfo.Padding = new Padding(10, 8, 8, 8);
        _lblInfo.Font = new Font("Segoe UI", 10F);
        raiz.Controls.Add(_lblInfo, 0, 3);
        raiz.SetColumnSpan(_lblInfo, 2);

        var barra = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        barra.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        barra.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        var btnSalvar = new Button
        {
            Text = "Salvar Configuração", Dock = DockStyle.Fill, Margin = new Padding(0, 8, 6, 8),
            BackColor = Color.FromArgb(0, 120, 200), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 12F, FontStyle.Bold)
        };
        btnSalvar.Click += (s, e) => Salvar();

        var btnTeste = new Button
        {
            Text = "Imprimir Teste", Dock = DockStyle.Fill, Margin = new Padding(6, 8, 0, 8),
            BackColor = Color.FromArgb(0, 150, 0), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 12F, FontStyle.Bold)
        };
        btnTeste.Click += (s, e) => ImprimirTeste();

        barra.Controls.Add(btnSalvar, 0, 0);
        barra.Controls.Add(btnTeste, 1, 0);
        raiz.Controls.Add(barra, 0, 4);
        raiz.SetColumnSpan(barra, 2);

        _lblStatus.Dock = DockStyle.Fill;
        _lblStatus.ForeColor = Color.FromArgb(60, 60, 60);
        raiz.Controls.Add(_lblStatus, 0, 5);
        raiz.SetColumnSpan(_lblStatus, 2);

        Controls.Add(raiz);
    }

    private void CarregarLista()
    {
        _cmb.Items.Clear();
        foreach (var p in PrinterDiscovery.ListarImpressoras())
            _cmb.Items.Add(p);
        foreach (var com in PrinterDiscovery.ListarPortasCom())
            _cmb.Items.Add(com);

        var atual = _servico.ImpressoraPadrao;
        if (string.IsNullOrWhiteSpace(atual))
            atual = PrinterDiscovery.SugerirTermica() ?? "";

        if (!string.IsNullOrWhiteSpace(atual) && _cmb.Items.Contains(atual))
            _cmb.SelectedItem = atual;
        else if (_cmb.Items.Count > 0)
            _cmb.SelectedIndex = 0;

        _lblStatus.Text = $"Impressora atual salva: {(_servico.ImpressoraPadrao == "" ? "(nenhuma)" : _servico.ImpressoraPadrao)}";
    }

    private string? Selecionada() => _cmb.SelectedItem?.ToString();

    /// <summary>Mostra detalhes da impressora selecionada: tipo (USB/Bluetooth), status, padrao.</summary>
    private void AtualizarInfo()
    {
        var nome = Selecionada();
        if (string.IsNullOrWhiteSpace(nome)) { _lblInfo.Text = "Selecione uma impressora acima."; return; }

        // tipo pela forma do alvo: "COMx (Bluetooth)"/"(serial)" = porta; senao fila USB/Windows.
        string tipo;
        if (nome.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
            tipo = nome.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase) ? "Bluetooth (porta serial)" : "Serial (porta COM)";
        else
            tipo = "USB / fila do Windows";

        bool online = PrinterDiscovery.EstaOnline(nome);
        bool ehPadrao = string.Equals(nome, _servico.ImpressoraPadrao, StringComparison.OrdinalIgnoreCase);

        _lblInfo.Text =
            $"Tipo: {tipo}\n" +
            $"Status: {(online ? "PRONTA (online)" : "OFFLINE — verifique cabo/energia")}\n" +
            $"Impressora padrão do sistema: {(ehPadrao ? "SIM (esta)" : "nao")}";
        _lblInfo.ForeColor = online ? Color.FromArgb(40, 40, 40) : Color.FromArgb(180, 0, 0);
    }

    /// <summary>Detecta a termica automaticamente (USB tem prioridade sobre Bluetooth) e seleciona.</summary>
    private void DetectarAutomatico()
    {
        CarregarLista();
        var auto = PrinterDiscovery.SugerirTermica();
        if (string.IsNullOrWhiteSpace(auto))
        {
            _lblStatus.ForeColor = Color.FromArgb(180, 0, 0);
            _lblStatus.Text = "Nenhuma impressora termica encontrada. Ligue/conecte e clique Atualizar.";
            return;
        }
        if (_cmb.Items.Contains(auto)) _cmb.SelectedItem = auto;
        _lblStatus.ForeColor = Color.FromArgb(0, 120, 0);
        _lblStatus.Text = $"Detectada: {auto}. Clique 'Salvar Configuração' para usar.";
        AtualizarInfo();
    }

    private void Salvar()
    {
        var nome = Selecionada();
        if (string.IsNullOrWhiteSpace(nome))
        {
            _lblStatus.ForeColor = Color.FromArgb(180, 0, 0);
            _lblStatus.Text = "Selecione uma impressora antes de salvar.";
            return;
        }
        _servico.DefinirImpressora(nome);
        _lblStatus.ForeColor = Color.FromArgb(0, 120, 0);
        _lblStatus.Text = $"Salvo! Impressora padrão: {nome}";
    }

    private void ImprimirTeste()
    {
        var nome = Selecionada();
        if (string.IsNullOrWhiteSpace(nome))
        {
            _lblStatus.ForeColor = Color.FromArgb(180, 0, 0);
            _lblStatus.Text = "Selecione uma impressora para testar.";
            return;
        }
        var (ok, msg) = EscPosPrinter.ImprimirTeste(nome);
        _lblStatus.ForeColor = ok ? Color.FromArgb(0, 120, 0) : Color.FromArgb(180, 0, 0);
        _lblStatus.Text = ok ? "Teste enviado! Confira o papel." : "Falha: " + msg;
    }
}
