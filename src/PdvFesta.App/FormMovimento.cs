using System.Runtime.Versioning;
using PdvFesta.Core;

namespace PdvFesta.App;

/// <summary>
/// Registro de Sangria (retirada) ou Suprimento (entrada) de dinheiro na gaveta,
/// dentro do turno atual. Ajuda a bater o "Total em Gaveta" no fechamento.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FormMovimento : Form
{
    private readonly Servico _servico;
    private readonly TipoMovimento _tipo;
    private readonly TextBox _txtValor = new();
    private readonly TextBox _txtMotivo = new();

    public FormMovimento(Servico servico, TipoMovimento tipo)
    {
        _servico = servico;
        _tipo = tipo;

        bool sangria = tipo == TipoMovimento.Sangria;
        Text = sangria ? "Sangria (retirada)" : "Suprimento (entrada)";
        Name = "FormMovimento";
        Icon = Marca.Icone();
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        ClientSize = new Size(420, 300);
        Font = new Font("Segoe UI", 11F);
        KeyPreview = true;

        var titulo = new Label
        {
            Text = sangria ? "SANGRIA - RETIRADA" : "SUPRIMENTO - ENTRADA",
            Dock = DockStyle.Top, Height = 48, TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 14F, FontStyle.Bold), ForeColor = Color.White,
            BackColor = sangria ? Color.FromArgb(180, 60, 60) : Color.FromArgb(0, 130, 70)
        };

        var lblValor = new Label
        {
            Text = "Valor (R$):", Dock = DockStyle.Top, Height = 26,
            TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(14, 0, 0, 0)
        };
        _txtValor.Name = "txtValorMov";
        _txtValor.Dock = DockStyle.Top;
        _txtValor.Font = new Font("Segoe UI", 22F, FontStyle.Bold);
        _txtValor.TextAlign = HorizontalAlignment.Right;
        _txtValor.Text = "0,00";

        var lblMotivo = new Label
        {
            Text = "Motivo (opcional):", Dock = DockStyle.Top, Height = 26,
            TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(14, 0, 0, 0)
        };
        _txtMotivo.Name = "txtMotivoMov";
        _txtMotivo.Dock = DockStyle.Top;
        _txtMotivo.Font = new Font("Segoe UI", 13F);

        var btn = new Button
        {
            Name = "btnConfirmarMov",
            Text = "CONFIRMAR (Enter)", Dock = DockStyle.Bottom, Height = 56,
            BackColor = Color.FromArgb(0, 120, 200), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 12F, FontStyle.Bold)
        };
        btn.Click += (s, e) => Confirmar();

        Controls.Add(_txtMotivo);
        Controls.Add(lblMotivo);
        Controls.Add(_txtValor);
        Controls.Add(lblValor);
        Controls.Add(titulo);
        Controls.Add(btn);

        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter) { Confirmar(); e.Handled = true; }
            else if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
        };
        Shown += (s, e) => { _txtValor.Focus(); _txtValor.SelectAll(); };
    }

    private void Confirmar()
    {
        var valor = Dinheiro.ParseCentavos(_txtValor.Text);
        if (valor is null or 0)
        {
            MessageBox.Show("Informe um valor valido (maior que zero).", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtValor.Focus(); _txtValor.SelectAll();
            return;
        }
        _servico.RegistrarMovimento(_tipo, valor.Value, _txtMotivo.Text.Trim());
        DialogResult = DialogResult.OK;
        Close();
    }
}
