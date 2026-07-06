using System.Runtime.Versioning;
using PdvFesta.Core;

namespace PdvFesta.App;

/// <summary>
/// Tela de pagamento. Escolhe a forma (Dinheiro/Pix/Debito/Credito):
///  - Dinheiro: digita o recebido e ve o TROCO ao vivo; botoes de valor rapido
///    (Exato, R$ 20/50/100) para agilizar.
///  - Pix/Debito/Credito: valor ja vem PRE-PREENCHIDO com o total (troco zero).
/// Anti-crash: se a impressora falhar, oferece Repetir/Ignorar (a venda ja foi salva).
/// Atalhos: D=Dinheiro, P=Pix, B=Debito, C=Credito, Enter=confirmar, Esc=voltar.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FormPagamento : Form
{
    private readonly Servico _servico;
    private FormaPagamento _forma = FormaPagamento.Dinheiro;

    private readonly Label _lblTroco = new();
    private readonly TextBox _txtRecebido = new();
    private readonly FlowLayoutPanel _painelRapido = new();
    private readonly Button _btnDinheiro = new();
    private readonly Button _btnPix = new();
    private readonly Button _btnDebito = new();
    private readonly Button _btnCredito = new();
    private readonly Button _btnCortesia = new();
    private readonly Label _lblNomeCortesia = new();
    private readonly TextBox _txtNomeCortesia = new();

    private readonly int _totalCentavos;

    public FormPagamento(Servico servico)
    {
        _servico = servico;
        _totalCentavos = servico.Carrinho.TotalCentavos;

        Text = "Pagamento";
        Name = "FormPagamento";
        Icon = Marca.Icone();
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        ClientSize = new Size(480, 640);
        KeyPreview = true;
        Font = new Font("Segoe UI", 12F);

        MontarLayout();
        SelecionarForma(FormaPagamento.Dinheiro);
        KeyDown += FormPagamento_KeyDown;
    }

    private void MontarLayout()
    {
        var lblTotal = new Label
        {
            Text = "TOTAL: " + Dinheiro.Formatar(_totalCentavos),
            Font = new Font("Segoe UI", 22F, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 100, 0),
            TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Top, Height = 64
        };

        // ---- formas de pagamento (4 colunas) ----
        var painelFormas = new TableLayoutPanel
        {
            Dock = DockStyle.Top, Height = 76, ColumnCount = 4, RowCount = 1, Padding = new Padding(8)
        };
        for (int i = 0; i < 4; i++) painelFormas.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        ConfigBotaoForma(_btnDinheiro, "Dinheiro\n(D)", FormaPagamento.Dinheiro);
        ConfigBotaoForma(_btnPix, "Pix\n(P)", FormaPagamento.Pix);
        ConfigBotaoForma(_btnDebito, "Débito\n(B)", FormaPagamento.CartaoDebito);
        ConfigBotaoForma(_btnCredito, "Crédito\n(C)", FormaPagamento.CartaoCredito);
        painelFormas.Controls.Add(_btnDinheiro, 0, 0);
        painelFormas.Controls.Add(_btnPix, 1, 0);
        painelFormas.Controls.Add(_btnDebito, 2, 0);
        painelFormas.Controls.Add(_btnCredito, 3, 0);

        // ---- botao CORTESIA (linha propria, largo) ----
        _btnCortesia.Text = "🎁 CORTESIA (brinde - nao cobra)  [O]";
        _btnCortesia.Dock = DockStyle.Top; _btnCortesia.Height = 46; _btnCortesia.Margin = new Padding(8, 0, 8, 4);
        _btnCortesia.FlatStyle = FlatStyle.Flat; _btnCortesia.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        _btnCortesia.BackColor = Color.FromArgb(150, 90, 160); _btnCortesia.ForeColor = Color.White;
        _btnCortesia.TabStop = false;
        _btnCortesia.Click += (s, e) => SelecionarForma(FormaPagamento.Cortesia);

        // campo do NOME de quem recebeu a cortesia (aparece so no modo Cortesia)
        _lblNomeCortesia.Text = "Nome de quem recebeu (cortesia):"; _lblNomeCortesia.Dock = DockStyle.Top;
        _lblNomeCortesia.Height = 26; _lblNomeCortesia.TextAlign = ContentAlignment.MiddleLeft;
        _lblNomeCortesia.Padding = new Padding(12, 0, 0, 0); _lblNomeCortesia.Visible = false;
        _txtNomeCortesia.Name = "txtNomeCortesia"; _txtNomeCortesia.Dock = DockStyle.Top;
        _txtNomeCortesia.Font = new Font("Segoe UI", 14F); _txtNomeCortesia.Visible = false;

        var lblRec = new Label
        {
            Text = "Valor recebido (R$):", Dock = DockStyle.Top, Height = 28,
            TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(12, 0, 0, 0)
        };
        _txtRecebido.Name = "txtRecebido";
        _txtRecebido.Dock = DockStyle.Top;
        _txtRecebido.Font = new Font("Segoe UI", 22F, FontStyle.Bold);
        _txtRecebido.TextAlign = HorizontalAlignment.Right;
        _txtRecebido.TextChanged += (s, e) => AtualizarTroco();

        // ---- botoes de valor rapido (estilo PDV) ----
        _painelRapido.Dock = DockStyle.Top;
        _painelRapido.Height = 150;
        _painelRapido.Padding = new Padding(8);
        _painelRapido.AutoScroll = true;
        // "Exato" preenche o total certinho; "Limpar" zera. As notas/moedas do Real SOMAM
        // (cliente pagou com varias: clica R$50 + R$20 -> 70,00), como num PDV de verdade.
        _painelRapido.Controls.Add(BotaoExato());
        _painelRapido.Controls.Add(BotaoLimpar());
        foreach (var nota in new[] { 100, 200, 500, 1000, 2000, 5000, 10000, 20000 })
            _painelRapido.Controls.Add(BotaoSomarNota(nota));

        _lblTroco.Dock = DockStyle.Top; _lblTroco.Height = 72;
        _lblTroco.Font = new Font("Segoe UI", 26F, FontStyle.Bold);
        _lblTroco.TextAlign = ContentAlignment.MiddleCenter;
        _lblTroco.ForeColor = Color.FromArgb(0, 0, 150);
        _lblTroco.Name = "lblTroco";

        var btnOk = new Button
        {
            Name = "btnConfirmar",
            Text = "CONFIRMAR (Enter)", Dock = DockStyle.Bottom, Height = 64,
            BackColor = Color.FromArgb(0, 150, 0), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 15F, FontStyle.Bold)
        };
        btnOk.Click += (s, e) => Confirmar();

        // Dock: adiciona de baixo pra cima (o ultimo Add fica no TOPO)
        Controls.Add(_lblTroco);
        Controls.Add(_painelRapido);
        Controls.Add(_txtRecebido);
        Controls.Add(lblRec);
        Controls.Add(_txtNomeCortesia);      // campo nome (so visivel no modo Cortesia)
        Controls.Add(_lblNomeCortesia);
        Controls.Add(_btnCortesia);          // botao cortesia (linha propria)
        Controls.Add(painelFormas);
        Controls.Add(lblTotal);
        Controls.Add(btnOk);
    }

    private void ConfigBotaoForma(Button b, string texto, FormaPagamento forma)
    {
        b.Text = texto; b.Dock = DockStyle.Fill; b.FlatStyle = FlatStyle.Flat; b.Margin = new Padding(4);
        b.Font = new Font("Segoe UI", 11F, FontStyle.Bold); b.TabStop = false;
        b.Click += (s, e) => SelecionarForma(forma);
    }

    private static Button BaseBotao(string texto, string nome, Color cor)
    {
        return new Button
        {
            Text = texto, Width = 88, Height = 42, Margin = new Padding(4),
            FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            BackColor = cor, TabStop = false, Name = nome
        };
    }

    /// <summary>"Exato": preenche o valor recebido com o total (troco 0).</summary>
    private Button BotaoExato()
    {
        var b = BaseBotao("Exato", "btnRapido_" + _totalCentavos, Color.FromArgb(210, 235, 210));
        b.Click += (s, e) => { DefinirRecebido(_totalCentavos); };
        return b;
    }

    /// <summary>"Limpar": zera o valor recebido.</summary>
    private Button BotaoLimpar()
    {
        var b = BaseBotao("Limpar", "btnLimparRecebido", Color.FromArgb(245, 220, 210));
        b.Click += (s, e) => { DefinirRecebido(0); };
        return b;
    }

    /// <summary>Botao de nota/moeda do Real: SOMA o valor ao recebido (estilo PDV).</summary>
    private Button BotaoSomarNota(int centavos)
    {
        var b = BaseBotao(Dinheiro.Formatar(centavos).Replace("R$ ", "R$"),
                          "btnNota_" + centavos, Color.FromArgb(235, 235, 235));
        b.Click += (s, e) =>
        {
            var atual = Dinheiro.ParseCentavos(_txtRecebido.Text) ?? 0;
            DefinirRecebido(atual + centavos);
        };
        return b;
    }

    private void DefinirRecebido(int centavos)
    {
        _txtRecebido.Text = (centavos / 100m).ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
        _txtRecebido.Focus(); _txtRecebido.SelectAll();
    }

    private void SelecionarForma(FormaPagamento forma)
    {
        _forma = forma;
        Realcar(_btnDinheiro, forma == FormaPagamento.Dinheiro);
        Realcar(_btnPix, forma == FormaPagamento.Pix);
        Realcar(_btnDebito, forma == FormaPagamento.CartaoDebito);
        Realcar(_btnCredito, forma == FormaPagamento.CartaoCredito);
        // botao cortesia: roxo normal / verde-escuro quando selecionado
        _btnCortesia.BackColor = forma == FormaPagamento.Cortesia
            ? Color.FromArgb(0, 120, 0) : Color.FromArgb(150, 90, 160);

        bool dinheiro = forma == FormaPagamento.Dinheiro;
        bool cortesia = forma == FormaPagamento.Cortesia;

        _txtRecebido.Enabled = dinheiro;
        _painelRapido.Visible = dinheiro;
        // campo do nome so aparece na cortesia
        _lblNomeCortesia.Visible = cortesia;
        _txtNomeCortesia.Visible = cortesia;

        if (dinheiro)
        {
            _txtRecebido.Text = "";
            _txtRecebido.Focus();
        }
        else if (cortesia)
        {
            // cortesia: brinde -> recebido 0, sem troco. Foca o nome pra registrar quem recebeu.
            _txtRecebido.Text = "0,00";
            _txtNomeCortesia.Focus();
        }
        else
        {
            // Pix/Debito/Credito: valor ja pre-preenchido com o total (troco zero).
            _txtRecebido.Text = (_totalCentavos / 100m).ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
        }
        AtualizarTroco();
    }

    private static void Realcar(Button b, bool ativo)
    {
        b.BackColor = ativo ? Color.FromArgb(0, 150, 0) : Color.Gainsboro;
        b.ForeColor = ativo ? Color.White : Color.Black;
    }

    private void AtualizarTroco()
    {
        if (_forma != FormaPagamento.Dinheiro)
        {
            _lblTroco.ForeColor = Color.FromArgb(0, 100, 0);
            _lblTroco.Text = "Troco: R$ 0,00";
            return;
        }
        var rec = Dinheiro.ParseCentavos(_txtRecebido.Text);
        if (rec is null) { _lblTroco.Text = ""; return; }
        if (rec < _totalCentavos)
        {
            _lblTroco.ForeColor = Color.FromArgb(180, 0, 0);
            _lblTroco.Text = "Falta " + Dinheiro.Formatar(_totalCentavos - rec.Value);
        }
        else
        {
            _lblTroco.ForeColor = Color.FromArgb(0, 0, 150);
            _lblTroco.Text = "TROCO: " + Dinheiro.Formatar(rec.Value - _totalCentavos);
        }
    }

    private void FormPagamento_KeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            // atalhos de forma de pagamento: SuppressKeyPress impede a LETRA de ser digitada
            // no campo de valor recebido (ex: apertar "P" escrevia "p7,00").
            case Keys.D: SelecionarForma(FormaPagamento.Dinheiro); e.Handled = e.SuppressKeyPress = true; break;
            case Keys.P: SelecionarForma(FormaPagamento.Pix); e.Handled = e.SuppressKeyPress = true; break;
            case Keys.B: SelecionarForma(FormaPagamento.CartaoDebito); e.Handled = e.SuppressKeyPress = true; break;
            case Keys.C: SelecionarForma(FormaPagamento.CartaoCredito); e.Handled = e.SuppressKeyPress = true; break;
            case Keys.O: SelecionarForma(FormaPagamento.Cortesia); e.Handled = e.SuppressKeyPress = true; break;
            case Keys.Enter:
                // no campo do nome da cortesia, Enter confirma tambem (nao quebra linha)
                Confirmar(); e.Handled = e.SuppressKeyPress = true; break;
            case Keys.Escape: DialogResult = DialogResult.Cancel; Close(); break;
        }
    }

    private void Confirmar()
    {
        int recebido = 0;
        string observacao = "";

        if (_forma == FormaPagamento.Dinheiro)
        {
            var rec = Dinheiro.ParseCentavos(_txtRecebido.Text);
            if (rec is null || rec < _totalCentavos)
            {
                MessageBox.Show("Valor recebido insuficiente para o troco.", "Pagamento",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtRecebido.Focus(); _txtRecebido.SelectAll();
                return;
            }
            recebido = rec.Value;
        }
        else if (_forma == FormaPagamento.Cortesia)
        {
            // cortesia exige o NOME de quem recebeu (rastreabilidade dos brindes)
            var nome = _txtNomeCortesia.Text.Trim();
            if (string.IsNullOrWhiteSpace(nome))
            {
                MessageBox.Show("Informe o NOME de quem recebeu a cortesia.", "Cortesia",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtNomeCortesia.Focus();
                return;
            }
            observacao = "CORTESIA: " + nome;
            recebido = 0;   // brinde: nao entra dinheiro
        }

        Venda venda;
        bool impressaoOk; string impressaoMsg;
        try
        {
            (venda, impressaoOk, impressaoMsg) = _servico.FinalizarVenda(_forma, recebido, operador: "", observacao: observacao);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Erro ao finalizar a venda: " + ex.Message, "Pagamento",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        // Anti-crash da impressora: a venda JA foi salva.
        // - SEM impressora configurada: o caixa NAO trava. Segue a venda em silencio
        //   (nada de popup "Repetir/Cancelar" que so faz sentido com impressora de verdade).
        // - COM impressora que falhou (cabo/papel): oferece Repetir/Ignorar.
        if (!impressaoOk && _servico.TemImpressora)
            OfferecerReimpressao(venda, impressaoMsg);

        DialogResult = DialogResult.OK;
        Close();
    }

    /// <summary>Loop amigavel de reimpressao (Repetir/Ignorar) sem nunca crashar.</summary>
    private void OfferecerReimpressao(Venda venda, string msg)
    {
        while (true)
        {
            var r = MessageBox.Show(
                "Erro na impressora. Verifique o cabo e o papel.\n" +
                "A VENDA FOI REGISTRADA no sistema normalmente.\n\n" +
                $"Detalhe: {msg}\n\nDeseja tentar imprimir novamente?",
                "Impressora", MessageBoxButtons.RetryCancel, MessageBoxIcon.Warning);

            if (r != DialogResult.Retry) return;      // Ignorar

            var (ok, novaMsg) = _servico.ImprimirVenda(venda);
            if (ok) return;                           // imprimiu -> sai
            msg = novaMsg;                            // falhou de novo -> repete o prompt
        }
    }
}
