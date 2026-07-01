using System.Runtime.Versioning;

namespace PdvFesta.App;

/// <summary>
/// Prompt simples de Senha de Administrador. Bloqueia o operador de caixa de
/// acessar telas sensiveis (precos, fechamento, config). Senha padrao: 0000.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FormSenhaAdmin : Form
{
    private readonly Servico _servico;
    private readonly TextBox _txt = new();
    private readonly Label _lblErro = new();

    public FormSenhaAdmin(Servico servico)
    {
        _servico = servico;

        Text = "Acesso restrito";
        Name = "FormSenhaAdmin";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
        ClientSize = new Size(360, 200);
        Font = new Font("Segoe UI", 11F);
        KeyPreview = true;

        var lbl = new Label
        {
            Text = "Digite a senha de administrador:", Dock = DockStyle.Top, Height = 40,
            TextAlign = ContentAlignment.MiddleCenter
        };

        _txt.Name = "txtSenhaAdmin";
        _txt.Dock = DockStyle.Top;
        _txt.UseSystemPasswordChar = true;
        _txt.TextAlign = HorizontalAlignment.Center;
        _txt.Font = new Font("Segoe UI", 22F, FontStyle.Bold);
        _txt.Margin = new Padding(20);

        _lblErro.Dock = DockStyle.Top; _lblErro.Height = 26;
        _lblErro.ForeColor = Color.FromArgb(180, 0, 0);
        _lblErro.TextAlign = ContentAlignment.MiddleCenter;

        var btn = new Button
        {
            Name = "btnConfirmarSenha",
            Text = "Entrar (Enter)", Dock = DockStyle.Bottom, Height = 52,
            BackColor = Color.FromArgb(0, 120, 200), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 12F, FontStyle.Bold)
        };
        btn.Click += (s, e) => Validar();

        Controls.Add(_lblErro);
        Controls.Add(_txt);
        Controls.Add(lbl);
        Controls.Add(btn);

        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter) { Validar(); e.Handled = true; }
            else if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
        };

        Shown += (s, e) => _txt.Focus();
    }

    private void Validar()
    {
        if (_servico.ValidarSenhaAdmin(_txt.Text))
        {
            DialogResult = DialogResult.OK;
            Close();
        }
        else
        {
            _lblErro.Text = "Senha incorreta.";
            _txt.SelectAll();
            _txt.Focus();
        }
    }
}
