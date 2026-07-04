using System.Runtime.Versioning;

namespace PdvFesta.App;

/// <summary>
/// PERMISSOES do operador (so o admin acessa, com senha): para cada acao protegida, o admin
/// marca se EXIGE SENHA ou fica LIBERADA ao operador. A troca de senha ficou numa tela
/// separada (<see cref="FormTrocarSenha"/>).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FormPermissoes : Form
{
    private readonly Servico _servico;
    private readonly Permissoes _perm;
    private readonly Dictionary<AcaoProtegida, CheckBox> _checks = new();
    private readonly Label _lblStatus = new();

    public FormPermissoes(Servico servico)
    {
        _servico = servico;
        _perm = new Permissoes(servico);
        Text = "Permissões do Operador";
        Name = "FormPermissoes";
        Icon = Marca.Icone();
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(640, 560);
        ClientSize = new Size(680, 600);
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

        _lblStatus.Dock = DockStyle.Top; _lblStatus.Height = 34;
        _lblStatus.TextAlign = ContentAlignment.MiddleCenter; _lblStatus.Font = new Font("Segoe UI", 11F, FontStyle.Bold);

        // Dock.Top empilha na ordem inversa de adicao -> adiciona de baixo pra cima.
        Controls.Add(_lblStatus);
        Controls.Add(btnSalvarPerm);
        Controls.Add(lista);
        Controls.Add(explica);
        Controls.Add(titulo);
    }

    private void SalvarPermissoes()
    {
        foreach (var (acao, chk) in _checks)
            _perm.Definir(acao, chk.Checked);
        _lblStatus.ForeColor = Color.FromArgb(0, 130, 0);
        _lblStatus.Text = "Permissões salvas.";
    }
}
