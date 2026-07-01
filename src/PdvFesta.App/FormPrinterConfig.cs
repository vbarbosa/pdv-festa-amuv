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

    public FormPrinterConfig(Servico servico)
    {
        _servico = servico;

        Text = "Configuração da Impressora";
        Name = "FormPrinterConfig";
        Icon = Marca.Icone();
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        ClientSize = new Size(560, 300);
        Font = new Font("Segoe UI", 11F);

        MontarLayout();
        CarregarLista();
    }

    private void MontarLayout()
    {
        var raiz = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, Padding = new Padding(16)
        };
        raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 78));
        raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

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
        raiz.Controls.Add(barra, 0, 2);
        raiz.SetColumnSpan(barra, 2);

        _lblStatus.Dock = DockStyle.Fill;
        _lblStatus.ForeColor = Color.FromArgb(60, 60, 60);
        raiz.Controls.Add(_lblStatus, 0, 3);
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
