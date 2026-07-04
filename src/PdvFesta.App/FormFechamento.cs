using System.Globalization;
using System.Runtime.Versioning;
using System.Text;
using PdvFesta.Core;

namespace PdvFesta.App;

/// <summary>
/// Fluxo e fechamento de caixa (turno atual). Dashboard em tempo real:
/// fundo inicial, entradas por forma de pagamento e o "Total em Gaveta".
/// Permite Sangria/Suprimento, imprimir a Leitura Z, exportar CSV (Excel) e
/// fechar o turno. Tambem mantem o backup .json (disaster recovery).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FormFechamento : Form
{
    private readonly Servico _servico;

    private readonly Label _lblFundo = new();
    private readonly Label _lblDinheiro = new();
    private readonly Label _lblPix = new();
    private readonly Label _lblDebito = new();
    private readonly Label _lblCredito = new();
    private readonly Label _lblBruto = new();
    private readonly Label _lblGaveta = new();
    private readonly Label _lblQtd = new();
    private readonly DataGridView _grid = new();
    private readonly GraficoBarras _grafPagamentos = new();
    private readonly GraficoBarras _grafItens = new();

    public FormFechamento(Servico servico)
    {
        _servico = servico;
        Text = "Fechamento de Caixa - Tesoureiro";
        Icon = Marca.Icone();
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(560, 700);
        ClientSize = new Size(600, 780);
        Font = new Font("Segoe UI", 12F);
        KeyPreview = true;

        MontarLayout();
        Atualizar();

        KeyDown += (s, e) => { if (e.KeyCode == Keys.F5) Atualizar(); else if (e.KeyCode == Keys.Escape) Close(); };
    }

    private void MontarLayout()
    {
        var titulo = new Label
        {
            Text = "RESUMO DO CAIXA", Dock = DockStyle.Top, Height = 46,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            BackColor = Color.FromArgb(60, 60, 60), ForeColor = Color.White
        };

        // ---- painel de indicadores ----
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top, Height = 250, ColumnCount = 2, RowCount = 6, Padding = new Padding(16)
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));

        AddLinha(grid, 0, "Fundo inicial:", _lblFundo, Color.FromArgb(80, 80, 80));
        AddLinha(grid, 1, "Dinheiro:", _lblDinheiro, Color.FromArgb(0, 120, 0));
        AddLinha(grid, 2, "Pix:", _lblPix, Color.FromArgb(0, 100, 160));
        AddLinha(grid, 3, "Débito:", _lblDebito, Color.FromArgb(120, 60, 160));
        AddLinha(grid, 4, "Crédito:", _lblCredito, Color.FromArgb(150, 80, 40));

        var lblGavetaCab = new Label
        {
            Text = "TOTAL EM GAVETA:", Font = new Font("Segoe UI", 15F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill
        };
        _lblGaveta.Name = "lblTotalGaveta";
        _lblGaveta.Font = new Font("Segoe UI", 22F, FontStyle.Bold);
        _lblGaveta.ForeColor = Color.FromArgb(0, 130, 0);
        _lblGaveta.TextAlign = ContentAlignment.MiddleRight; _lblGaveta.Dock = DockStyle.Fill;
        grid.Controls.Add(lblGavetaCab, 0, 5);
        grid.Controls.Add(_lblGaveta, 1, 5);

        // faixa com bruto + nº de vendas
        var faixa = new TableLayoutPanel { Dock = DockStyle.Top, Height = 34, ColumnCount = 2 };
        faixa.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        faixa.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _lblBruto.Font = new Font("Segoe UI", 11F, FontStyle.Bold); _lblBruto.Dock = DockStyle.Fill;
        _lblBruto.TextAlign = ContentAlignment.MiddleLeft;
        _lblQtd.Font = new Font("Segoe UI", 11F); _lblQtd.Dock = DockStyle.Fill;
        _lblQtd.TextAlign = ContentAlignment.MiddleRight;
        faixa.Controls.Add(_lblBruto, 0, 0);
        faixa.Controls.Add(_lblQtd, 1, 0);

        // ---- grid de itens vendidos (fonte do CSV) ----
        _grid.Name = "gridResumo";
        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.RowHeadersVisible = false;
        _grid.BorderStyle = BorderStyle.None;
        _grid.BackgroundColor = Color.White;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        EstiloGrid.Padronizar(_grid);   // cabecalho legivel (sem cortar texto)
        _grid.Columns.Add("prod", "Produto");
        _grid.Columns.Add("qtd", "Qtd");
        _grid.Columns.Add("total", "Total");
        _grid.Columns["qtd"]!.FillWeight = 30;
        _grid.Columns["total"]!.FillWeight = 40;
        _grid.Columns["qtd"]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        _grid.Columns["total"]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

        // ---- botoes de acao ----
        // 8 botoes (44px alt + 4px margem) quebram em ate 3 fileiras na largura padrao:
        // 3 * 52 + 24 (padding) = 180px. Altura 190 => cabe tudo SEM scroll.
        var barra = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, Height = 190, Padding = new Padding(12),
            FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AutoScroll = true
        };
        barra.Controls.Add(Botao("Atualizar (F5)", Color.FromArgb(70, 70, 90), (s, e) => Atualizar()));
        barra.Controls.Add(Botao("Sangria", Color.FromArgb(180, 60, 60), (s, e) => Movimento(TipoMovimento.Sangria)));
        barra.Controls.Add(Botao("Suprimento", Color.FromArgb(0, 130, 70), (s, e) => Movimento(TipoMovimento.Suprimento)));
        barra.Controls.Add(Botao("Imprimir Leitura Z", Color.FromArgb(60, 60, 60), (s, e) => ImprimirZ()));
        barra.Controls.Add(Botao("Exportar Excel (CSV)", Color.FromArgb(0, 120, 60), (s, e) => ExportarCsv()));
        barra.Controls.Add(Botao("Exportar backup (.json)", Color.FromArgb(0, 120, 200), (s, e) => ExportarBackup()));
        barra.Controls.Add(Botao("Importar backup", Color.FromArgb(180, 100, 0), (s, e) => ImportarBackup()));
        barra.Controls.Add(Botao("FECHAR CAIXA", Color.FromArgb(150, 0, 0), (s, e) => FecharCaixa()));

        // ---- aba RESUMO (indicadores + faixa + itens) ----
        var tabResumo = new TabPage("Resumo");
        tabResumo.Controls.Add(_grid);
        tabResumo.Controls.Add(faixa);
        tabResumo.Controls.Add(grid);

        // ---- aba GRAFICOS (barras GDI+, escalam com a janela = zoom) ----
        var painelGraf = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        painelGraf.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
        painelGraf.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        _grafPagamentos.Dock = DockStyle.Fill; _grafPagamentos.Titulo = "Vendas por forma de pagamento";
        _grafPagamentos.FormatarValor = v => Dinheiro.Formatar((int)v);
        _grafItens.Dock = DockStyle.Fill; _grafItens.Titulo = "Itens mais vendidos (R$)";
        _grafItens.FormatarValor = v => Dinheiro.Formatar((int)v);
        painelGraf.Controls.Add(_grafPagamentos, 0, 0);
        painelGraf.Controls.Add(_grafItens, 0, 1);
        var tabGraf = new TabPage("Gráficos");
        tabGraf.Controls.Add(painelGraf);

        var tabs = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 11F) };
        tabs.TabPages.Add(tabResumo);
        tabs.TabPages.Add(tabGraf);

        Controls.Add(tabs);
        Controls.Add(titulo);
        Controls.Add(barra);
    }

    private static void AddLinha(TableLayoutPanel grid, int row, string rotulo, Label valor, Color cor)
    {
        var lbl = new Label { Text = rotulo, Font = new Font("Segoe UI", 13F), TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill };
        valor.Font = new Font("Segoe UI", 15F, FontStyle.Bold);
        valor.ForeColor = cor; valor.TextAlign = ContentAlignment.MiddleRight; valor.Dock = DockStyle.Fill;
        grid.Controls.Add(lbl, 0, row);
        grid.Controls.Add(valor, 1, row);
    }

    private static Button Botao(string texto, Color cor, EventHandler onClick)
    {
        var b = new Button
        {
            Text = texto, Width = 170, Height = 44, Margin = new Padding(4),
            BackColor = cor, ForeColor = Color.White, FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold)
        };
        b.Click += onClick;
        return b;
    }

    // -------------------------------------------------------------------------

    private ResumoTurno _ultimoResumo = null!;
    private List<ItemVendido> _ultimosItens = new();

    private void Atualizar()
    {
        _ultimoResumo = _servico.ResumoTurnoAtual();
        _ultimosItens = _servico.ItensVendidosTurno();
        var v = _ultimoResumo.Vendas;

        _lblFundo.Text = Dinheiro.Formatar(_ultimoResumo.FundoCentavos);
        _lblDinheiro.Text = Dinheiro.Formatar(v.TotalDinheiroCentavos);
        _lblPix.Text = Dinheiro.Formatar(v.TotalPixCentavos);
        _lblDebito.Text = Dinheiro.Formatar(v.TotalDebitoCentavos);
        _lblCredito.Text = Dinheiro.Formatar(v.TotalCreditoCentavos);
        _lblGaveta.Text = Dinheiro.Formatar(_ultimoResumo.TotalGavetaCentavos);
        _lblBruto.Text = "Bruto: " + Dinheiro.Formatar(v.FaturamentoBrutoCentavos);
        _lblQtd.Text = $"Vendas: {v.QuantidadeVendas}";

        _grid.Rows.Clear();
        foreach (var it in _ultimosItens)
            _grid.Rows.Add(it.Nome, it.Quantidade, Dinheiro.Formatar(it.TotalCentavos));

        // ---- graficos ----
        _grafPagamentos.Definir(new (string, long, Color)[]
        {
            ("Dinheiro", v.TotalDinheiroCentavos, Color.FromArgb(0, 150, 0)),
            ("Pix",      v.TotalPixCentavos,      Color.FromArgb(0, 150, 200)),
            ("Débito",   v.TotalDebitoCentavos,   Color.FromArgb(130, 80, 190)),
            ("Crédito",  v.TotalCreditoCentavos,  Color.FromArgb(220, 130, 40)),
        });
        var cores = new[]
        {
            Color.FromArgb(0, 150, 0), Color.FromArgb(0, 150, 200), Color.FromArgb(130, 80, 190),
            Color.FromArgb(220, 130, 40), Color.FromArgb(200, 60, 90), Color.FromArgb(90, 160, 60),
            Color.FromArgb(120, 120, 200), Color.FromArgb(200, 170, 40)
        };
        _grafItens.Definir(_ultimosItens.Take(8).Select((it, idx) =>
            (it.Nome, (long)it.TotalCentavos, cores[idx % cores.Length])));

        if (!_servico.CaixaAberto)
            Text = "Fechamento de Caixa (CAIXA FECHADO)";
    }

    private void Movimento(TipoMovimento tipo)
    {
        if (!_servico.CaixaAberto)
        {
            MessageBox.Show("Abra o caixa antes de registrar sangria/suprimento.", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (Dialogos.Modal(this, () => new FormMovimento(_servico, tipo)) == DialogResult.OK)
            Atualizar();
    }

    private void ImprimirZ()
    {
        var (ok, msg) = _servico.ImprimirFechamentoZ(_ultimoResumo, _ultimosItens);
        MessageBox.Show(ok ? "Leitura Z enviada para a impressora." : "Falha ao imprimir: " + msg,
            "Leitura Z", MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }

    /// <summary>
    /// Gera CSV que o Excel LOCAL abre certinho: usa o separador de lista da cultura
    /// do sistema (';' em pt-BR, ',' em en-US) e formata os numeros na mesma cultura.
    /// Salva na Area de Trabalho, em UTF-8 com BOM (acentos corretos no Excel).
    /// </summary>
    private void ExportarCsv()
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        using var dlg = new SaveFileDialog
        {
            Filter = "CSV Excel (*.csv)|*.csv",
            InitialDirectory = desktop,
            FileName = $"fechamento-festa-{DateTime.Now:yyyyMMdd-HHmm}.csv"
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            // reusa o ExportadorCsv do Core (mesma logica do Historico/menu, sem duplicar)
            var csv = ExportadorCsv.Resumo(_ultimoResumo, _ultimosItens);
            File.WriteAllText(dlg.FileName, csv, new UTF8Encoding(true));
            MessageBox.Show($"CSV exportado:\n{dlg.FileName}", "Exportar Excel",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Erro ao exportar CSV: " + ex.Message, "Exportar Excel",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void FecharCaixa()
    {
        if (!_servico.CaixaAberto)
        {
            MessageBox.Show("O caixa já está fechado.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var r = MessageBox.Show(
            "Fechar o turno de caixa?\n\nSerá impressa a Leitura Z e nenhuma nova venda\n" +
            "poderá ser registrada até abrir um novo caixa.",
            "Fechar Caixa", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (r != DialogResult.Yes) return;

        Atualizar(); // garante numeros atualizados
        var (ok, msg) = _servico.ImprimirFechamentoZ(_ultimoResumo, _ultimosItens);
        if (!ok)
            MessageBox.Show("Aviso: a Leitura Z não imprimiu (" + msg + ").\n" +
                "Os números continuam salvos; use Exportar Excel se precisar.",
                "Leitura Z", MessageBoxButtons.OK, MessageBoxIcon.Warning);

        _servico.FecharCaixa();
        MessageBox.Show("Caixa fechado. Bom descanso!", "Fechamento",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
        Close();
    }

    private void ExportarBackup()
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

    private void ImportarBackup()
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
