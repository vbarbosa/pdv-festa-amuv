using System.Runtime.Versioning;
using PdvFesta.Core;

namespace PdvFesta.App;

/// <summary>
/// Tela principal do caixa. Layout em 2 colunas:
///  - Esquerda: grade de botoes grandes de produtos (clique/touch).
///  - Direita: carrinho + TOTAL em fonte gigante.
/// Operavel 100% por teclado: 1-9 adiciona itens, F2/Enter paga, Esc limpa, F12 config.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FormVendas : Form
{
    private readonly Servico _servico;
    private readonly ListView _lstCarrinho = new();
    private readonly Label _lblTotal = new();
    private readonly FlowLayoutPanel _painelProdutos = new();
    private List<Produto> _produtos = new();

    public FormVendas(Servico servico)
    {
        _servico = servico;
        Name = "FormVendas";
        Text = "PDV Festa - Caixa  [1-9 add | F2 pagar | Esc limpar | F8 backup | F9 fechamento | F12 impressora]";
        Icon = Marca.Icone();          // marca AMUV na barra de titulo
        WindowState = FormWindowState.Maximized;
        KeyPreview = true;             // recebe atalhos globais antes dos controles
        BackColor = Color.White;
        Font = new Font("Segoe UI", 11F);

        MontarLayout();
        CarregarProdutos();
        AtualizarCarrinho();

        KeyDown += FormVendas_KeyDown;
    }

    private void MontarLayout()
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 900,
            FixedPanel = FixedPanel.Panel2,
            IsSplitterFixed = false
        };
        Controls.Add(split);

        // ---- ESQUERDA: produtos ----
        _painelProdutos.Dock = DockStyle.Fill;
        _painelProdutos.AutoScroll = true;
        _painelProdutos.Padding = new Padding(10);
        split.Panel1.Controls.Add(_painelProdutos);

        // ---- DIREITA: carrinho ----
        var pDir = split.Panel2;
        pDir.BackColor = Color.FromArgb(245, 245, 245);

        var lblCab = new Label
        {
            Text = "CARRINHO", Dock = DockStyle.Top, Height = 40,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            BackColor = Marca.Vermelho, ForeColor = Color.White  // sotaque de marca AMUV
        };

        _lstCarrinho.Dock = DockStyle.Fill;
        _lstCarrinho.View = View.Details;
        _lstCarrinho.FullRowSelect = true;
        _lstCarrinho.Font = new Font("Segoe UI", 12F);
        _lstCarrinho.Columns.Add("Item", 200);
        _lstCarrinho.Columns.Add("Qtd", 55, HorizontalAlignment.Center);
        _lstCarrinho.Columns.Add("Total", 90, HorizontalAlignment.Right);
        _lstCarrinho.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Delete && _lstCarrinho.SelectedItems.Count > 0)
            {
                var id = (string)_lstCarrinho.SelectedItems[0].Tag!;
                _servico.Carrinho.Remover(id);
                AtualizarCarrinho();
            }
        };

        // TOTAL gigante
        _lblTotal.Name = "lblTotal";   // AutomationId para E2E
        _lblTotal.Dock = DockStyle.Bottom;
        _lblTotal.Height = 110;
        _lblTotal.TextAlign = ContentAlignment.MiddleCenter;
        _lblTotal.Font = new Font("Segoe UI", 40F, FontStyle.Bold);
        _lblTotal.ForeColor = Color.FromArgb(0, 120, 0);
        _lblTotal.BackColor = Color.White;

        // Barra de botoes de acao
        var barra = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom, Height = 70, ColumnCount = 3, RowCount = 1
        };
        barra.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        barra.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        barra.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));

        var btnPagar = BotaoAcao("PAGAR (F2)", Color.FromArgb(0, 150, 0));
        btnPagar.Click += (s, e) => AbrirPagamento();
        var btnLimpar = BotaoAcao("Limpar (Esc)", Color.FromArgb(180, 60, 60));
        btnLimpar.Click += (s, e) => LimparCarrinho();
        var btnFech = BotaoAcao("Fechamento (F9)", Color.FromArgb(70, 70, 90));
        btnFech.Click += (s, e) => AbrirFechamento();

        barra.Controls.Add(btnPagar, 0, 0);
        barra.Controls.Add(btnLimpar, 1, 0);
        barra.Controls.Add(btnFech, 2, 0);

        pDir.Controls.Add(_lstCarrinho);
        pDir.Controls.Add(_lblTotal);
        pDir.Controls.Add(barra);
        pDir.Controls.Add(lblCab);
    }

    private static Button BotaoAcao(string texto, Color cor) => new()
    {
        Text = texto, Dock = DockStyle.Fill,
        BackColor = cor, ForeColor = Color.White, FlatStyle = FlatStyle.Flat,
        Font = new Font("Segoe UI", 12F, FontStyle.Bold), TabStop = false
    };

    private void CarregarProdutos()
    {
        _produtos = _servico.Produtos();
        _painelProdutos.Controls.Clear();

        foreach (var p in _produtos)
        {
            var atalho = p.Atalho is >= 1 and <= 9 ? $"[{p.Atalho}] " : "";
            var btn = new Button
            {
                Text = $"{atalho}{p.Nome}\n{CupomFormatter.Moeda(p.PrecoCentavos)}",
                Width = 200, Height = 90, Margin = new Padding(8),
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                BackColor = CorCategoria(p.Categoria), FlatStyle = FlatStyle.Flat,
                TabStop = false, Tag = p.Id
            };
            btn.Name = "btnProduto_" + p.Id;   // AutomationId para testes E2E (FlaUI)
            btn.Click += (s, e) => { _servico.Carrinho.Adicionar(p); AtualizarCarrinho(); };
            _painelProdutos.Controls.Add(btn);
        }
    }

    private static Color CorCategoria(string cat) => cat switch
    {
        "Bebidas" => Color.FromArgb(210, 230, 255),
        "Comidas" => Color.FromArgb(255, 235, 200),
        "Doces" => Color.FromArgb(255, 220, 235),
        "Bingo" => Color.FromArgb(225, 215, 255),
        "Jogos" => Color.FromArgb(215, 255, 220),
        "Promocoes" => Color.FromArgb(255, 245, 180),
        _ => Color.FromArgb(235, 235, 235)
    };

    private void AtualizarCarrinho()
    {
        _lstCarrinho.BeginUpdate();
        _lstCarrinho.Items.Clear();
        foreach (var i in _servico.Carrinho.Itens)
        {
            var lvi = new ListViewItem(i.Nome) { Tag = i.ProdutoId };
            lvi.SubItems.Add(i.Quantidade.ToString());
            lvi.SubItems.Add(CupomFormatter.Moeda(i.SubtotalCentavos));
            _lstCarrinho.Items.Add(lvi);
        }
        _lstCarrinho.EndUpdate();
        _lblTotal.Text = CupomFormatter.Moeda(_servico.Carrinho.TotalCentavos);
    }

    // ---------------- ATALHOS DE TECLADO ----------------
    private void FormVendas_KeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.D1: case Keys.D2: case Keys.D3:
            case Keys.D4: case Keys.D5: case Keys.D6:
            case Keys.D7: case Keys.D8: case Keys.D9:
                AdicionarPorAtalho(e.KeyCode - Keys.D0);
                e.Handled = true;
                break;
            case Keys.NumPad1: case Keys.NumPad2: case Keys.NumPad3:
            case Keys.NumPad4: case Keys.NumPad5: case Keys.NumPad6:
            case Keys.NumPad7: case Keys.NumPad8: case Keys.NumPad9:
                AdicionarPorAtalho(e.KeyCode - Keys.NumPad0);
                e.Handled = true;
                break;
            case Keys.F2:
            case Keys.Enter:
                AbrirPagamento(); e.Handled = true;
                break;
            case Keys.Escape:
                LimparCarrinho(); e.Handled = true;
                break;
            case Keys.F12:
                AbrirConfigImpressora(); e.Handled = true;
                break;
            case Keys.F9:
                AbrirFechamento(); e.Handled = true;
                break;
            case Keys.F8:
                AbrirBackup(); e.Handled = true;
                break;
        }
    }

    private void AbrirBackup()
    {
        using var f = new FormBackup(_servico);
        f.ShowDialog(this);
    }

    private void AdicionarPorAtalho(int numero)
    {
        var p = _produtos.FirstOrDefault(x => x.Atalho == numero);
        if (p is not null) { _servico.Carrinho.Adicionar(p); AtualizarCarrinho(); }
    }

    private void LimparCarrinho()
    {
        if (_servico.Carrinho.Itens.Count == 0) return;
        _servico.Carrinho.Limpar();
        AtualizarCarrinho();
    }

    private void AbrirPagamento()
    {
        if (_servico.Carrinho.Itens.Count == 0) return;
        using var f = new FormPagamento(_servico);
        if (f.ShowDialog(this) == DialogResult.OK)
            AtualizarCarrinho();
    }

    private void AbrirConfigImpressora()
    {
        using var f = new FormPrinterConfig(_servico);
        f.ShowDialog(this);
    }

    private void AbrirFechamento()
    {
        using var f = new FormFechamento(_servico);
        f.ShowDialog(this);
    }
}
