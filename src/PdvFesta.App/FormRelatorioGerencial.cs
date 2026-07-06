using System.Runtime.Versioning;
using PdvFesta.Core;

namespace PdvFesta.App;

/// <summary>
/// RELATORIO GERENCIAL (backoffice / "time travel"): analisa as vendas de QUALQUER periodo
/// direto do banco — DESACOPLADO do caixa aberto. Filtro de datas (De/Ate) + atalhos rapidos
/// (Hoje, Ontem, 7 dias, Tudo), cartoes de resumo, graficos e grade de vendas. O CSV exporta
/// exatamente o periodo filtrado, com nome dinamico. Somente leitura (nao mexe no caixa).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FormRelatorioGerencial : Form
{
    private readonly Servico _servico;
    private readonly DateTimePicker _dtDe = new();
    private readonly DateTimePicker _dtAte = new();
    private readonly Label _lblResumo = new();
    private readonly GraficoBarras _grafPag = new();
    private readonly GraficoBarras _grafItens = new();
    private readonly DataGridView _grid = new();

    private List<Venda> _vendas = new();

    public FormRelatorioGerencial(Servico servico)
    {
        _servico = servico;
        Text = "Relatório Gerencial — Vendas por Período";
        Name = "FormRelatorioGerencial";
        Icon = Marca.Icone();
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(980, 660);
        ClientSize = new Size(1040, 720);
        Font = new Font("Segoe UI", 10.5F);
        KeyPreview = true;

        MontarLayout();
        AtalhoHoje();   // abre no dia de hoje por padrao
        KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); else if (e.KeyCode == Keys.F5) Filtrar(); };
    }

    private void MontarLayout()
    {
        var titulo = new Label
        {
            Text = "RELATÓRIO GERENCIAL", Dock = DockStyle.Top, Height = 42,
            TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.White,
            BackColor = Color.FromArgb(60, 60, 60), Font = new Font("Segoe UI", 15F, FontStyle.Bold)
        };

        // ---- barra de FILTRO ----
        var barra = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 82, Padding = new Padding(10, 8, 10, 4), WrapContents = true };
        _dtDe.Format = DateTimePickerFormat.Short; _dtDe.Width = 110;
        _dtAte.Format = DateTimePickerFormat.Short; _dtAte.Width = 110;
        barra.Controls.Add(new Label { Text = "De:", AutoSize = true, Margin = new Padding(0, 8, 4, 0) });
        barra.Controls.Add(_dtDe);
        barra.Controls.Add(new Label { Text = "Até:", AutoSize = true, Margin = new Padding(10, 8, 4, 0) });
        barra.Controls.Add(_dtAte);
        barra.Controls.Add(BotaoRapido("Hoje", AtalhoHoje));
        barra.Controls.Add(BotaoRapido("Ontem", AtalhoOntem));
        barra.Controls.Add(BotaoRapido("7 dias", Atalho7Dias));
        barra.Controls.Add(BotaoRapido("Tudo", AtalhoTudo));
        var btnFiltrar = BotaoRapido("FILTRAR (F5)", Filtrar); btnFiltrar.BackColor = Color.FromArgb(0, 120, 200); btnFiltrar.ForeColor = Color.White; btnFiltrar.Width = 130;
        barra.Controls.Add(btnFiltrar);
        var btnCsv = BotaoRapido("Exportar CSV", ExportarCsv); btnCsv.BackColor = Color.FromArgb(0, 120, 60); btnCsv.ForeColor = Color.White; btnCsv.Width = 130;
        barra.Controls.Add(btnCsv);
        var btnPrecos = BotaoRapido("Preços praticados", MostrarPrecosPraticados); btnPrecos.BackColor = Color.FromArgb(120, 90, 30); btnPrecos.ForeColor = Color.White; btnPrecos.Width = 150;
        barra.Controls.Add(btnPrecos);

        // ---- resumo (texto) ----
        _lblResumo.Dock = DockStyle.Top; _lblResumo.Height = 70;
        _lblResumo.Font = new Font("Consolas", 11F); _lblResumo.Padding = new Padding(14, 6, 8, 6);
        _lblResumo.BackColor = Color.FromArgb(245, 245, 245); _lblResumo.TextAlign = ContentAlignment.TopLeft;

        // ---- graficos (2 lado a lado) ----
        var painelGraf = new TableLayoutPanel { Dock = DockStyle.Top, Height = 220, ColumnCount = 2, RowCount = 1 };
        painelGraf.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        painelGraf.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        _grafPag.Titulo = "Por forma de pagamento"; _grafPag.Dock = DockStyle.Fill; _grafPag.FormatarValor = v => Dinheiro.Formatar((int)v);
        _grafItens.Titulo = "Itens mais vendidos (R$)"; _grafItens.Dock = DockStyle.Fill; _grafItens.FormatarValor = v => Dinheiro.Formatar((int)v);
        painelGraf.Controls.Add(_grafPag, 0, 0);
        painelGraf.Controls.Add(_grafItens, 1, 0);

        // ---- grade de vendas ----
        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true; _grid.AllowUserToAddRows = false; _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; _grid.RowHeadersVisible = false;
        _grid.BorderStyle = BorderStyle.None; _grid.BackgroundColor = Color.White;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        EstiloGrid.Padronizar(_grid);
        _grid.Columns.Add("id", "Venda"); _grid.Columns["id"]!.FillWeight = 10;
        _grid.Columns.Add("data", "Data/Hora"); _grid.Columns["data"]!.FillWeight = 20;
        _grid.Columns.Add("total", "Total"); _grid.Columns["total"]!.FillWeight = 14;
        _grid.Columns["total"]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _grid.Columns.Add("forma", "Pagamento"); _grid.Columns["forma"]!.FillWeight = 18;
        _grid.Columns.Add("obs", "Observação"); _grid.Columns["obs"]!.FillWeight = 24;
        _grid.Columns.Add("status", "Status"); _grid.Columns["status"]!.FillWeight = 14;

        var rodape = new Label { Text = "Escolha o período e clique Filtrar. Esc fecha.", Dock = DockStyle.Bottom, Height = 24, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.FromArgb(90, 90, 90) };

        Controls.Add(_grid);
        Controls.Add(painelGraf);
        Controls.Add(_lblResumo);
        Controls.Add(barra);
        Controls.Add(rodape);
        Controls.Add(titulo);
    }

    private static Button BotaoRapido(string texto, Action onClick)
    {
        var b = new Button { Text = texto, AutoSize = false, Width = 82, Height = 32, Margin = new Padding(4, 4, 0, 0), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
        b.Click += (s, e) => onClick();
        return b;
    }

    // ---- atalhos de periodo ----
    private void AtalhoHoje()   { var h = DateTime.Today; _dtDe.Value = h; _dtAte.Value = h; Filtrar(); }
    private void AtalhoOntem()  { var o = DateTime.Today.AddDays(-1); _dtDe.Value = o; _dtAte.Value = o; Filtrar(); }
    private void Atalho7Dias()  { _dtDe.Value = DateTime.Today.AddDays(-6); _dtAte.Value = DateTime.Today; Filtrar(); }
    private void AtalhoTudo()   { _dtDe.Value = new DateTime(2020, 1, 1); _dtAte.Value = DateTime.Today; Filtrar(); }

    /// <summary>Periodo selecionado: do inicio do dia "De" ate o FIM do dia "Ate" (23:59:59).</summary>
    private (DateTime ini, DateTime fim) Periodo()
    {
        var ini = _dtDe.Value.Date;
        var fim = _dtAte.Value.Date.AddDays(1).AddTicks(-1);   // ultimo instante do dia final
        if (ini > fim) (ini, fim) = (fim.Date, ini.Date.AddDays(1).AddTicks(-1));
        return (ini, fim);
    }

    private static string M(int c) => Dinheiro.Formatar(c);

    private void Filtrar()
    {
        var (ini, fim) = Periodo();
        _vendas = _servico.VendasPorPeriodo(ini, fim);
        var resumo = _servico.ResumoPorPeriodo(ini, fim);
        var itens = _servico.ItensPorPeriodo(ini, fim);
        var v = resumo;

        // grade
        _grid.Rows.Clear();
        foreach (var venda in _vendas.OrderByDescending(x => x.Id))
        {
            int idx = _grid.Rows.Add($"#{venda.Id}", venda.DataHora.ToString("dd/MM/yyyy HH:mm"),
                M(venda.TotalCentavos), CupomFormatter.NomeForma(venda.Forma), venda.Observacao,
                venda.Cancelada ? "CANCELADA" : "OK");
            if (venda.Cancelada) { _grid.Rows[idx].DefaultCellStyle.ForeColor = Color.Gray; _grid.Rows[idx].DefaultCellStyle.Font = new Font(_grid.Font, FontStyle.Strikeout); }
            else if (venda.EhCortesia) _grid.Rows[idx].Cells["forma"].Style.ForeColor = Color.FromArgb(150, 90, 160);
        }

        // resumo
        int canceladas = _vendas.Count(x => x.Cancelada);
        _lblResumo.Text =
            $"Período: {ini:dd/MM/yyyy} a {fim:dd/MM/yyyy}    VENDAS: {v.QuantidadeVendas}    FATURAMENTO: {M(v.FaturamentoBrutoCentavos)}\n" +
            $"Dinheiro {M(v.TotalDinheiroCentavos)}  |  Pix {M(v.TotalPixCentavos)}  |  Cartão {M(v.TotalCartaoCentavos)}\n" +
            $"Canceladas: {canceladas}   |   Cortesias: {v.QuantidadeCortesias} ({M(v.TotalCortesiaCentavos)})";

        // graficos
        _grafPag.Definir(new (string, long, Color)[]
        {
            ("Dinheiro", v.TotalDinheiroCentavos, Color.FromArgb(0, 150, 0)),
            ("Pix",      v.TotalPixCentavos,      Color.FromArgb(0, 150, 200)),
            ("Débito",   v.TotalDebitoCentavos,   Color.FromArgb(130, 80, 190)),
            ("Crédito",  v.TotalCreditoCentavos,  Color.FromArgb(220, 130, 40)),
        });
        var top = itens.Take(8).ToList();
        var cores = new[] { Color.FromArgb(0,150,0), Color.FromArgb(0,150,200), Color.FromArgb(130,80,190), Color.FromArgb(220,130,40), Color.FromArgb(200,80,80), Color.FromArgb(80,160,120), Color.FromArgb(160,140,0), Color.FromArgb(120,120,120) };
        _grafItens.Definir(top.Select((it, i) => (it.Nome, (long)it.TotalCentavos, cores[i % cores.Length])));
    }

    /// <summary>
    /// Mostra os PRECOS PRATICADOS por item no periodo (para o contador). Itens que mudaram de
    /// preco durante o evento aparecem com marca "PRECO MUDOU" e uma linha por preco.
    /// </summary>
    private void MostrarPrecosPraticados()
    {
        var (ini, fim) = Periodo();
        var precos = _servico.PrecosPraticadosPorPeriodo(ini, fim);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"PRECOS PRATICADOS  —  {ini:dd/MM/yyyy} a {fim:dd/MM/yyyy}");
        sb.AppendLine(new string('=', 54));
        // agrupa por item para marcar os que tiveram mais de um preco
        foreach (var grupo in precos.GroupBy(p => p.Nome).OrderBy(g => g.Key))
        {
            var linhas = grupo.OrderBy(p => p.PrecoUnitarioCentavos).ToList();
            bool mudou = linhas.Count > 1;
            sb.AppendLine();
            sb.AppendLine(mudou ? $"{grupo.Key}   *** PRECO MUDOU ({linhas.Count} precos) ***" : grupo.Key);
            foreach (var p in linhas)
                sb.AppendLine($"   {M(p.PrecoUnitarioCentavos),9} un.  x{p.Quantidade,-4}  =  {M(p.TotalCentavos)}");
        }
        if (precos.Count == 0) sb.AppendLine("\n(Nenhuma venda no periodo.)");

        // janela simples de texto (copiavel) para o contador
        using var dlg = new Form
        {
            Text = "Preços praticados (para o contador)", StartPosition = FormStartPosition.CenterParent,
            Size = new Size(520, 560), Icon = Marca.Icone(), FormBorderStyle = FormBorderStyle.Sizable
        };
        var txt = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Font = new Font("Consolas", 10.5F), Text = sb.ToString(), BackColor = Color.FromArgb(250, 250, 240) };
        dlg.Controls.Add(txt);
        dlg.ShowDialog(this);
    }

    private void ExportarCsv()
    {
        // exige a mesma permissao do CSV do turno (dado financeiro saindo)
        if (!Dialogos.LiberarAcao(this, _servico, AcaoProtegida.ExportarCsv)) return;

        var (ini, fim) = Periodo();
        using var dlg = new FolderBrowserDialog { Description = "Pasta para salvar o CSV do período" };
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (Directory.Exists(desktop)) dlg.SelectedPath = desktop;
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            // nome dinamico com as datas -> nunca sobrescreve
            var nome = $"Vendas_FestaJunina_{ini:dd-MM-yyyy}_ate_{fim:dd-MM-yyyy}.csv";
            var caminho = Path.Combine(dlg.SelectedPath, nome);
            var utf8Bom = new System.Text.UTF8Encoding(true);
            File.WriteAllText(caminho, ExportadorCsv.Vendas(_vendas), utf8Bom);
            MessageBox.Show($"CSV exportado ({_vendas.Count} vendas do período):\n{caminho}",
                "Exportar CSV", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Erro ao exportar: " + ex.Message, "Exportar CSV", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
