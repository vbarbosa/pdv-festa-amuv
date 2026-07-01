using System.Runtime.Versioning;
using PdvFesta.Core;

namespace PdvFesta.App;

/// <summary>
/// Tela de pagamento: escolhe forma (Dinheiro/Pix/Cartao) e, no dinheiro,
/// digita o valor recebido para calcular o TROCO ao vivo.
/// Atalhos: D=Dinheiro, P=Pix, C=Cartao, Enter=confirmar, Esc=voltar.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FormPagamento : Form
{
    private readonly Servico _servico;
    private FormaPagamento _forma = FormaPagamento.Dinheiro;

    private readonly Label _lblTotal = new();
    private readonly Label _lblTroco = new();
    private readonly TextBox _txtRecebido = new();
    private readonly Button _btnDinheiro = new();
    private readonly Button _btnPix = new();
    private readonly Button _btnCartao = new();

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
        ClientSize = new Size(460, 420);
        KeyPreview = true;
        Font = new Font("Segoe UI", 12F);

        MontarLayout();
        SelecionarForma(FormaPagamento.Dinheiro);
        KeyDown += FormPagamento_KeyDown;
    }

    private void MontarLayout()
    {
        _lblTotal.Text = "TOTAL: " + CupomFormatter.Moeda(_totalCentavos);
        _lblTotal.Font = new Font("Segoe UI", 22F, FontStyle.Bold);
        _lblTotal.ForeColor = Color.FromArgb(0, 100, 0);
        _lblTotal.TextAlign = ContentAlignment.MiddleCenter;
        _lblTotal.Dock = DockStyle.Top; _lblTotal.Height = 70;

        var painelFormas = new TableLayoutPanel
        {
            Dock = DockStyle.Top, Height = 80, ColumnCount = 3, RowCount = 1, Padding = new Padding(10)
        };
        for (int i = 0; i < 3; i++) painelFormas.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3F));

        ConfigBotaoForma(_btnDinheiro, "Dinheiro\n(D)", FormaPagamento.Dinheiro);
        ConfigBotaoForma(_btnPix, "Pix\n(P)", FormaPagamento.Pix);
        ConfigBotaoForma(_btnCartao, "Cartao\n(C)", FormaPagamento.Cartao);
        painelFormas.Controls.Add(_btnDinheiro, 0, 0);
        painelFormas.Controls.Add(_btnPix, 1, 0);
        painelFormas.Controls.Add(_btnCartao, 2, 0);

        var lblRec = new Label
        {
            Text = "Valor recebido (R$):", Dock = DockStyle.Top, Height = 30,
            TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(12, 0, 0, 0)
        };
        _txtRecebido.Name = "txtRecebido";
        _txtRecebido.Dock = DockStyle.Top;
        _txtRecebido.Font = new Font("Segoe UI", 20F, FontStyle.Bold);
        _txtRecebido.TextAlign = HorizontalAlignment.Right;
        _txtRecebido.Margin = new Padding(12);
        _txtRecebido.TextChanged += (s, e) => AtualizarTroco();

        _lblTroco.Dock = DockStyle.Top; _lblTroco.Height = 70;
        _lblTroco.Font = new Font("Segoe UI", 24F, FontStyle.Bold);
        _lblTroco.TextAlign = ContentAlignment.MiddleCenter;
        _lblTroco.ForeColor = Color.FromArgb(0, 0, 150);

        var btnOk = new Button
        {
            Name = "btnConfirmar",
            Text = "CONFIRMAR (Enter)", Dock = DockStyle.Bottom, Height = 60,
            BackColor = Color.FromArgb(0, 150, 0), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 14F, FontStyle.Bold)
        };
        btnOk.Click += (s, e) => Confirmar();

        // Ordem de Dock (adiciona de baixo pra cima)
        Controls.Add(_lblTroco);
        Controls.Add(_txtRecebido);
        Controls.Add(lblRec);
        Controls.Add(painelFormas);
        Controls.Add(_lblTotal);
        Controls.Add(btnOk);
    }

    private void ConfigBotaoForma(Button b, string texto, FormaPagamento forma)
    {
        b.Text = texto; b.Dock = DockStyle.Fill; b.FlatStyle = FlatStyle.Flat;
        b.Font = new Font("Segoe UI", 12F, FontStyle.Bold); b.TabStop = false;
        b.Click += (s, e) => SelecionarForma(forma);
    }

    private void SelecionarForma(FormaPagamento forma)
    {
        _forma = forma;
        _btnDinheiro.BackColor = forma == FormaPagamento.Dinheiro ? Color.FromArgb(0, 150, 0) : Color.Gainsboro;
        _btnDinheiro.ForeColor = forma == FormaPagamento.Dinheiro ? Color.White : Color.Black;
        _btnPix.BackColor = forma == FormaPagamento.Pix ? Color.FromArgb(0, 150, 0) : Color.Gainsboro;
        _btnPix.ForeColor = forma == FormaPagamento.Pix ? Color.White : Color.Black;
        _btnCartao.BackColor = forma == FormaPagamento.Cartao ? Color.FromArgb(0, 150, 0) : Color.Gainsboro;
        _btnCartao.ForeColor = forma == FormaPagamento.Cartao ? Color.White : Color.Black;

        bool dinheiro = forma == FormaPagamento.Dinheiro;
        _txtRecebido.Enabled = dinheiro;
        if (dinheiro) { _txtRecebido.Focus(); _txtRecebido.SelectAll(); }
        AtualizarTroco();
    }

    private int? RecebidoCentavos()
    {
        var txt = _txtRecebido.Text.Trim().Replace("R$", "").Replace(" ", "").Replace(".", "").Replace(',', '.');
        if (decimal.TryParse(txt, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var reais))
            return (int)Math.Round(reais * 100);
        return null;
    }

    private void AtualizarTroco()
    {
        if (_forma != FormaPagamento.Dinheiro)
        {
            _lblTroco.Text = "";
            return;
        }
        var rec = RecebidoCentavos();
        if (rec is null) { _lblTroco.Text = ""; return; }
        if (rec < _totalCentavos)
        {
            _lblTroco.ForeColor = Color.FromArgb(180, 0, 0);
            _lblTroco.Text = "Falta " + CupomFormatter.Moeda(_totalCentavos - rec.Value);
        }
        else
        {
            _lblTroco.ForeColor = Color.FromArgb(0, 0, 150);
            _lblTroco.Text = "TROCO: " + CupomFormatter.Moeda(rec.Value - _totalCentavos);
        }
    }

    private void FormPagamento_KeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.D: SelecionarForma(FormaPagamento.Dinheiro); e.Handled = true; break;
            case Keys.P: SelecionarForma(FormaPagamento.Pix); e.Handled = true; break;
            case Keys.C: SelecionarForma(FormaPagamento.Cartao); e.Handled = true; break;
            case Keys.Enter: Confirmar(); e.Handled = true; break;
            case Keys.Escape: DialogResult = DialogResult.Cancel; Close(); break;
        }
    }

    private void Confirmar()
    {
        int recebido = 0;
        if (_forma == FormaPagamento.Dinheiro)
        {
            var rec = RecebidoCentavos();
            if (rec is null || rec < _totalCentavos)
            {
                MessageBox.Show("Valor recebido insuficiente para o troco.", "Pagamento",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtRecebido.Focus(); _txtRecebido.SelectAll();
                return;
            }
            recebido = rec.Value;
        }

        try
        {
            _servico.FinalizarVenda(_forma, recebido, operador: "");
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Erro ao finalizar: " + ex.Message, "Pagamento",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
