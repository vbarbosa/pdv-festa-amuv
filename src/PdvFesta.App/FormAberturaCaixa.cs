using System.Runtime.Versioning;
using PdvFesta.Core;

namespace PdvFesta.App;

/// <summary>
/// Abertura de turno de caixa: informa o Troco Inicial (fundo de caixa em dinheiro).
/// Sem um caixa aberto, o sistema nao registra vendas.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FormAberturaCaixa : Form
{
    private readonly Servico _servico;
    private readonly TextBox _txtFundo = new();
    private readonly TextBox _txtOperador = new();

    public FormAberturaCaixa(Servico servico)
    {
        _servico = servico;

        Text = "Abertura de Caixa";
        Name = "FormAberturaCaixa";
        Icon = Marca.Icone();
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        ClientSize = new Size(420, 320);
        Font = new Font("Segoe UI", 11F);
        KeyPreview = true;

        var titulo = new Label
        {
            Text = "ABERTURA DE CAIXA", Dock = DockStyle.Top, Height = 50,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 15F, FontStyle.Bold),
            BackColor = Marca.Vermelho, ForeColor = Color.White
        };

        var lblFundo = new Label
        {
            Text = "Troco inicial / Fundo de caixa (R$):", Dock = DockStyle.Top, Height = 28,
            TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(14, 0, 0, 0)
        };
        _txtFundo.Name = "txtFundo";
        _txtFundo.Dock = DockStyle.Top;
        _txtFundo.Font = new Font("Segoe UI", 22F, FontStyle.Bold);
        _txtFundo.TextAlign = HorizontalAlignment.Right;
        _txtFundo.Text = "0,00";
        _txtFundo.Margin = new Padding(14);

        var lblOp = new Label
        {
            Text = "Operador (opcional):", Dock = DockStyle.Top, Height = 26,
            TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(14, 0, 0, 0)
        };
        _txtOperador.Name = "txtOperador";
        _txtOperador.Dock = DockStyle.Top;
        _txtOperador.Font = new Font("Segoe UI", 13F);

        var btn = new Button
        {
            Name = "btnAbrirCaixa",
            Text = "ABRIR CAIXA (Enter)", Dock = DockStyle.Bottom, Height = 58,
            BackColor = Color.FromArgb(0, 150, 0), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 13F, FontStyle.Bold)
        };
        btn.Click += (s, e) => Abrir();

        // Dock: adiciona de baixo pra cima
        Controls.Add(_txtOperador);
        Controls.Add(lblOp);
        Controls.Add(_txtFundo);
        Controls.Add(lblFundo);
        Controls.Add(titulo);
        Controls.Add(btn);

        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter) { Abrir(); e.Handled = true; }
            else if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
        };
        Shown += (s, e) => { _txtFundo.Focus(); _txtFundo.SelectAll(); };
    }

    private void Abrir()
    {
        var fundo = Dinheiro.ParseCentavos(_txtFundo.Text);
        if (fundo is null)
        {
            MessageBox.Show("Informe um valor valido para o fundo de caixa (ex: 100,00).",
                "Abertura", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtFundo.Focus(); _txtFundo.SelectAll();
            return;
        }
        _servico.AbrirCaixa(fundo.Value, _txtOperador.Text.Trim());
        DialogResult = DialogResult.OK;
        Close();
    }
}
