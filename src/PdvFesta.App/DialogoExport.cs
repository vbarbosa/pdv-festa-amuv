using System.Runtime.Versioning;
using PdvFesta.Core;

namespace PdvFesta.App;

/// <summary>
/// Pergunta ao operador COMO exportar: formato (CSV unico / varios CSV / XLSX / XLSX com abas),
/// como os itens aparecem (amontoados por venda ou 1 linha por item) e quais secoes incluir
/// (Resumo, Vendas, Itens, Precos). Retorna as escolhas; a chamadora grava. Reutilizado no
/// Relatorio Gerencial e no export do turno.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DialogoExport : Form
{
    private readonly RadioButton _rbCsvUnico = new() { Text = "CSV único (tudo num arquivo, seções empilhadas)" };
    private readonly RadioButton _rbCsvMulti = new() { Text = "Vários CSV (um arquivo por seção, numa pasta)" };
    private readonly RadioButton _rbXlsxUnico = new() { Text = "Excel .xlsx (uma aba só)" };
    private readonly RadioButton _rbXlsxAbas = new() { Text = "Excel .xlsx com ABAS (Resumo | Vendas | Itens | Preços)" };

    private readonly RadioButton _rbItemLinha = new() { Text = "1 linha por item (explode — fácil de analisar)" };
    private readonly RadioButton _rbItemAmont = new() { Text = "Itens amontoados na venda (\"2x A | 1x B\")" };

    private readonly CheckBox _chkResumo = new() { Text = "Resumo (totais por pagamento, cortesias)", Checked = true };
    private readonly CheckBox _chkVendas = new() { Text = "Vendas (lista detalhada)", Checked = true };
    private readonly CheckBox _chkItens = new() { Text = "Itens vendidos (agrupado por produto)", Checked = true };
    private readonly CheckBox _chkPrecos = new() { Text = "Preços praticados (mudança de preço)", Checked = true };

    public FormatoExport Formato { get; private set; } = FormatoExport.XlsxAbas;
    public ModoItens ModoItens { get; private set; } = ModoItens.UmaLinhaPorItem;
    public SecoesExport Secoes { get; private set; } = SecoesExport.Tudo;
    public bool Confirmado { get; private set; }

    public DialogoExport()
    {
        Text = "Exportar relatório — opções";
        Name = "DialogoExport";
        Icon = Marca.Icone();
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        ClientSize = new Size(480, 560);
        Font = new Font("Segoe UI", 10.5F);
        KeyPreview = true;

        _rbXlsxAbas.Checked = true;     // default: Excel com abas (o mais completo)
        _rbItemLinha.Checked = true;

        var raiz = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(14, 10, 14, 10) };

        raiz.Controls.Add(Titulo("Formato do arquivo:"));
        foreach (var rb in new[] { _rbXlsxAbas, _rbXlsxUnico, _rbCsvUnico, _rbCsvMulti }) { rb.AutoSize = true; rb.Margin = new Padding(4, 2, 0, 2); raiz.Controls.Add(rb); }

        raiz.Controls.Add(Titulo("Como listar os itens das vendas:"));
        foreach (var rb in new[] { _rbItemLinha, _rbItemAmont }) { rb.AutoSize = true; rb.Margin = new Padding(4, 2, 0, 2); raiz.Controls.Add(rb); }

        raiz.Controls.Add(Titulo("Seções a incluir:"));
        foreach (var ch in new[] { _chkResumo, _chkVendas, _chkItens, _chkPrecos }) { ch.AutoSize = true; ch.Margin = new Padding(4, 2, 0, 2); raiz.Controls.Add(ch); }

        var barra = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 56, ColumnCount = 2, RowCount = 1 };
        barra.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        barra.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        var ok = new Button { Text = "EXPORTAR", Dock = DockStyle.Fill, Margin = new Padding(6), BackColor = Color.FromArgb(0, 130, 0), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 12F, FontStyle.Bold) };
        ok.Click += (s, e) => Aplicar();
        var cancelar = new Button { Text = "Cancelar", Dock = DockStyle.Fill, Margin = new Padding(6), BackColor = Color.FromArgb(120, 120, 120), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        cancelar.Click += (s, e) => { Confirmado = false; Close(); };
        barra.Controls.Add(ok, 0, 0);
        barra.Controls.Add(cancelar, 1, 0);

        Controls.Add(raiz);
        Controls.Add(barra);
        KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) { Confirmado = false; Close(); } };
    }

    private static Label Titulo(string t) => new() { Text = t, AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), Margin = new Padding(0, 10, 0, 4), ForeColor = Color.FromArgb(60, 60, 60) };

    private void Aplicar()
    {
        Formato = _rbCsvUnico.Checked ? FormatoExport.CsvUnico
                : _rbCsvMulti.Checked ? FormatoExport.CsvMultiplos
                : _rbXlsxUnico.Checked ? FormatoExport.XlsxUnico
                : FormatoExport.XlsxAbas;
        ModoItens = _rbItemAmont.Checked ? ModoItens.AmontoadoNaVenda : ModoItens.UmaLinhaPorItem;

        SecoesExport s = 0;
        if (_chkResumo.Checked) s |= SecoesExport.Resumo;
        if (_chkVendas.Checked) s |= SecoesExport.Vendas;
        if (_chkItens.Checked)  s |= SecoesExport.Itens;
        if (_chkPrecos.Checked) s |= SecoesExport.Precos;
        if (s == 0) { MessageBox.Show("Selecione ao menos uma seção.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        Secoes = s;

        Confirmado = true;
        Close();
    }

    /// <summary>Extensao sugerida para o SaveFileDialog conforme o formato (CsvMultiplos usa pasta).</summary>
    public bool ExigePasta => Formato == FormatoExport.CsvMultiplos;
    public string Extensao => Formato is FormatoExport.CsvUnico ? "csv" : Formato is FormatoExport.CsvMultiplos ? "" : "xlsx";
}
