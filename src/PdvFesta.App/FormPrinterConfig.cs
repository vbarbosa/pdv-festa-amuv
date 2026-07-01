using System.Runtime.Versioning;
using PdvFesta.Core;

namespace PdvFesta.App;

/// <summary>
/// Janela flutuante de gestao da impressora termica (aberta com F12).
///  - Lista impressoras instaladas + portas COM ativas.
///  - Salva a escolhida no SQLite (persistente entre aberturas).
///  - Botao "Imprimir Teste" que manda ESC/POS RAW (Status OK) + corte.
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

        Text = "Configuracao da Impressora";
        Icon = Marca.Icone();
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;   // flutuante/destacavel
        MaximizeBox = false; MinimizeBox = false;
        ClientSize = new Size(520, 260);
        Font = new Font("Segoe UI", 11F);

        MontarLayout();
        CarregarLista();
    }

    private void MontarLayout()
    {
        var lbl = new Label
        {
            Text = "Impressora / Porta:", Location = new Point(20, 20),
            AutoSize = true
        };
        _cmb.Location = new Point(20, 50);
        _cmb.Width = 380;
        _cmb.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmb.Font = new Font("Segoe UI", 12F);

        var btnAtualizar = new Button
        {
            Text = "Atualizar", Location = new Point(410, 49), Width = 90, Height = 30
        };
        btnAtualizar.Click += (s, e) => CarregarLista();

        var btnSalvar = new Button
        {
            Text = "Salvar Configuracao", Location = new Point(20, 100), Width = 230, Height = 55,
            BackColor = Color.FromArgb(0, 120, 200), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 12F, FontStyle.Bold)
        };
        btnSalvar.Click += (s, e) => Salvar();

        var btnTeste = new Button
        {
            Text = "Imprimir Teste", Location = new Point(270, 100), Width = 230, Height = 55,
            BackColor = Color.FromArgb(0, 150, 0), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 12F, FontStyle.Bold)
        };
        btnTeste.Click += (s, e) => ImprimirTeste();

        _lblStatus.Location = new Point(20, 175);
        _lblStatus.Size = new Size(480, 60);
        _lblStatus.ForeColor = Color.FromArgb(60, 60, 60);

        Controls.AddRange(new Control[] { lbl, _cmb, btnAtualizar, btnSalvar, btnTeste, _lblStatus });
    }

    private void CarregarLista()
    {
        _cmb.Items.Clear();
        foreach (var p in PrinterDiscovery.ListarImpressoras())
            _cmb.Items.Add(p);
        foreach (var com in PrinterDiscovery.ListarPortasCom())
            _cmb.Items.Add(com); // portas COM ao final

        // seleciona a impressora salva; senao, tenta sugerir uma termica
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
        _lblStatus.Text = $"Salvo! Impressora padrao: {nome}";
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
