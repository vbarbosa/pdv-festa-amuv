using System.Reflection;
using System.Runtime.Versioning;

namespace PdvFesta.App;

/// <summary>Janela "Sobre": marca, versao e creditos.</summary>
[SupportedOSPlatform("windows")]
public sealed class FormSobre : Form
{
    public FormSobre()
    {
        Text = "Sobre o PDV Festa";
        Name = "FormSobre";
        Icon = Marca.Icone();
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
        ClientSize = new Size(420, 320);
        Font = new Font("Segoe UI", 11F);
        BackColor = Color.White;

        var versao = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0";

        var logo = new PictureBox
        {
            Dock = DockStyle.Top, Height = 120, SizeMode = PictureBoxSizeMode.Zoom,
            Image = Marca.Logo(), Padding = new Padding(10)
        };

        var txt = new Label
        {
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
            Text =
                "PDV Festa Junina - Terminal Caixa\n" +
                $"Versao {versao}\n\n" +
                "Arraia da AMUV\n" +
                "Sistema de ponto de venda para eventos.\n\n" +
                "Licenca MIT - uso livre.\n" +
                "Feito com carinho para a festa. 🌽🔥"
        };

        var btn = new Button
        {
            Text = "Fechar", Dock = DockStyle.Bottom, Height = 46,
            FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 11F, FontStyle.Bold)
        };
        btn.Click += (s, e) => Close();

        Controls.Add(txt);
        Controls.Add(logo);
        Controls.Add(btn);
    }
}
