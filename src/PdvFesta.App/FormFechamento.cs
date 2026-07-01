using System.Runtime.Versioning;
using PdvFesta.Core;

namespace PdvFesta.App;

/// <summary>
/// Dashboard do tesoureiro: total por forma de pagamento + faturamento bruto,
/// atualizado em tempo real. Inclui Exportar/Importar backup (dump do banco)
/// para continuar a festa em outro PC se um caixa quebrar.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FormFechamento : Form
{
    private readonly Servico _servico;
    private readonly Label _lblDinheiro = new();
    private readonly Label _lblPix = new();
    private readonly Label _lblCartao = new();
    private readonly Label _lblBruto = new();
    private readonly Label _lblQtd = new();

    public FormFechamento(Servico servico)
    {
        _servico = servico;
        Text = "Fechamento de Caixa - Tesoureiro";
        Icon = Marca.Icone();
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        ClientSize = new Size(500, 480);
        Font = new Font("Segoe UI", 12F);

        MontarLayout();
        Atualizar();
    }

    private void MontarLayout()
    {
        var titulo = new Label
        {
            Text = "RESUMO DO CAIXA", Dock = DockStyle.Top, Height = 50,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            BackColor = Color.FromArgb(60, 60, 60), ForeColor = Color.White
        };

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top, Height = 250, ColumnCount = 2, RowCount = 5, Padding = new Padding(20)
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));

        AddLinha(grid, 0, "Dinheiro:", _lblDinheiro, Color.FromArgb(0, 120, 0));
        AddLinha(grid, 1, "Pix:", _lblPix, Color.FromArgb(0, 100, 160));
        AddLinha(grid, 2, "Cartao:", _lblCartao, Color.FromArgb(120, 60, 160));
        AddLinha(grid, 3, "Nº de vendas:", _lblQtd, Color.FromArgb(60, 60, 60));

        var lblBrutoCab = new Label
        {
            Text = "FATURAMENTO:", Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill
        };
        _lblBruto.Font = new Font("Segoe UI", 22F, FontStyle.Bold);
        _lblBruto.ForeColor = Color.FromArgb(0, 130, 0);
        _lblBruto.TextAlign = ContentAlignment.MiddleRight; _lblBruto.Dock = DockStyle.Fill;
        grid.Controls.Add(lblBrutoCab, 0, 4);
        grid.Controls.Add(_lblBruto, 1, 4);

        // Botoes: atualizar + backup
        var barra = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 140, Padding = new Padding(20), FlowDirection = FlowDirection.TopDown };

        var btnAtualizar = BotaoLargo("Atualizar (tempo real)", Color.FromArgb(70, 70, 90));
        btnAtualizar.Click += (s, e) => Atualizar();

        var btnExportar = BotaoLargo("Exportar backup (.json)", Color.FromArgb(0, 120, 200));
        btnExportar.Click += (s, e) => Exportar();

        var btnImportar = BotaoLargo("Importar backup (outro PC)", Color.FromArgb(180, 100, 0));
        btnImportar.Click += (s, e) => Importar();

        barra.Controls.Add(btnAtualizar);
        barra.Controls.Add(btnExportar);
        barra.Controls.Add(btnImportar);

        Controls.Add(barra);
        Controls.Add(grid);
        Controls.Add(titulo);
    }

    private static void AddLinha(TableLayoutPanel grid, int row, string rotulo, Label valor, Color cor)
    {
        var lbl = new Label { Text = rotulo, Font = new Font("Segoe UI", 14F), TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill };
        valor.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        valor.ForeColor = cor; valor.TextAlign = ContentAlignment.MiddleRight; valor.Dock = DockStyle.Fill;
        grid.Controls.Add(lbl, 0, row);
        grid.Controls.Add(valor, 1, row);
    }

    private static Button BotaoLargo(string texto, Color cor) => new()
    {
        Text = texto, Width = 440, Height = 38, Margin = new Padding(0, 0, 0, 6),
        BackColor = cor, ForeColor = Color.White, FlatStyle = FlatStyle.Flat,
        Font = new Font("Segoe UI", 11F, FontStyle.Bold)
    };

    private void Atualizar()
    {
        var r = _servico.Fechamento();
        _lblDinheiro.Text = CupomFormatter.Moeda(r.TotalDinheiroCentavos);
        _lblPix.Text = CupomFormatter.Moeda(r.TotalPixCentavos);
        _lblCartao.Text = CupomFormatter.Moeda(r.TotalCartaoCentavos);
        _lblQtd.Text = r.QuantidadeVendas.ToString();
        _lblBruto.Text = CupomFormatter.Moeda(r.FaturamentoBrutoCentavos);
    }

    private void Exportar()
    {
        using var dlg = new SaveFileDialog
        {
            Filter = "Backup PDV (*.json)|*.json",
            FileName = $"backup-festa-{DateTime.Now:yyyyMMdd-HHmm}.json"
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            BackupService.ExportarParaArquivo(_servico.Repo, dlg.FileName);
            MessageBox.Show("Backup salvo com sucesso!", "Backup", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Erro ao exportar: " + ex.Message, "Backup", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void Importar()
    {
        var confirma = MessageBox.Show(
            "Importar um backup vai SUBSTITUIR as vendas e o cardapio atuais deste PC.\n\nContinuar?",
            "Importar backup", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirma != DialogResult.Yes) return;

        using var dlg = new OpenFileDialog { Filter = "Backup PDV (*.json)|*.json" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            BackupService.ImportarDeArquivo(_servico.Repo, dlg.FileName);
            Atualizar();
            MessageBox.Show("Backup importado! O caixa continua deste ponto.", "Backup",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Erro ao importar: " + ex.Message, "Backup", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
