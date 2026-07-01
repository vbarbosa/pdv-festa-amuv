using System.Runtime.Versioning;

namespace PdvFesta.App;

/// <summary>
/// PERMISSOES E SENHA (so o admin acessa, com senha). Duas partes:
///  1) Para cada acao protegida, o admin marca se EXIGE SENHA ou fica LIBERADA ao operador.
///  2) Trocar a senha de administrador.
/// Assim o admin delega: o que o operador faz sozinho e o que exige a senha dele.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FormPermissoes : Form
{
    private readonly Servico _servico;
    private readonly Permissoes _perm;
    private readonly Dictionary<AcaoProtegida, CheckBox> _checks = new();

    // troca de senha
    private readonly TextBox _txtAtual = new();
    private readonly TextBox _txtNova = new();
    private readonly TextBox _txtNova2 = new();
    private readonly Label _lblStatus = new();

    public FormPermissoes(Servico servico)
    {
        _servico = servico;
        _perm = new Permissoes(servico);
        Text = "Permissões e Senha do Administrador";
        Name = "FormPermissoes";
        Icon = Marca.Icone();
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(640, 620);
        ClientSize = new Size(680, 680);
        Font = new Font("Segoe UI", 11F);
        KeyPreview = true;

        MontarLayout();
        KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
    }

    private void MontarLayout()
    {
        var titulo = new Label
        {
            Text = "PERMISSÕES DO OPERADOR", Dock = DockStyle.Top, Height = 44,
            TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.White,
            BackColor = Color.FromArgb(60, 60, 60), Font = new Font("Segoe UI", 15F, FontStyle.Bold)
        };

        var explica = new Label
        {
            Text = "Marque as ações que EXIGEM a senha do administrador.\n" +
                   "As desmarcadas ficam LIBERADAS para o operador fazer sozinho.",
            Dock = DockStyle.Top, Height = 48, Padding = new Padding(14, 6, 0, 0),
            ForeColor = Color.FromArgb(70, 70, 70)
        };

        // lista de acoes com checkbox "exige senha"
        var lista = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, Height = 320, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoScroll = true, Padding = new Padding(18, 4, 4, 4)
        };
        foreach (AcaoProtegida a in Enum.GetValues(typeof(AcaoProtegida)))
        {
            if (a == AcaoProtegida.Permissoes) continue;   // esta sempre exige senha (fixo)
            var chk = new CheckBox
            {
                Text = "Exige senha:  " + Permissoes.Rotulo(a),
                Checked = _perm.ExigeSenha(a),
                AutoSize = true, Font = new Font("Segoe UI", 11F), Margin = new Padding(3, 5, 3, 5)
            };
            _checks[a] = chk;
            lista.Controls.Add(chk);
        }

        var btnSalvarPerm = new Button
        {
            Name = "btnSalvarPermissoes",
            Text = "Salvar permissões", Dock = DockStyle.Top, Height = 44,
            BackColor = Color.FromArgb(0, 130, 0), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 12F, FontStyle.Bold)
        };
        btnSalvarPerm.Click += (s, e) => SalvarPermissoes();

        // ---- trocar senha ----
        var sepSenha = new Label
        {
            Text = "TROCAR SENHA DO ADMINISTRADOR", Dock = DockStyle.Top, Height = 34,
            TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(14, 0, 0, 0),
            Font = new Font("Segoe UI", 12F, FontStyle.Bold), ForeColor = Color.FromArgb(60, 60, 60)
        };

        var painelSenha = new TableLayoutPanel { Dock = DockStyle.Top, Height = 150, ColumnCount = 2, Padding = new Padding(14, 4, 14, 4) };
        painelSenha.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        painelSenha.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var t in new[] { _txtAtual, _txtNova, _txtNova2 }) { t.UseSystemPasswordChar = true; t.Dock = DockStyle.Fill; t.Font = new Font("Segoe UI", 13F); }
        LinhaSenha(painelSenha, 0, "Senha atual:", _txtAtual);
        LinhaSenha(painelSenha, 1, "Nova senha:", _txtNova);
        LinhaSenha(painelSenha, 2, "Repita a nova:", _txtNova2);

        var btnSenha = new Button
        {
            Name = "btnTrocarSenha",
            Text = "Trocar senha", Dock = DockStyle.Top, Height = 42,
            BackColor = Color.FromArgb(0, 120, 200), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 12F, FontStyle.Bold)
        };
        btnSenha.Click += (s, e) => TrocarSenha();

        _lblStatus.Dock = DockStyle.Top; _lblStatus.Height = 34;
        _lblStatus.TextAlign = ContentAlignment.MiddleCenter; _lblStatus.Font = new Font("Segoe UI", 11F, FontStyle.Bold);

        // Dock.Top empilha na ordem inversa de adicao -> adiciona de baixo pra cima.
        Controls.Add(_lblStatus);
        Controls.Add(btnSenha);
        Controls.Add(painelSenha);
        Controls.Add(sepSenha);
        Controls.Add(btnSalvarPerm);
        Controls.Add(lista);
        Controls.Add(explica);
        Controls.Add(titulo);
    }

    private static void LinhaSenha(TableLayoutPanel p, int row, string rotulo, TextBox txt)
    {
        p.Controls.Add(new Label { Text = rotulo, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        p.Controls.Add(txt, 1, row);
    }

    private void SalvarPermissoes()
    {
        foreach (var (acao, chk) in _checks)
            _perm.Definir(acao, chk.Checked);
        _lblStatus.ForeColor = Color.FromArgb(0, 130, 0);
        _lblStatus.Text = "Permissões salvas.";
    }

    private void TrocarSenha()
    {
        if (!_servico.ValidarSenhaAdmin(_txtAtual.Text))
        {
            AvisoSenha("Senha atual incorreta.");
            _txtAtual.Focus(); _txtAtual.SelectAll();
            return;
        }
        var nova = _txtNova.Text;
        if (string.IsNullOrWhiteSpace(nova) || nova.Length < 4)
        {
            AvisoSenha("A nova senha deve ter pelo menos 4 caracteres.");
            _txtNova.Focus();
            return;
        }
        if (nova != _txtNova2.Text)
        {
            AvisoSenha("A confirmação não confere com a nova senha.");
            _txtNova2.Focus(); _txtNova2.SelectAll();
            return;
        }
        _servico.DefinirSenhaAdmin(nova);
        _txtAtual.Clear(); _txtNova.Clear(); _txtNova2.Clear();
        _lblStatus.ForeColor = Color.FromArgb(0, 130, 0);
        _lblStatus.Text = "Senha do administrador trocada com sucesso.";
    }

    private void AvisoSenha(string m)
    {
        _lblStatus.ForeColor = Color.FromArgb(180, 0, 0);
        _lblStatus.Text = m;
    }
}
