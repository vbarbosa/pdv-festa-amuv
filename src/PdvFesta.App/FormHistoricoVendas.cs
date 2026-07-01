using System.Runtime.Versioning;
using PdvFesta.Core;

namespace PdvFesta.App;

/// <summary>
/// Historico de vendas do turno (F3). Permite ESTORNAR (cancelar) uma venda com
/// trava de admin e aviso de estorno fisico. O cancelamento e SOFT DELETE (status
/// Cancelada) — a venda some dos totais/gaveta/Leitura Z, mas fica no banco (auditoria).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FormHistoricoVendas : Form
{
    private readonly Servico _servico;
    private readonly DataGridView _grid = new();
    private readonly Label _lblResumo = new();
    private List<Venda> _vendas = new();

    public FormHistoricoVendas(Servico servico)
    {
        _servico = servico;
        Text = "Histórico de Vendas do Turno";
        Name = "FormHistoricoVendas";
        Icon = Marca.Icone();
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(680, 480);
        ClientSize = new Size(760, 520);
        Font = new Font("Segoe UI", 11F);
        KeyPreview = true;

        MontarLayout();
        Carregar();

        KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); else if (e.KeyCode == Keys.F5) Carregar(); };
    }

    private void MontarLayout()
    {
        var titulo = new Label
        {
            Text = "HISTÓRICO DE VENDAS", Dock = DockStyle.Top, Height = 44,
            TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.White,
            BackColor = Color.FromArgb(60, 60, 60), Font = new Font("Segoe UI", 15F, FontStyle.Bold)
        };

        _grid.Name = "gridHistorico";
        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true; _grid.AllowUserToAddRows = false; _grid.AllowUserToDeleteRows = false;
        _grid.MultiSelect = false; _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.RowHeadersVisible = false; _grid.BorderStyle = BorderStyle.None; _grid.BackgroundColor = Color.White;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        EstiloGrid.Padronizar(_grid);   // cabecalho legivel (sem cortar "Pagamento" etc)
        _grid.Columns.Add("id", "Venda");
        _grid.Columns.Add("hora", "Hora");
        _grid.Columns.Add("total", "Total");
        _grid.Columns.Add("forma", "Pagamento");
        _grid.Columns.Add("status", "Status");
        _grid.Columns["id"]!.FillWeight = 18;
        _grid.Columns["hora"]!.FillWeight = 18;
        _grid.Columns["total"]!.FillWeight = 22;
        _grid.Columns["total"]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _grid.Columns["forma"]!.FillWeight = 24;
        _grid.Columns["status"]!.FillWeight = 18;

        _lblResumo.Dock = DockStyle.Top; _lblResumo.Height = 30;
        _lblResumo.TextAlign = ContentAlignment.MiddleLeft; _lblResumo.Padding = new Padding(12, 0, 0, 0);
        _lblResumo.Font = new Font("Segoe UI", 11F, FontStyle.Bold);

        var barra = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 60, Padding = new Padding(10) };
        barra.Controls.Add(Botao("Atualizar (F5)", Color.FromArgb(70, 70, 90), (s, e) => Carregar()));
        // Reimprimir: para vendas feitas SEM impressora (ex: comecou a festa sem cabo),
        // depois de configurar a impressora o operador reimprime a nota daqui.
        var btnReimprimir = Botao("Reimprimir Nota", Color.FromArgb(0, 110, 60), (s, e) => Reimprimir());
        btnReimprimir.Name = "btnReimprimirNota"; btnReimprimir.Width = 180;
        barra.Controls.Add(btnReimprimir);
        var btnCancelar = Botao("Cancelar / Estornar Venda Selecionada", Color.FromArgb(160, 0, 0), (s, e) => Cancelar());
        btnCancelar.Name = "btnCancelarVenda"; btnCancelar.Width = 320;
        barra.Controls.Add(btnCancelar);

        Controls.Add(_grid);
        Controls.Add(_lblResumo);
        Controls.Add(titulo);
        Controls.Add(barra);
    }

    private static Button Botao(string texto, Color cor, EventHandler onClick)
    {
        var b = new Button
        {
            Text = texto, Width = 150, Height = 40, Margin = new Padding(4),
            BackColor = cor, ForeColor = Color.White, FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold)
        };
        b.Click += onClick;
        return b;
    }

    private void Carregar()
    {
        _vendas = _servico.VendasDoTurno().OrderByDescending(v => v.Id).ToList();
        _grid.Rows.Clear();
        foreach (var v in _vendas)
        {
            int idx = _grid.Rows.Add(
                $"#{v.Id}", v.DataHora.ToString("HH:mm"),
                CupomFormatter.Moeda(v.TotalCentavos),
                CupomFormatter.NomeForma(v.Forma),
                v.Cancelada ? "CANCELADA" : "OK");
            if (v.Cancelada)
            {
                _grid.Rows[idx].DefaultCellStyle.ForeColor = Color.Gray;
                _grid.Rows[idx].DefaultCellStyle.Font = new Font(_grid.Font, FontStyle.Strikeout);
            }
        }
        int validas = _vendas.Count(v => !v.Cancelada);
        int canceladas = _vendas.Count(v => v.Cancelada);
        int bruto = _vendas.Where(v => !v.Cancelada).Sum(v => v.TotalCentavos);
        _lblResumo.Text = $"Vendas validas: {validas}   |   Canceladas: {canceladas}   |   Bruto valido: {CupomFormatter.Moeda(bruto)}";
    }

    private void Reimprimir()
    {
        if (_grid.CurrentRow is null || _grid.CurrentRow.Index < 0 || _grid.CurrentRow.Index >= _vendas.Count)
        {
            MessageBox.Show("Selecione uma venda na lista.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var venda = _vendas[_grid.CurrentRow.Index];

        if (!_servico.TemImpressora)
        {
            MessageBox.Show("Nenhuma impressora configurada.\nConfigure a impressora (F12) e tente de novo.",
                "Reimprimir", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var (ok, msg) = _servico.ImprimirVenda(venda);
        if (ok)
            MessageBox.Show($"Nota da venda #{venda.Id} enviada para a impressora.",
                "Reimprimir", MessageBoxButtons.OK, MessageBoxIcon.Information);
        else
            MessageBox.Show($"Não foi possível imprimir.\nDetalhe: {msg}",
                "Reimprimir", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void Cancelar()
    {
        if (_grid.CurrentRow is null || _grid.CurrentRow.Index < 0 || _grid.CurrentRow.Index >= _vendas.Count)
        {
            MessageBox.Show("Selecione uma venda na lista.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var venda = _vendas[_grid.CurrentRow.Index];
        if (venda.Cancelada)
        {
            MessageBox.Show("Essa venda já está cancelada.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // trava de admin: operador comum nao estorna sozinho
        if (!Dialogos.LiberarAcao(this, _servico, AcaoProtegida.EstornarVenda)) return;

        var r = MessageBox.Show(
            $"Cancelar a venda #{venda.Id} ({CupomFormatter.Moeda(venda.TotalCentavos)} - {CupomFormatter.NomeForma(venda.Forma)})?\n\n" +
            "O estorno na maquininha de cartão ou a devolução do dinheiro físico\n" +
            "DEVE ser feito manualmente.\n\n" +
            "Deseja registrar este cancelamento no sistema?",
            "Estorno de Venda", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (r != DialogResult.Yes) return;

        _servico.CancelarVenda(venda.Id);
        Carregar();
        MessageBox.Show("Venda cancelada. Os totais do caixa já foram ajustados.",
            "Estorno", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
