using System.Runtime.Versioning;
using PdvFesta.Core;

namespace PdvFesta.App;

/// <summary>
/// PAINEL EM TEMPO REAL (F4): acompanha as vendas do turno enquanto a festa acontece,
/// SEM precisar fechar o caixa e SEM senha (e so visualizacao). Mostra o Total em Gaveta,
/// entradas por forma de pagamento e os itens mais vendidos, atualizando sozinho.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FormDashboard : Form
{
    private readonly Servico _servico;
    private readonly Label _lblGaveta = new();
    private readonly Label _lblBruto = new();
    private readonly Label _lblVendas = new();
    private readonly Label _lblCanceladas = new();
    private readonly GraficoBarras _grafPagamentos = new();
    private readonly GraficoBarras _grafItens = new();
    private readonly System.Windows.Forms.Timer _timer = new();

    public FormDashboard(Servico servico)
    {
        _servico = servico;
        Text = "Painel em Tempo Real";
        Name = "FormDashboard";
        Icon = Marca.Icone();
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(760, 560);
        ClientSize = new Size(900, 640);
        Font = new Font("Segoe UI", 11F);
        KeyPreview = true;

        MontarLayout();
        Atualizar();

        // auto-refresh a cada 3s enquanto o painel estiver aberto.
        _timer.Interval = 3000;
        _timer.Tick += (s, e) => Atualizar();
        _timer.Start();
        FormClosed += (s, e) => _timer.Stop();

        KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); else if (e.KeyCode == Keys.F5) Atualizar(); };
    }

    private void MontarLayout()
    {
        var titulo = new Label
        {
            Text = "PAINEL EM TEMPO REAL", Dock = DockStyle.Top, Height = 46,
            TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.White,
            BackColor = Color.FromArgb(60, 60, 60), Font = new Font("Segoe UI", 16F, FontStyle.Bold)
        };

        // faixa de indicadores no topo (4 cartoes)
        var faixa = new TableLayoutPanel { Dock = DockStyle.Top, Height = 110, ColumnCount = 4, RowCount = 1, Padding = new Padding(10) };
        for (int i = 0; i < 4; i++) faixa.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        faixa.Controls.Add(Cartao("TOTAL EM GAVETA", _lblGaveta, Color.FromArgb(0, 130, 0)), 0, 0);
        faixa.Controls.Add(Cartao("FATURAMENTO", _lblBruto, Color.FromArgb(0, 100, 160)), 1, 0);
        faixa.Controls.Add(Cartao("Nº DE VENDAS", _lblVendas, Color.FromArgb(120, 60, 160)), 2, 0);
        faixa.Controls.Add(Cartao("CANCELADAS", _lblCanceladas, Color.FromArgb(180, 60, 60)), 3, 0);

        // graficos (barras GDI+, escalam com a janela)
        var painelGraf = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        painelGraf.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
        painelGraf.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        _grafPagamentos.Dock = DockStyle.Fill; _grafPagamentos.Titulo = "Vendas por forma de pagamento";
        _grafPagamentos.FormatarValor = v => Dinheiro.Formatar((int)v);
        _grafItens.Dock = DockStyle.Fill; _grafItens.Titulo = "Itens mais vendidos (R$)";
        _grafItens.FormatarValor = v => Dinheiro.Formatar((int)v);
        painelGraf.Controls.Add(_grafPagamentos, 0, 0);
        painelGraf.Controls.Add(_grafItens, 0, 1);

        var rodape = new Label
        {
            Text = "Atualiza sozinho a cada 3s  |  F5 atualiza agora  |  Esc fecha",
            Dock = DockStyle.Bottom, Height = 26, TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.FromArgb(90, 90, 90)
        };

        Controls.Add(painelGraf);
        Controls.Add(faixa);
        Controls.Add(titulo);
        Controls.Add(rodape);
    }

    private static Panel Cartao(string rotulo, Label valor, Color cor)
    {
        var p = new Panel { Dock = DockStyle.Fill, Margin = new Padding(6), BackColor = Color.FromArgb(245, 245, 245) };
        var lbl = new Label { Text = rotulo, Dock = DockStyle.Top, Height = 26, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.FromArgb(70, 70, 70) };
        valor.Dock = DockStyle.Fill; valor.TextAlign = ContentAlignment.MiddleCenter;
        valor.Font = new Font("Segoe UI", 22F, FontStyle.Bold); valor.ForeColor = cor;
        p.Controls.Add(valor); p.Controls.Add(lbl);
        return p;
    }

    private void Atualizar()
    {
        if (!_servico.CaixaAberto)
        {
            _lblGaveta.Text = "--"; _lblBruto.Text = "--"; _lblVendas.Text = "0"; _lblCanceladas.Text = "0";
            Text = "Painel em Tempo Real (caixa fechado)";
            return;
        }

        var resumo = _servico.ResumoTurnoAtual();
        var v = resumo.Vendas;
        _lblGaveta.Text = Dinheiro.Formatar(resumo.TotalGavetaCentavos);
        _lblBruto.Text = Dinheiro.Formatar(v.FaturamentoBrutoCentavos);
        _lblVendas.Text = v.QuantidadeVendas.ToString();

        // canceladas (estornadas) do turno — nao entram no faturamento, mas o gestor vigia.
        var vendasTurno = _servico.VendasDoTurno();
        int canceladas = vendasTurno.Count(x => x.Cancelada);
        _lblCanceladas.Text = canceladas.ToString();
        if (canceladas > 0) _lblCanceladas.ForeColor = Color.FromArgb(200, 0, 0);

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
        var itens = _servico.ItensVendidosTurno().Take(8).ToList();
        var dados = itens.Select((it, i) => (it.Nome, (long)it.TotalCentavos, cores[i % cores.Length]));
        _grafItens.Definir(dados);
    }
}
