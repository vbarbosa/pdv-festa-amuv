using System.Reflection;
using System.Runtime.Versioning;

namespace PdvFesta.App;

/// <summary>Janela "Sobre": marca, versao, recursos e creditos, com layout organizado.</summary>
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
        ClientSize = new Size(460, 500);
        Font = new Font("Segoe UI", 11F);
        BackColor = Color.White;

        var versao = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0";

        // ---- cabecalho colorido com titulo ----
        var cab = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = Marca.Vermelho };
        var titulo = new Label
        {
            Text = "PDV FESTA JUNINA", Dock = DockStyle.Top, Height = 52,
            TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.White,
            Font = new Font("Segoe UI", 20F, FontStyle.Bold)
        };
        var subtitulo = new Label
        {
            Text = "Terminal de Caixa para Eventos", Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.White,
            Font = new Font("Segoe UI", 11F)
        };
        cab.Controls.Add(subtitulo);
        cab.Controls.Add(titulo);

        // ---- logo (se existir) ----
        var logo = new PictureBox
        {
            Dock = DockStyle.Top, Height = 96, SizeMode = PictureBoxSizeMode.Zoom,
            Image = TryLogo(), Padding = new Padding(8, 10, 8, 6)
        };

        // ---- corpo: versao + recursos ----
        var corpo = new Label
        {
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.TopCenter,
            Padding = new Padding(16, 10, 16, 8),
            Text =
                $"Versão {versao}\n" +
                "Arraia da AMUV\n" +
                "\n" +
                "Vendas por teclado, fichas de vales destacáveis,\n" +
                "combos automáticos, controle de caixa e turnos,\n" +
                "impressão térmica 58mm e backup automático.\n" +
                "\n" +
                "Licença MIT — uso livre.\n" +
                "Feito com carinho para a festa. 🌽🔥"
        };

        var btn = new Button
        {
            Text = "Fechar", Dock = DockStyle.Bottom, Height = 48,
            BackColor = Color.FromArgb(60, 60, 60), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 12F, FontStyle.Bold)
        };
        btn.Click += (s, e) => Close();

        // Dock: adiciona de baixo pra cima (Fill por ultimo dos Top).
        Controls.Add(corpo);
        Controls.Add(logo);
        Controls.Add(cab);
        Controls.Add(btn);

        AcceptButton = btn;
        KeyPreview = true;
        KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
    }

    private static Image? TryLogo()
    {
        try { return Marca.Logo(); } catch { return null; }
    }
}
