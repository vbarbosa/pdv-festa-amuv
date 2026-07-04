using System.Runtime.Versioning;
using PdvFesta.Core;

namespace PdvFesta.App;

/// <summary>
/// BALANÇO GERAL (visão macro do administrador): TODOS os turnos/caixas do banco — aberturas,
/// fechamentos, operador, vendas, sangrias/suprimentos e Total em Gaveta — com um resumo
/// global no topo e detalhamento por turno. Livro-caixa completo da operação, sem precisar
/// fechar nada. Somente leitura.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FormBalancoGeral : Form
{
    private readonly Servico _servico;
    private readonly Label _lblResumo = new();
    private readonly DataGridView _grid = new();
    private readonly TextBox _detalhe = new();
    private List<ResumoTurno> _turnos = new();

    public FormBalancoGeral(Servico servico)
    {
        _servico = servico;
        Text = "Balanço Geral — Todos os Caixas";
        Name = "FormBalancoGeral";
        Icon = Marca.Icone();
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(920, 620);
        ClientSize = new Size(980, 680);
        Font = new Font("Segoe UI", 10.5F);
        KeyPreview = true;

        MontarLayout();
        Carregar();
        KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); else if (e.KeyCode == Keys.F5) Carregar(); };
    }

    private void MontarLayout()
    {
        var titulo = new Label
        {
            Text = "BALANÇO GERAL — TODOS OS CAIXAS", Dock = DockStyle.Top, Height = 44,
            TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.White,
            BackColor = Color.FromArgb(60, 60, 60), Font = new Font("Segoe UI", 15F, FontStyle.Bold)
        };

        _lblResumo.Dock = DockStyle.Top; _lblResumo.Height = 96;
        _lblResumo.Font = new Font("Consolas", 11F);
        _lblResumo.Padding = new Padding(14, 8, 8, 8);
        _lblResumo.BackColor = Color.FromArgb(245, 245, 245);
        _lblResumo.TextAlign = ContentAlignment.TopLeft;

        // grade de turnos
        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true; _grid.AllowUserToAddRows = false; _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; _grid.RowHeadersVisible = false;
        _grid.BorderStyle = BorderStyle.None; _grid.BackgroundColor = Color.White;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        EstiloGrid.Padronizar(_grid);
        _grid.Columns.Add("id", "Caixa"); _grid.Columns["id"]!.FillWeight = 10;
        _grid.Columns.Add("dia", "Dia"); _grid.Columns["dia"]!.FillWeight = 16;
        _grid.Columns.Add("abertura", "Abertura"); _grid.Columns["abertura"]!.FillWeight = 14;
        _grid.Columns.Add("fechamento", "Fechamento"); _grid.Columns["fechamento"]!.FillWeight = 14;
        _grid.Columns.Add("operador", "Operador"); _grid.Columns["operador"]!.FillWeight = 16;
        _grid.Columns.Add("vendas", "Vendas"); _grid.Columns["vendas"]!.FillWeight = 10;
        _grid.Columns.Add("faturamento", "Faturamento"); _grid.Columns["faturamento"]!.FillWeight = 16;
        _grid.Columns.Add("gaveta", "Em Gaveta"); _grid.Columns["gaveta"]!.FillWeight = 16;
        _grid.Columns["faturamento"]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _grid.Columns["gaveta"]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _grid.SelectionChanged += (s, e) => MostrarDetalhe();

        // detalhe (embaixo): movimentos e resumo por forma do turno selecionado
        _detalhe.Dock = DockStyle.Bottom; _detalhe.Height = 180;
        _detalhe.Multiline = true; _detalhe.ReadOnly = true; _detalhe.ScrollBars = ScrollBars.Vertical;
        _detalhe.WordWrap = false; _detalhe.Font = new Font("Consolas", 10F);
        _detalhe.BackColor = Color.FromArgb(250, 250, 240);

        var rodape = new Label
        {
            Text = "Clique num caixa para ver o detalhe  |  F5 atualiza  |  Esc fecha",
            Dock = DockStyle.Bottom, Height = 24, TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.FromArgb(90, 90, 90)
        };

        Controls.Add(_grid);
        Controls.Add(_detalhe);
        Controls.Add(rodape);
        Controls.Add(_lblResumo);
        Controls.Add(titulo);
    }

    private static string M(int c) => Dinheiro.Formatar(c);

    private void Carregar()
    {
        _turnos = _servico.ConsolidarTodosOsTurnos();
        _grid.Rows.Clear();

        foreach (var r in _turnos)
        {
            var t = r.Turno;
            int idx = _grid.Rows.Add(
                $"#{t.Id}",
                t.Abertura.ToString("dd/MM/yyyy"),
                t.Abertura.ToString("HH:mm"),
                t.Fechamento is DateTime f ? f.ToString("HH:mm") : "ABERTO",
                string.IsNullOrWhiteSpace(t.Operador) ? "-" : t.Operador,
                r.Vendas.QuantidadeVendas.ToString(),
                M(r.Vendas.FaturamentoBrutoCentavos),
                M(r.TotalGavetaCentavos));
            if (t.Status == StatusCaixa.Aberto)
                _grid.Rows[idx].DefaultCellStyle.ForeColor = Color.FromArgb(0, 120, 0);   // aberto em verde
        }

        // resumo GLOBAL (todos os turnos somados)
        int nTurnos = _turnos.Count;
        int totVendas = _turnos.Sum(r => r.Vendas.QuantidadeVendas);
        int totFat = _turnos.Sum(r => r.Vendas.FaturamentoBrutoCentavos);
        int totDin = _turnos.Sum(r => r.Vendas.TotalDinheiroCentavos);
        int totPix = _turnos.Sum(r => r.Vendas.TotalPixCentavos);
        int totCartao = _turnos.Sum(r => r.Vendas.TotalDebitoCentavos + r.Vendas.TotalCreditoCentavos);
        int totSang = _turnos.Sum(r => r.SangriasCentavos);
        int totSup = _turnos.Sum(r => r.SuprimentosCentavos);

        _lblResumo.Text =
            $"TURNOS: {nTurnos}    VENDAS: {totVendas}    FATURAMENTO TOTAL: {M(totFat)}\n" +
            $"Dinheiro {M(totDin)}   |   Pix {M(totPix)}   |   Cartao {M(totCartao)}\n" +
            $"Suprimentos {M(totSup)}   |   Sangrias {M(totSang)}";

        if (_grid.Rows.Count > 0) { _grid.Rows[0].Selected = true; MostrarDetalhe(); }
        else _detalhe.Text = "Nenhum caixa registrado ainda.";
    }

    private void MostrarDetalhe()
    {
        if (_grid.CurrentRow is null || _grid.CurrentRow.Index < 0 || _grid.CurrentRow.Index >= _turnos.Count)
        { _detalhe.Text = ""; return; }

        var r = _turnos[_grid.CurrentRow.Index];
        var t = r.Turno;
        var v = r.Vendas;
        var movs = _servico.MovimentosDoTurno(t.Id);
        int canceladas = _servico.VendasDoCaixa(t.Id).Count(x => x.Cancelada);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"CAIXA #{t.Id}  —  {t.Operador}");
        sb.AppendLine($"Abertura: {t.Abertura:dd/MM/yyyy HH:mm}   Fechamento: {(t.Fechamento is DateTime f ? f.ToString("dd/MM/yyyy HH:mm") : "ABERTO")}");
        sb.AppendLine(new string('-', 52));
        sb.AppendLine($"Fundo inicial ......... {M(r.FundoCentavos)}");
        sb.AppendLine($"Dinheiro .............. {M(v.TotalDinheiroCentavos)}");
        sb.AppendLine($"Pix ................... {M(v.TotalPixCentavos)}");
        sb.AppendLine($"Cartao Debito ......... {M(v.TotalDebitoCentavos)}");
        sb.AppendLine($"Cartao Credito ........ {M(v.TotalCreditoCentavos)}");
        sb.AppendLine($"(+) Suprimentos ....... {M(r.SuprimentosCentavos)}");
        sb.AppendLine($"(-) Sangrias .......... {M(r.SangriasCentavos)}");
        sb.AppendLine($"= EM GAVETA ........... {M(r.TotalGavetaCentavos)}");
        sb.AppendLine($"Vendas: {v.QuantidadeVendas}   Canceladas: {canceladas}");

        if (movs.Count > 0)
        {
            sb.AppendLine(new string('-', 52));
            sb.AppendLine("MOVIMENTOS:");
            foreach (var m in movs)
                sb.AppendLine($"  {(m.Tipo == TipoMovimento.Sangria ? "SANGRIA " : "SUPRIM. ")} {M(m.ValorCentavos),12}   {m.Motivo}");
        }
        _detalhe.Text = sb.ToString();
    }
}
