using System.Runtime.Versioning;
using PdvFesta.Core;

namespace PdvFesta.App;

/// <summary>
/// TROCA DE OPERADOR (passar o bastao entre voluntarios) com BATIMENTO DE CAIXA OPCIONAL:
///  - Sempre: fecha o turno atual e abre um novo para o proximo operador.
///  - Opcional (checkbox): confere a gaveta AGORA — mostra o esperado (Total em Gaveta),
///    pede o valor contado e exibe a DIFERENCA (sobra/falta), tudo antes de trocar.
/// Festa corrida passa o bastao direto; festa organizada confere na troca.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FormTrocaOperador : Form
{
    private readonly Servico _servico;
    private readonly TextBox _txtOperador = new();
    private readonly CheckBox _chkConferir = new();
    private readonly Label _lblEsperado = new();
    private readonly TextBox _txtContado = new();
    private readonly Label _lblDiferenca = new();
    private readonly TextBox _txtFundo = new();

    public FormTrocaOperador(Servico servico)
    {
        _servico = servico;
        Text = "Troca de Operador";
        Name = "FormTrocaOperador";
        Icon = Marca.Icone();
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        ClientSize = new Size(460, 470);
        Font = new Font("Segoe UI", 11F);
        KeyPreview = true;

        MontarLayout();
        AtualizarEstado();

        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
        };
        Shown += (s, e) => _txtOperador.Focus();
    }

    private void MontarLayout()
    {
        var titulo = new Label
        {
            Text = "TROCA DE OPERADOR", Dock = DockStyle.Top, Height = 48,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 15F, FontStyle.Bold),
            BackColor = Marca.Vermelho, ForeColor = Color.White
        };

        var painel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(16), AutoScroll = true
        };

        painel.Controls.Add(Rotulo("Novo operador:"));
        _txtOperador.Name = "txtNovoOperador"; _txtOperador.Dock = DockStyle.Top;
        _txtOperador.Font = new Font("Segoe UI", 14F);
        painel.Controls.Add(_txtOperador);

        _chkConferir.Name = "chkConferirGaveta";
        _chkConferir.Text = "Conferir a gaveta agora (batimento de caixa)";
        _chkConferir.Dock = DockStyle.Top; _chkConferir.Height = 40;
        _chkConferir.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        _chkConferir.CheckedChanged += (s, e) => AtualizarEstado();
        painel.Controls.Add(_chkConferir);

        _lblEsperado.Dock = DockStyle.Top; _lblEsperado.Height = 30;
        _lblEsperado.Font = new Font("Segoe UI", 11F);
        painel.Controls.Add(_lblEsperado);

        painel.Controls.Add(Rotulo("Contei na gaveta (R$):"));
        _txtContado.Name = "txtContado"; _txtContado.Dock = DockStyle.Top;
        _txtContado.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        _txtContado.TextAlign = HorizontalAlignment.Right; _txtContado.Text = "0,00";
        _txtContado.TextChanged += (s, e) => AtualizarDiferenca();
        painel.Controls.Add(_txtContado);

        _lblDiferenca.Dock = DockStyle.Top; _lblDiferenca.Height = 34;
        _lblDiferenca.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
        _lblDiferenca.TextAlign = ContentAlignment.MiddleLeft;
        painel.Controls.Add(_lblDiferenca);

        painel.Controls.Add(Rotulo("Fundo do novo turno / troco que fica (R$):"));
        _txtFundo.Name = "txtFundoNovo"; _txtFundo.Dock = DockStyle.Top;
        _txtFundo.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        _txtFundo.TextAlign = HorizontalAlignment.Right; _txtFundo.Text = "0,00";
        painel.Controls.Add(_txtFundo);

        // empilhamento reverso do TableLayoutPanel: adiciona os controles ao painel na ordem
        // e depois inverte a colecao para exibir de cima pra baixo.
        InverterOrdem(painel);

        var btn = new Button
        {
            Name = "btnConfirmarTroca",
            Text = "CONFIRMAR TROCA (Enter)", Dock = DockStyle.Bottom, Height = 56,
            BackColor = Color.FromArgb(0, 150, 0), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 12F, FontStyle.Bold)
        };
        btn.Click += (s, e) => Confirmar();

        Controls.Add(painel);
        Controls.Add(titulo);
        Controls.Add(btn);
    }

    private static Label Rotulo(string texto) => new()
    {
        Text = texto, Dock = DockStyle.Top, Height = 26,
        TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(70, 70, 70)
    };

    // TableLayoutPanel com Dock.Top empilha na ordem de adicao (1o adicionado no topo).
    // Como adicionamos na ordem natural, nao precisamos inverter; metodo mantido no-op
    // para clareza caso o layout mude.
    private static void InverterOrdem(TableLayoutPanel _) { }

    private void AtualizarEstado()
    {
        bool conferir = _chkConferir.Checked;
        _lblEsperado.Visible = conferir;
        _txtContado.Visible = conferir;
        _lblDiferenca.Visible = conferir;

        if (conferir)
        {
            var resumo = _servico.ResumoTurnoAtual();
            _lblEsperado.Text = $"Esperado na gaveta: {Dinheiro.Formatar(resumo.TotalGavetaCentavos)}";
            // sugere o fundo do novo turno = o esperado (o dinheiro que fica na gaveta)
            _txtFundo.Text = Dinheiro.FormatarSemSimbolo(resumo.TotalGavetaCentavos);
            AtualizarDiferenca();
        }
    }

    private void AtualizarDiferenca()
    {
        if (!_chkConferir.Checked) return;
        var contado = Dinheiro.ParseCentavos(_txtContado.Text);
        if (contado is null) { _lblDiferenca.Text = ""; return; }

        var r = _servico.BaterGaveta(contado.Value);
        if (r.Bate)
        {
            _lblDiferenca.Text = "Confere! A gaveta bate certinho.";
            _lblDiferenca.ForeColor = Color.FromArgb(0, 130, 0);
        }
        else if (r.Sobra)
        {
            _lblDiferenca.Text = $"SOBRA: {Dinheiro.Formatar(r.DiferencaCentavos)}";
            _lblDiferenca.ForeColor = Color.FromArgb(0, 100, 160);
        }
        else
        {
            _lblDiferenca.Text = $"FALTA: {Dinheiro.Formatar(-r.DiferencaCentavos)}";
            _lblDiferenca.ForeColor = Color.FromArgb(180, 0, 0);
        }
    }

    private void Confirmar()
    {
        var operador = _txtOperador.Text.Trim();
        if (string.IsNullOrWhiteSpace(operador))
        {
            MessageBox.Show("Informe o nome do novo operador.", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtOperador.Focus();
            return;
        }

        var fundo = Dinheiro.ParseCentavos(_txtFundo.Text);
        if (fundo is null)
        {
            MessageBox.Show("Informe um valor valido para o fundo do novo turno (ex: 100,00).",
                Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtFundo.Focus(); _txtFundo.SelectAll();
            return;
        }

        // Se conferiu e nao bate, confirma que o operador quer prosseguir mesmo assim.
        if (_chkConferir.Checked)
        {
            var contado = Dinheiro.ParseCentavos(_txtContado.Text) ?? 0;
            var r = _servico.BaterGaveta(contado);
            if (!r.Bate)
            {
                var msg = r.Sobra
                    ? $"A gaveta tem SOBRA de {Dinheiro.Formatar(r.DiferencaCentavos)}."
                    : $"A gaveta tem FALTA de {Dinheiro.Formatar(-r.DiferencaCentavos)}.";
                var resp = MessageBox.Show(msg + "\n\nRegistrar a troca mesmo assim?", "Batimento",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (resp != DialogResult.Yes) return;
            }
        }

        _servico.TrocarOperador(operador, fundo.Value);
        DialogResult = DialogResult.OK;
        Close();
    }
}
