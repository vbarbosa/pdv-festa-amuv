using System.Runtime.Versioning;
using PdvFesta.Core;

namespace PdvFesta.App;

/// <summary>
/// Central de seguranca dos dados (disaster recovery). Janela destacavel:
///  - Escolhe pasta de backup secundaria (ex: OneDrive/Drive).
///  - Auto-backup a cada N minutos (thread em background).
///  - "Gerar Backup Agora" -> .zip com timestamp.
///  - "Restaurar Backup" -> seleciona .db/.zip e substitui o banco, reiniciando o app.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FormBackup : Form
{
    private readonly Servico _servico;
    private readonly TextBox _txtPasta = new();
    private readonly NumericUpDown _numMin = new();
    private readonly Label _lblStatus = new();

    public FormBackup(Servico servico)
    {
        _servico = servico;
        Text = "Segurança de Dados - Backup e Restauração";
        Icon = Marca.Icone();
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        ClientSize = new Size(560, 400);
        Font = new Font("Segoe UI", 11F);

        MontarLayout();
        Carregar();
    }

    private void MontarLayout()
    {
        var titulo = new Label
        {
            Text = "SEGURANÇA DE DADOS", Dock = DockStyle.Top, Height = 44,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 15F, FontStyle.Bold),
            BackColor = Color.FromArgb(60, 60, 60), ForeColor = Color.White
        };

        // --- Pasta secundaria ---
        var lblPasta = new Label { Text = "Pasta de backup secundária (ex: OneDrive):", Location = new Point(20, 60), AutoSize = true };
        _txtPasta.Location = new Point(20, 88); _txtPasta.Width = 420; _txtPasta.ReadOnly = true;
        var btnEscolher = new Button { Text = "Escolher...", Location = new Point(450, 86), Width = 90, Height = 28 };
        btnEscolher.Click += (s, e) => EscolherPasta();

        // --- Intervalo auto-backup ---
        var lblInt = new Label { Text = "Backup automático a cada (min, 0 = desligado):", Location = new Point(20, 130), AutoSize = true };
        _numMin.Location = new Point(20, 158); _numMin.Width = 100; _numMin.Minimum = 0; _numMin.Maximum = 240;
        _numMin.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
        var btnAplicar = new Button { Text = "Aplicar intervalo", Location = new Point(140, 156), Width = 160, Height = 30 };
        btnAplicar.Click += (s, e) => AplicarIntervalo();

        // --- Botoes grandes ---
        var btnAgora = new Button
        {
            Text = "Gerar Backup Agora (.zip)", Location = new Point(20, 210), Width = 260, Height = 60,
            BackColor = Color.FromArgb(0, 120, 200), ForeColor = Color.White, FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 12F, FontStyle.Bold)
        };
        btnAgora.Click += (s, e) => BackupAgora();

        var btnRestaurar = new Button
        {
            Text = "Restaurar Backup", Location = new Point(290, 210), Width = 250, Height = 60,
            BackColor = Color.FromArgb(180, 100, 0), ForeColor = Color.White, FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 12F, FontStyle.Bold)
        };
        btnRestaurar.Click += (s, e) => Restaurar();

        _lblStatus.Location = new Point(20, 290); _lblStatus.Size = new Size(520, 90);
        _lblStatus.ForeColor = Color.FromArgb(60, 60, 60);

        Controls.AddRange(new Control[]
        {
            titulo, lblPasta, _txtPasta, btnEscolher, lblInt, _numMin, btnAplicar,
            btnAgora, btnRestaurar, _lblStatus
        });
    }

    private void Carregar()
    {
        _txtPasta.Text = _servico.PastaBackup;
        _numMin.Value = Math.Clamp(_servico.IntervaloBackupMin, 0, 240);
        _lblStatus.Text = $"Banco atual:\n{_servico.CaminhoBanco}";
    }

    private void EscolherPasta()
    {
        using var dlg = new FolderBrowserDialog { Description = "Escolha a pasta de backup (ex: OneDrive/Drive)" };
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _txtPasta.Text = dlg.SelectedPath;
            _servico.DefinirPastaBackup(dlg.SelectedPath);
            _lblStatus.ForeColor = Color.FromArgb(0, 120, 0);
            _lblStatus.Text = "Pasta de backup salva:\n" + dlg.SelectedPath;
        }
    }

    private void AplicarIntervalo()
    {
        _servico.DefinirIntervaloBackup((int)_numMin.Value);
        _lblStatus.ForeColor = Color.FromArgb(0, 120, 0);
        _lblStatus.Text = _numMin.Value == 0
            ? "Backup automático DESLIGADO."
            : $"Backup automático a cada {_numMin.Value} min ATIVADO.";
    }

    private void BackupAgora()
    {
        try
        {
            // se tiver pasta secundaria, salva la; senao pergunta onde salvar
            string destinoDir = _servico.PastaBackup;
            if (string.IsNullOrWhiteSpace(destinoDir))
            {
                using var dlg = new FolderBrowserDialog { Description = "Onde salvar o backup?" };
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                destinoDir = dlg.SelectedPath;
            }
            var zip = BackupManager.GerarZip(_servico.CaminhoBanco, destinoDir);
            _lblStatus.ForeColor = Color.FromArgb(0, 120, 0);
            _lblStatus.Text = "Backup gerado:\n" + zip;
        }
        catch (Exception ex)
        {
            _lblStatus.ForeColor = Color.FromArgb(180, 0, 0);
            _lblStatus.Text = "Erro no backup: " + ex.Message;
        }
    }

    private void Restaurar()
    {
        var confirma = MessageBox.Show(
            "Restaurar vai SUBSTITUIR o banco atual pelo backup selecionado.\n" +
            "O banco atual será guardado como .bak antes.\n\nO programa será reiniciado. Continuar?",
            "Restaurar Backup", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirma != DialogResult.Yes) return;

        using var dlg = new OpenFileDialog
        {
            Filter = "Backup (*.zip;*.db)|*.zip;*.db|Zip (*.zip)|*.zip|Banco (*.db)|*.db"
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            BackupManager.RestaurarAuto(dlg.FileName, _servico.CaminhoBanco);
            MessageBox.Show("Backup restaurado! O programa vai reiniciar.", "Restauração",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            Application.Restart();
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            _lblStatus.ForeColor = Color.FromArgb(180, 0, 0);
            _lblStatus.Text = "Erro ao restaurar: " + ex.Message;
        }
    }
}
