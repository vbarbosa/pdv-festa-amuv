using System.Runtime.Versioning;
using System.Text;
using PdvFesta.Core;

namespace PdvFesta.App;

/// <summary>
/// Editor visual do layout do cupom impresso (58mm). Configura cabecalho, modo
/// (Recibo Completo x Ficha de Consumo), separacao por item e rodape, com um
/// PREVIEW em tempo real em fonte mono (Courier New) simulando a bobina.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FormLayoutCupom : Form
{
    private readonly Servico _servico;

    private readonly TextBox _txtEvento = new();
    private readonly TextBox _txtSubtitulo = new();
    private readonly RadioButton _rbCompleto = new();
    private readonly RadioButton _rbFicha = new();
    private readonly RadioButton _rbVales = new();
    private readonly RadioButton _rbSoVales = new();
    private readonly CheckBox _chkSeparar = new();
    private readonly TextBox _txtRodape = new();
    private readonly TextBox _preview = new();

    public FormLayoutCupom(Servico servico)
    {
        _servico = servico;
        Text = "Layout do Cupom";
        Name = "FormLayoutCupom";
        Icon = Marca.Icone();
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(760, 560);
        ClientSize = new Size(820, 600);
        Font = new Font("Segoe UI", 11F);

        MontarLayout();
        CarregarConfig();
        AtualizarPreview();
    }

    private void MontarLayout()
    {
        // ---- painel de configuracao (esquerda), empilhado de cima p/ baixo ----
        _txtEvento.Font = new Font("Segoe UI", 12F);
        _txtEvento.TextChanged += (s, e) => AtualizarPreview();
        _txtSubtitulo.Font = new Font("Segoe UI", 12F);
        _txtSubtitulo.TextChanged += (s, e) => AtualizarPreview();

        _rbCompleto.Text = "Recibo Completo (valores, total, troco)";
        _rbCompleto.CheckedChanged += (s, e) => { AtualizarEstado(); AtualizarPreview(); };
        _rbFicha.Text = "Ficha de Consumo (só item, fonte grande)";
        _rbFicha.CheckedChanged += (s, e) => { AtualizarEstado(); AtualizarPreview(); };
        _rbVales.Text = "Recibo + Vales destacáveis (1 ficha por unidade)";
        _rbVales.CheckedChanged += (s, e) => { AtualizarEstado(); AtualizarPreview(); };
        _rbSoVales.Text = "Só Vales destacáveis (sem recibo, mini-cabeçalho por ficha)";
        _rbSoVales.CheckedChanged += (s, e) => { AtualizarEstado(); AtualizarPreview(); };

        _chkSeparar.Text = "Cortar / separar 1 ficha por item";
        _chkSeparar.CheckedChanged += (s, e) => AtualizarPreview();

        _txtRodape.Font = new Font("Segoe UI", 12F);
        _txtRodape.TextChanged += (s, e) => AtualizarPreview();

        var btnSalvar = new Button
        {
            Text = "Salvar layout", Height = 48, Margin = new Padding(3, 12, 3, 3),
            BackColor = Color.FromArgb(0, 130, 0), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 12F, FontStyle.Bold)
        };
        btnSalvar.Click += (s, e) => Salvar();

        var btnTeste = new Button
        {
            Name = "btnImprimirTeste",
            Text = "Imprimir teste de layout", Height = 44, Margin = new Padding(3, 3, 3, 3),
            BackColor = Color.FromArgb(0, 120, 200), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 11F, FontStyle.Bold)
        };
        // trava anti-duplo-clique: desabilita o botao por ~2s para nao imprimir 2x.
        btnTeste.Click += async (s, e) =>
        {
            btnTeste.Enabled = false;
            try { ImprimirTeste(); }
            finally { await System.Threading.Tasks.Task.Delay(2000); btnTeste.Enabled = true; }
        };

        var fluxo = new FlowLayoutPanel
        {
            Dock = DockStyle.Left, Width = 380, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoScroll = true, Padding = new Padding(14),
            BackColor = Color.FromArgb(245, 245, 245)
        };
        var ordenados = new Control[]
        {
            Titulo("CABECALHO"), Rotulo("Nome do evento:"), _txtEvento,
            Rotulo("Subtitulo (ex: Caixa 01):"), _txtSubtitulo,
            Titulo("MODO DE IMPRESSÃO"), _rbCompleto, _rbFicha, _rbVales, _rbSoVales, _chkSeparar,
            Titulo("RODAPE"), _txtRodape, btnSalvar, btnTeste
        };
        foreach (var c in ordenados) { c.Width = 344; fluxo.Controls.Add(c); }

        // ---- preview (direita) ----
        var painelPreview = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10), BackColor = Color.White };
        var cabPrev = new Label
        {
            Text = "PREVIEW DA BOBINA (58mm / 32 col)", Dock = DockStyle.Top, Height = 30,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold)
        };
        _preview.Name = "previewCupom";
        _preview.Dock = DockStyle.Fill;
        _preview.Multiline = true;
        _preview.ReadOnly = true;
        _preview.ScrollBars = ScrollBars.Vertical;
        _preview.WordWrap = false;
        _preview.Font = new Font("Courier New", 11F);
        _preview.BackColor = Color.FromArgb(250, 250, 240);
        painelPreview.Controls.Add(_preview);
        painelPreview.Controls.Add(cabPrev);

        Controls.Add(painelPreview);
        Controls.Add(fluxo);
    }

    private static Label Titulo(string t) => new()
    {
        Text = t, Font = new Font("Segoe UI", 12F, FontStyle.Bold), AutoSize = false,
        Height = 30, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(60, 60, 60)
    };
    private static Label Rotulo(string t) => new()
    {
        Text = t, AutoSize = false, Height = 22, TextAlign = ContentAlignment.MiddleLeft
    };

    private void CarregarConfig()
    {
        var c = _servico.LerConfigCupom();
        _txtEvento.Text = c.Evento;
        _txtSubtitulo.Text = c.Subtitulo;
        _rbCompleto.Checked = c.Modo == ModoCupom.Completo;
        _rbFicha.Checked = c.Modo == ModoCupom.FichaConsumo;
        _rbVales.Checked = c.Modo == ModoCupom.ReciboComVales;
        _rbSoVales.Checked = c.Modo == ModoCupom.SoVales;
        _chkSeparar.Checked = c.SepararPorItem;
        _txtRodape.Text = c.Rodape;
        AtualizarEstado();
    }

    private ConfigCupom ConfigAtual() => new()
    {
        Evento = _txtEvento.Text,
        Subtitulo = _txtSubtitulo.Text,
        Modo = _rbFicha.Checked ? ModoCupom.FichaConsumo
             : _rbVales.Checked ? ModoCupom.ReciboComVales
             : _rbSoVales.Checked ? ModoCupom.SoVales
             : ModoCupom.Completo,
        SepararPorItem = _chkSeparar.Checked,
        Rodape = _txtRodape.Text
    };

    private void AtualizarEstado()
    {
        // "separar por item" so faz sentido no modo Ficha
        _chkSeparar.Enabled = _rbFicha.Checked;
    }

    private void Salvar()
    {
        _servico.SalvarConfigCupom(ConfigAtual());
        MessageBox.Show("Layout do cupom salvo!", "Layout", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    /// <summary>Imprime uma amostra do cupom com o layout ATUAL (o que esta na tela) na impressora.</summary>
    private void ImprimirTeste()
    {
        if (!_servico.TemImpressora)
        {
            MessageBox.Show("Nenhuma impressora configurada.\nConfigure a impressora (F12) e tente de novo.",
                "Teste de layout", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        // usa o layout que esta na tela agora (mesmo antes de salvar), com a venda de exemplo.
        var (ok, msg) = PdvFesta.Core.EscPosPrinter.ImprimirTicket(
            _servico.ImpressoraPadrao, VendaExemplo(), ConfigAtual());
        if (ok)
            MessageBox.Show("Teste enviado para a impressora. Confira o papel.",
                "Teste de layout", MessageBoxButtons.OK, MessageBoxIcon.Information);
        else
            MessageBox.Show($"Nao foi possivel imprimir.\nDetalhe: {msg}",
                "Teste de layout", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    /// <summary>Venda de exemplo para o preview (nao vai ao banco).</summary>
    private static Venda VendaExemplo() => new()
    {
        Id = 123,
        TotalCentavos = 1700,
        Forma = FormaPagamento.Dinheiro,
        RecebidoCentavos = 2000,
        TrocoCentavos = 300,
        Itens =
        {
            // ProdutoId preenchido: no modo Vales, itens SEM ProdutoId sao tratados como
            // linha de desconto de combo e nao viram vale (por isso o exemplo precisa dele).
            new ItemVenda { ProdutoId = "quentao", Nome = "Quentao", PrecoUnitarioCentavos = 700, Quantidade = 1 },
            new ItemVenda { ProdutoId = "bolomilho", Nome = "Bolo de Milho", PrecoUnitarioCentavos = 500, Quantidade = 2 },
        }
    };

    private void AtualizarPreview()
    {
        const int W = 32;
        var linhas = CupomFormatter.MontarTicket(VendaExemplo(), ConfigAtual(), W);
        var sb = new StringBuilder();
        sb.AppendLine("+" + new string('-', W) + "+");
        foreach (var l in linhas)
        {
            if (l.Estilo == EstiloLinha.Corte)
            {
                sb.AppendLine("|" + Centralizar("- - -  8< corte  - - -", W) + "|");
                continue;
            }
            var txt = l.Estilo == EstiloLinha.Titulo ? Centralizar(l.Texto, W) : l.Texto;
            if (txt.Length > W) txt = txt[..W];
            sb.AppendLine("|" + txt.PadRight(W) + "|");
        }
        sb.AppendLine("+" + new string('-', W) + "+");
        _preview.Text = sb.ToString();
    }

    private static string Centralizar(string t, int w)
    {
        if (t.Length >= w) return t[..w];
        int esq = (w - t.Length) / 2;
        return new string(' ', esq) + t + new string(' ', w - t.Length - esq);
    }
}
