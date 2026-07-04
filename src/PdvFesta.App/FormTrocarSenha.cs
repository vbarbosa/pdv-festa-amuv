using System.Runtime.Versioning;

namespace PdvFesta.App;

/// <summary>
/// Trocar a SENHA DE ADMINISTRADOR (tela dedicada, separada das permissões). Pede a senha
/// atual, a nova (mín. 4 caracteres) e a confirmação.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FormTrocarSenha : Form
{
    private readonly Servico _servico;
    private readonly TextBox _txtAtual = new();
    private readonly TextBox _txtNova = new();
    private readonly TextBox _txtNova2 = new();
    private readonly Label _lblStatus = new();

    public FormTrocarSenha(Servico servico)
    {
        _servico = servico;
        Text = "Trocar Senha do Administrador";
        Name = "FormTrocarSenha";
        Icon = Marca.Icone();
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        ClientSize = new Size(460, 320);
        Font = new Font("Segoe UI", 11F);
        KeyPreview = true;

        var titulo = new Label
        {
            Text = "TROCAR SENHA DO ADMINISTRADOR", Dock = DockStyle.Top, Height = 44,
            TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.White,
            BackColor = Color.FromArgb(0, 120, 200), Font = new Font("Segoe UI", 13F, FontStyle.Bold)
        };

        var painel = new TableLayoutPanel { Dock = DockStyle.Top, Height = 160, ColumnCount = 2, Padding = new Padding(16, 12, 16, 4) };
        painel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        painel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var t in new[] { _txtAtual, _txtNova, _txtNova2 })
        { t.UseSystemPasswordChar = true; t.Dock = DockStyle.Fill; t.Font = new Font("Segoe UI", 13F); }
        Linha(painel, 0, "Senha atual:", _txtAtual);
        Linha(painel, 1, "Nova senha:", _txtNova);
        Linha(painel, 2, "Repita a nova:", _txtNova2);

        var btn = new Button
        {
            Name = "btnTrocarSenha",
            Text = "Trocar senha (Enter)", Dock = DockStyle.Top, Height = 46,
            BackColor = Color.FromArgb(0, 120, 200), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 12F, FontStyle.Bold)
        };
        btn.Click += (s, e) => TrocarSenha();

        _lblStatus.Dock = DockStyle.Top; _lblStatus.Height = 34;
        _lblStatus.TextAlign = ContentAlignment.MiddleCenter; _lblStatus.Font = new Font("Segoe UI", 11F, FontStyle.Bold);

        Controls.Add(_lblStatus);
        Controls.Add(btn);
        Controls.Add(painel);
        Controls.Add(titulo);

        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter) { TrocarSenha(); e.Handled = true; }
            else if (e.KeyCode == Keys.Escape) Close();
        };
        Shown += (s, e) => _txtAtual.Focus();
    }

    private static void Linha(TableLayoutPanel p, int row, string rotulo, TextBox txt)
    {
        p.Controls.Add(new Label { Text = rotulo, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        p.Controls.Add(txt, 1, row);
    }

    private void TrocarSenha()
    {
        if (!_servico.ValidarSenhaAdmin(_txtAtual.Text))
        {
            Aviso("Senha atual incorreta."); _txtAtual.Focus(); _txtAtual.SelectAll(); return;
        }
        var nova = _txtNova.Text;
        if (string.IsNullOrWhiteSpace(nova) || nova.Length < 4)
        {
            Aviso("A nova senha deve ter pelo menos 4 caracteres."); _txtNova.Focus(); return;
        }
        if (nova != _txtNova2.Text)
        {
            Aviso("A confirmação não confere com a nova senha."); _txtNova2.Focus(); _txtNova2.SelectAll(); return;
        }
        _servico.DefinirSenhaAdmin(nova);
        _txtAtual.Clear(); _txtNova.Clear(); _txtNova2.Clear();
        _lblStatus.ForeColor = Color.FromArgb(0, 130, 0);
        _lblStatus.Text = "Senha trocada com sucesso!";
    }

    private void Aviso(string m)
    {
        _lblStatus.ForeColor = Color.FromArgb(180, 0, 0);
        _lblStatus.Text = m;
    }
}
