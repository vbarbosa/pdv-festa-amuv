using System.Runtime.Versioning;
using PdvFesta.Core;

namespace PdvFesta.App;

/// <summary>
/// Perguntinha "qual tipo de cupom?" para a REIMPRESSAO — o operador escolhe na hora, sem
/// ficar preso a config global do Layout do Cupom. Retorna o modo escolhido (ou null se cancelou).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DialogoTipoCupom : Form
{
    private readonly RadioButton _rbCompleto = new() { Text = "Recibo Completo (valores, total, troco)", Dock = DockStyle.Top, Height = 40 };
    private readonly RadioButton _rbFicha = new() { Text = "Ficha de Consumo (1 por unidade)", Dock = DockStyle.Top, Height = 40 };
    private readonly RadioButton _rbVales = new() { Text = "Recibo + Vales destacáveis", Dock = DockStyle.Top, Height = 40 };
    private readonly RadioButton _rbSoVales = new() { Text = "Só Vales destacáveis", Dock = DockStyle.Top, Height = 40 };

    public ModoCupom? Escolhido { get; private set; }

    public DialogoTipoCupom(ModoCupom padrao)
    {
        Text = "Reimprimir — tipo de cupom";
        Icon = Marca.Icone();
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        ClientSize = new Size(420, 320);
        Font = new Font("Segoe UI", 11F);
        KeyPreview = true;

        var titulo = new Label
        {
            Text = "Qual tipo de cupom?", Dock = DockStyle.Top, Height = 44,
            TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.White,
            BackColor = Color.FromArgb(0, 110, 60), Font = new Font("Segoe UI", 13F, FontStyle.Bold)
        };

        // marca o modo atual da config como default
        _rbCompleto.Checked = padrao == ModoCupom.Completo;
        _rbFicha.Checked = padrao == ModoCupom.FichaConsumo;
        _rbVales.Checked = padrao == ModoCupom.ReciboComVales;
        _rbSoVales.Checked = padrao == ModoCupom.SoVales;
        if (!_rbCompleto.Checked && !_rbFicha.Checked && !_rbVales.Checked && !_rbSoVales.Checked)
            _rbVales.Checked = true;

        var painel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 8, 16, 8) };
        // adiciona em ordem inversa (Dock.Top empilha do ultimo pro primeiro)
        painel.Controls.Add(_rbSoVales);
        painel.Controls.Add(_rbVales);
        painel.Controls.Add(_rbFicha);
        painel.Controls.Add(_rbCompleto);

        var barra = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 56, ColumnCount = 2, RowCount = 1 };
        barra.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        barra.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        var btnOk = new Button
        {
            Text = "IMPRIMIR", Dock = DockStyle.Fill, Margin = new Padding(4),
            BackColor = Color.FromArgb(0, 130, 0), ForeColor = Color.White, FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 12F, FontStyle.Bold)
        };
        btnOk.Click += (s, e) => { Escolhido = LerEscolha(); DialogResult = DialogResult.OK; Close(); };
        var btnCancelar = new Button
        {
            Text = "Cancelar", Dock = DockStyle.Fill, Margin = new Padding(4),
            BackColor = Color.FromArgb(120, 120, 120), ForeColor = Color.White, FlatStyle = FlatStyle.Flat
        };
        btnCancelar.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
        barra.Controls.Add(btnOk, 0, 0);
        barra.Controls.Add(btnCancelar, 1, 0);

        Controls.Add(painel);
        Controls.Add(barra);
        Controls.Add(titulo);

        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter) { Escolhido = LerEscolha(); DialogResult = DialogResult.OK; Close(); }
            else if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
        };
    }

    private ModoCupom LerEscolha() =>
        _rbFicha.Checked ? ModoCupom.FichaConsumo
        : _rbVales.Checked ? ModoCupom.ReciboComVales
        : _rbSoVales.Checked ? ModoCupom.SoVales
        : ModoCupom.Completo;
}
