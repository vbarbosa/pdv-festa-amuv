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

        // barra de acoes (excluir caixa de teste)
        var barraAcoes = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(8, 4, 8, 4) };
        var btnExcluir = new Button
        {
            Text = "Excluir caixa de teste (sem vendas)", Width = 300, Height = 34,
            BackColor = Color.FromArgb(160, 0, 0), ForeColor = Color.White, FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
        };
        btnExcluir.Click += (s, e) => ExcluirCaixaSelecionado();
        barraAcoes.Controls.Add(btnExcluir);

        Controls.Add(_grid);
        Controls.Add(_detalhe);
        Controls.Add(barraAcoes);
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
        int totCortesia = _turnos.Sum(r => r.Vendas.TotalCortesiaCentavos);
        int qtdCortesia = _turnos.Sum(r => r.Vendas.QuantidadeCortesias);

        _lblResumo.Text =
            $"TURNOS: {nTurnos}    VENDAS: {totVendas}    FATURAMENTO TOTAL: {M(totFat)}\n" +
            $"Dinheiro {M(totDin)}   |   Pix {M(totPix)}   |   Cartao {M(totCartao)}\n" +
            $"Suprimentos {M(totSup)}   |   Sangrias {M(totSang)}   |   Cortesias {qtdCortesia} ({M(totCortesia)})";

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

    /// <summary>
    /// Exclui o caixa selecionado. SEM vendas: confirmacao simples. COM vendas (turno de teste
    /// que voce nao quer manter): trava FORTE — senha de admin + digitar EXCLUIR + mostra o que
    /// vai apagar. Nunca o caixa aberto.
    /// </summary>
    private void ExcluirCaixaSelecionado()
    {
        if (_grid.CurrentRow is null || _grid.CurrentRow.Index < 0 || _grid.CurrentRow.Index >= _turnos.Count)
        { MessageBox.Show("Selecione um caixa na lista.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information); return; }

        var resumo = _turnos[_grid.CurrentRow.Index];
        var t = resumo.Turno;
        int nVendas = _servico.VendasNoCaixa(t.Id);

        // ---- caixa VAZIO: confirmacao simples ----
        if (nVendas == 0)
        {
            var r = MessageBox.Show(
                $"Excluir o Caixa #{t.Id} (operador {t.Operador}, sem vendas)?\nIsso limpa o histórico de testes.",
                "Excluir caixa de teste", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (r != DialogResult.Yes) return;
            if (_servico.ExcluirCaixaDeTeste(t.Id)) { Carregar(); MessageBox.Show("Caixa de teste excluído.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information); }
            else MessageBox.Show("Não foi possível excluir (caixa aberto?).", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // ---- caixa COM vendas: EXCLUSAO FORTE (teste que nao quer manter) ----
        // 1) senha de admin
        if (!Dialogos.LiberarAdmin(this, _servico)) return;

        // 2) mostra o que vai apagar e pede a palavra EXCLUIR
        var fatur = M(resumo.Vendas.FaturamentoBrutoCentavos);
        var aviso =
            $"ATENÇÃO — EXCLUSÃO DEFINITIVA (só para turno de TESTE):\n\n" +
            $"Caixa #{t.Id}  —  {t.Operador}\n" +
            $"{nVendas} venda(s), faturamento {fatur}.\n\n" +
            $"Isso APAGA o turno E todas as suas vendas do banco (não dá pra desfazer).\n" +
            $"Se este turno tem vendas REAIS, NÃO exclua.\n\n" +
            $"Para confirmar, digite a palavra EXCLUIR abaixo:";
        var digitado = PromptTexto("Excluir turno com vendas", aviso);
        if (!string.Equals(digitado?.Trim(), "EXCLUIR", StringComparison.Ordinal))
        {
            if (digitado is not null)   // null = cancelou; senao errou a palavra
                MessageBox.Show("Palavra incorreta. Nada foi excluído.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        int apagadas = _servico.ExcluirCaixaComVendas(t.Id);
        if (apagadas >= 0)
        {
            Carregar();
            MessageBox.Show($"Caixa #{t.Id} excluído ({apagadas} venda(s) apagadas).", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
            MessageBox.Show("Não foi possível excluir (é o caixa aberto agora?).", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    /// <summary>Mini-dialogo de entrada de texto (para digitar a palavra de confirmacao).</summary>
    private string? PromptTexto(string titulo, string mensagem)
    {
        using var dlg = new Form { Text = titulo, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, ClientSize = new Size(460, 260), Icon = Marca.Icone() };
        var lbl = new Label { Text = mensagem, Dock = DockStyle.Top, Height = 180, Padding = new Padding(12, 10, 12, 4) };
        var txt = new TextBox { Dock = DockStyle.Top, Font = new Font("Segoe UI", 14F, FontStyle.Bold), Margin = new Padding(12) };
        var ok = new Button { Text = "Confirmar", Dock = DockStyle.Bottom, Height = 40, DialogResult = DialogResult.OK, BackColor = Color.FromArgb(160, 0, 0), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        dlg.Controls.Add(txt); dlg.Controls.Add(lbl); dlg.Controls.Add(ok);
        dlg.AcceptButton = ok;
        txt.Select();
        return dlg.ShowDialog(this) == DialogResult.OK ? txt.Text : null;
    }
}
