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
        ClientSize = new Size(480, 560);
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

        // Dock: adiciona de baixo pra cima
        Controls.Add(_lblTroco);
        Controls.Add(_painelRapido);
        Controls.Add(_txtRecebido);
        Controls.Add(lblRec);
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

        bool dinheiro = forma == FormaPagamento.Dinheiro;
        _txtRecebido.Enabled = dinheiro;
        _painelRapido.Visible = dinheiro;

        if (dinheiro)
        {
            _txtRecebido.Text = "";
            _txtRecebido.Focus();
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
            case Keys.Enter: Confirmar(); e.Handled = e.SuppressKeyPress = true; break;
            case Keys.Escape: DialogResult = DialogResult.Cancel; Close(); break;
        }
    }

    private void Confirmar()
    {
        int recebido = 0;
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

        Venda venda;
        bool impressaoOk; string impressaoMsg;
        try
        {
            (venda, impressaoOk, impressaoMsg) = _servico.FinalizarVenda(_forma, recebido, operador: "");
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
