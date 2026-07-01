using System.Runtime.Versioning;
using PdvFesta.Core;

namespace PdvFesta.App;

/// <summary>
/// Tela principal do caixa (estilo ERP/PDV classico), 100% ancorada (sem coords fixas):
///  - MenuStrip (Arquivo/Config/Ferramentas/Ajuda) + StatusStrip (atalhos, BD, impressora, caixa).
///  - SplitContainer vertical com o CARRINHO fixo (Panel2, min 420px) que nunca colapsa.
///  - Esquerda: catalogo em abas (TabControl) por categoria, botoes sobrios com atalho no canto.
///  - Direita: DataGridView do carrinho + TOTAL gigante + PAGAR/CANCELAR.
/// Operavel por teclado: 1-9 adiciona, F2/Enter paga, Esc limpa, F8/F9/F12 nos menus.
/// Telas sensiveis exigem senha de admin; catalogo recarrega dinamicamente.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FormVendas : Form
{
    private readonly Servico _servico;
    private readonly DataGridView _grid = new();
    private readonly Label _lblTotal = new();
    private readonly TabControl _abas = new();

    // status bar
    private readonly ToolStripStatusLabel _stBd = new();
    private readonly ToolStripStatusLabel _stImpressora = new();
    private readonly ToolStripStatusLabel _stCaixa = new();

    private List<Produto> _produtos = new();

    public FormVendas(Servico servico)
    {
        _servico = servico;
        Name = "FormVendas";
        Text = "PDV Festa Junina - Terminal Caixa 01";
        Icon = Marca.Icone();
        WindowState = FormWindowState.Maximized;
        KeyPreview = true;
        BackColor = Color.White;
        Font = new Font("Segoe UI", 11F);
        MinimumSize = new Size(1000, 640);

        MontarLayout();
        RecarregarProdutos();
        AtualizarCarrinho();
        AtualizarStatus();

        KeyDown += FormVendas_KeyDown;
        Shown += (s, e) => GarantirCaixaAberto();
    }

    // ======================= LAYOUT =======================

    private SplitContainer _split = null!;
    private const int Panel1Min = 360, Panel2Min = 420, Panel2Desejado = 460;
    private bool _splitConfigurado;

    private void MontarLayout()
    {
        // Os MinSize NAO sao definidos aqui: durante a construcao o split ainda tem
        // largura default (~150px) e setar Panel2MinSize=420 lancaria InvalidOperation
        // ("SplitterDistance deve ficar entre Panel1MinSize e Width - Panel2MinSize").
        // Sao aplicados em AjustarSplitter(), apos a janela maximizar (largura real).
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            FixedPanel = FixedPanel.Panel2    // o carrinho tem largura fixa
        };
        _split = split;

        // ---- ESQUERDA: catalogo em abas ----
        _abas.Dock = DockStyle.Fill;
        _abas.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        _abas.Multiline = false;   // muitas abas -> setas de rolagem horizontais automaticas
        _abas.SizeMode = TabSizeMode.Normal;
        split.Panel1.Controls.Add(_abas);

        // ---- DIREITA: carrinho ----
        MontarPainelCarrinho(split.Panel2);

        // ordem de Dock: fill primeiro, depois bordas
        Controls.Add(split);
        Controls.Add(CriarStatusStrip());
        Controls.Add(CriarMenu());

        // posiciona o splitter deixando ~460px pro carrinho (apos maximizar)
        Shown += (s, e) => AjustarSplitter();
        Resize += (s, e) => AjustarSplitter();
    }

    private void AjustarSplitter()
    {
        int w = _split.Width;
        // so mexe quando ha largura suficiente para os dois minimos + o splitter
        if (w < Panel1Min + Panel2Min + _split.SplitterWidth + 8) return;

        int dist = Math.Clamp(
            w - Panel2Desejado - _split.SplitterWidth,
            Panel1Min,
            w - Panel2Min - _split.SplitterWidth);
        try
        {
            // ORDEM importa: posiciona o splitter num valor valido ANTES de fixar os
            // minimos, senao o set de Panel2MinSize valida contra uma distancia invalida.
            _split.SplitterDistance = dist;
            if (!_splitConfigurado)
            {
                _split.Panel1MinSize = Panel1Min;
                _split.Panel2MinSize = Panel2Min;   // carrinho NUNCA colapsa
                _splitConfigurado = true;
            }
        }
        catch (Exception ex) { Log.Aviso("Ajuste do splitter: " + ex.Message); }
    }

    private MenuStrip CriarMenu()
    {
        var menu = new MenuStrip { Font = new Font("Segoe UI", 10F) };

        var mArquivo = new ToolStripMenuItem("&Arquivo");
        mArquivo.DropDownItems.Add("Abrir Caixa...", null, (s, e) => AbrirCaixaMenu());
        mArquivo.DropDownItems.Add(Item("Fechamento de Caixa", Keys.F9, (s, e) => AbrirFechamento()));
        mArquivo.DropDownItems.Add(new ToolStripSeparator());
        mArquivo.DropDownItems.Add("Sair", null, (s, e) => Close());

        var mConfig = new ToolStripMenuItem("&Configuracoes");
        mConfig.DropDownItems.Add("Gerenciar Produtos...", null, (s, e) => AbrirGerenciarProdutos());
        mConfig.DropDownItems.Add("Gerenciar Categorias...", null, (s, e) => AbrirGerenciarCategorias());
        mConfig.DropDownItems.Add(Item("Gerenciar Impressora", Keys.F12, (s, e) => AbrirConfigImpressora()));
        mConfig.DropDownItems.Add("Layout do Cupom...", null, (s, e) => AbrirLayoutCupom());
        mConfig.DropDownItems.Add("Customizar Atalhos...", null, (s, e) => AbrirCustomizarAtalhos());

        var mFerr = new ToolStripMenuItem("Ferramen&tas");
        mFerr.DropDownItems.Add(Item("Backup / Restauracao", Keys.F8, (s, e) => AbrirBackup()));
        mFerr.DropDownItems.Add(new ToolStripSeparator());
        mFerr.DropDownItems.Add("Sangria (retirada)...", null, (s, e) => AbrirMovimento(TipoMovimento.Sangria));
        mFerr.DropDownItems.Add("Suprimento (entrada)...", null, (s, e) => AbrirMovimento(TipoMovimento.Suprimento));

        var mAjuda = new ToolStripMenuItem("A&juda");
        mAjuda.DropDownItems.Add("Sobre...", null, (s, e) => Dialogos.Modal(this, () => new FormSobre()));

        menu.Items.AddRange(new ToolStripItem[] { mArquivo, mConfig, mFerr, mAjuda });
        MainMenuStrip = menu;
        return menu;
    }

    private static ToolStripMenuItem Item(string texto, Keys atalho, EventHandler onClick)
    {
        var it = new ToolStripMenuItem(texto, null, onClick) { ShortcutKeys = atalho };
        return it;
    }

    private StatusStrip CriarStatusStrip()
    {
        var st = new StatusStrip { SizingGrip = false, Font = new Font("Segoe UI", 10F) };
        var atalhos = new ToolStripStatusLabel("[1-9] Produto   [F2] Pagar   [Esc] Limpar   [F9] Fechamento   [F12] Impressora")
        { Spring = true, TextAlign = ContentAlignment.MiddleLeft };

        _stBd.Text = "BD: --";
        _stImpressora.Text = "Impressora: --";
        _stCaixa.Text = "Caixa: --";

        st.Items.Add(atalhos);
        st.Items.Add(new ToolStripStatusLabel("|"));
        st.Items.Add(_stCaixa);
        st.Items.Add(new ToolStripStatusLabel("|"));
        st.Items.Add(_stBd);
        st.Items.Add(new ToolStripStatusLabel("|"));
        st.Items.Add(_stImpressora);
        return st;
    }

    private void MontarPainelCarrinho(SplitterPanel painel)
    {
        painel.BackColor = Color.FromArgb(245, 245, 245);

        var cab = new Label
        {
            Text = "CARRINHO", Dock = DockStyle.Top, Height = 42,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            BackColor = Marca.Vermelho, ForeColor = Color.White
        };

        _grid.Name = "gridCarrinho";
        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.RowHeadersVisible = false;
        _grid.BorderStyle = BorderStyle.None;
        _grid.BackgroundColor = Color.White;
        _grid.AllowUserToResizeRows = false;
        _grid.AllowUserToResizeColumns = false;
        _grid.Font = new Font("Segoe UI", 12F);
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        // cabecalho legivel (sem cortar): altura fixa, negrito, fundo cinza claro
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _grid.ColumnHeadersHeight = 36;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(230, 230, 230);
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        _grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(2, 4, 2, 4);
        _grid.RowTemplate.Height = 30;
        _grid.Columns.Add("qtd", "Qtd");
        _grid.Columns.Add("desc", "Descricao");
        _grid.Columns.Add("unit", "V. Unit");
        _grid.Columns.Add("sub", "Subtotal");
        _grid.Columns["qtd"]!.FillWeight = 18;
        _grid.Columns["desc"]!.FillWeight = 44;
        _grid.Columns["unit"]!.FillWeight = 19;
        _grid.Columns["sub"]!.FillWeight = 19;
        _grid.Columns["qtd"]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        _grid.Columns["unit"]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _grid.Columns["sub"]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _grid.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Delete) RemoverSelecionado();
        };

        // TOTAL gigante
        _lblTotal.Name = "lblTotal";
        _lblTotal.Dock = DockStyle.Bottom;
        _lblTotal.Height = 120;
        _lblTotal.TextAlign = ContentAlignment.MiddleCenter;
        _lblTotal.Font = new Font("Arial", 36F, FontStyle.Bold);
        _lblTotal.ForeColor = Color.FromArgb(0, 110, 0);
        _lblTotal.BackColor = Color.White;

        // barra de acao final
        var barra = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 84, ColumnCount = 2, RowCount = 1 };
        barra.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        barra.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        var btnPagar = BotaoAcao("PAGAR [F2]", Color.FromArgb(0, 150, 0));
        btnPagar.Name = "btnPagar";
        btnPagar.Click += (s, e) => AbrirPagamento();
        var btnCancelar = BotaoAcao("CANCELAR [Esc]", Color.FromArgb(180, 60, 60));
        btnCancelar.Click += (s, e) => LimparCarrinho();
        barra.Controls.Add(btnPagar, 0, 0);
        barra.Controls.Add(btnCancelar, 1, 0);

        // a grid vai num host com margem lateral p/ nao encostar/cortar na borda direita
        var gridHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6, 4, 8, 4), BackColor = Color.White };
        gridHost.Controls.Add(_grid);

        // Dock: fill primeiro, depois bordas
        painel.Controls.Add(gridHost);
        painel.Controls.Add(barra);
        painel.Controls.Add(_lblTotal);
        painel.Controls.Add(cab);
    }

    private static Button BotaoAcao(string texto, Color cor) => new()
    {
        Text = texto, Dock = DockStyle.Fill, Margin = new Padding(4),
        BackColor = cor, ForeColor = Color.White, FlatStyle = FlatStyle.Flat,
        Font = new Font("Segoe UI", 15F, FontStyle.Bold), TabStop = false
    };

    // ======================= CATALOGO (dinamico) =======================

    /// <summary>
    /// Varre o banco e RECRIA as abas/botoes de produtos ativos, respeitando a ORDEM
    /// das categorias (tabela categorias). Categorias inativas nao viram aba.
    /// </summary>
    public void RecarregarProdutos()
    {
        _produtos = _servico.Produtos();   // apenas produtos ativos
        _abas.TabPages.Clear();

        var ativas = _servico.CategoriasAtivas().Select(c => c.Nome).ToList();
        var inativas = _servico.Categorias().Where(c => !c.Ativo).Select(c => c.Nome).ToHashSet();
        var comProduto = _produtos.Select(p => p.Categoria).Distinct().ToList();

        // ordem final: categorias ativas (na ordem) que tem produto,
        // depois categorias novas/desconhecidas (nunca as explicitamente inativas)
        var ordem = new List<string>();
        foreach (var c in ativas) if (comProduto.Contains(c)) ordem.Add(c);
        foreach (var c in comProduto) if (!ordem.Contains(c) && !inativas.Contains(c)) ordem.Add(c);

        // 1a aba "Todos": TODOS os produtos agrupados por categoria numa tela so (com scroll).
        if (_produtos.Count > 0)
            _abas.TabPages.Add(CriarAbaTodos(ordem));

        // abas por categoria
        foreach (var cat in ordem)
        {
            var page = new TabPage(cat);
            var fluxo = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(10),
                BackColor = Color.White
            };
            foreach (var p in _produtos.Where(x => x.Categoria == cat))
                fluxo.Controls.Add(CriarBotaoProduto(p));
            page.Controls.Add(fluxo);
            _abas.TabPages.Add(page);
        }
    }

    /// <summary>
    /// Aba "Todos": todas as categorias empilhadas (cabecalho + botoes), rolagem vertical.
    /// Usa Dock.Top (largura total automatica) para nao depender de largura fixa.
    /// </summary>
    private TabPage CriarAbaTodos(List<string> ordem)
    {
        var page = new TabPage("Todos");
        var host = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White, Padding = new Padding(8) };

        // Dock.Top empilha em ordem inversa de adicao (ultimo adicionado = mais acima),
        // entao percorremos as categorias de tras pra frente, adicionando fluxo e depois cabecalho.
        foreach (var cat in Enumerable.Reverse(ordem))
        {
            var itens = _produtos.Where(x => x.Categoria == cat).ToList();
            if (itens.Count == 0) continue;

            var fluxo = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, AutoSize = true, WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(4, 0, 4, 10)
            };
            foreach (var p in itens)
                fluxo.Controls.Add(CriarBotaoProduto(p, "btnProdutoTodos_"));

            var cab = new Label
            {
                Dock = DockStyle.Top, Height = 32, Text = "  " + cat.ToUpperInvariant(),
                TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                BackColor = Color.FromArgb(235, 235, 235), ForeColor = Color.FromArgb(60, 60, 60)
            };

            host.Controls.Add(fluxo);
            host.Controls.Add(cab);
        }

        page.Controls.Add(host);
        return page;
    }

    private Button CriarBotaoProduto(Produto p, string prefixoNome = "btnProduto_")
    {
        var btn = new Button
        {
            Text = $"{p.Nome}\n{CupomFormatter.Moeda(p.PrecoCentavos)}",
            Width = 168, Height = 96, Margin = new Padding(8),
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            BackColor = CorCategoria(p.Categoria), FlatStyle = FlatStyle.Flat,
            TextAlign = ContentAlignment.MiddleCenter, TabStop = false, Tag = p.Id,
            Name = prefixoNome + p.Id        // AutomationId (btnProduto_ nas abas; btnProdutoTodos_ na aba Todos)
        };
        btn.FlatAppearance.BorderColor = Color.FromArgb(210, 210, 210);
        btn.Click += (s, e) => AdicionarProduto(p);

        // atalho no canto superior esquerdo (badge)
        if (p.Atalho is >= 1 and <= 9)
        {
            var badge = new Label
            {
                Text = p.Atalho.ToString(), AutoSize = false, Size = new Size(24, 20),
                Location = new Point(4, 4), TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Marca.Vermelho, ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };
            badge.Click += (s, e) => AdicionarProduto(p);
            btn.Controls.Add(badge);
        }
        return btn;
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

    // ======================= CARRINHO =======================

    private void AdicionarProduto(Produto p)
    {
        _servico.Carrinho.Adicionar(p);
        AtualizarCarrinho();
    }

    private void AtualizarCarrinho()
    {
        _grid.Rows.Clear();
        foreach (var i in _servico.Carrinho.Itens)
        {
            int idx = _grid.Rows.Add(
                i.Quantidade, i.Nome,
                CupomFormatter.Moeda(i.PrecoUnitarioCentavos),
                CupomFormatter.Moeda(i.SubtotalCentavos));
            _grid.Rows[idx].Tag = i.ProdutoId;
        }
        _lblTotal.Text = CupomFormatter.Moeda(_servico.Carrinho.TotalCentavos);
    }

    private void RemoverSelecionado()
    {
        if (_grid.CurrentRow?.Tag is string id)
        {
            _servico.Carrinho.Remover(id);
            AtualizarCarrinho();
        }
    }

    private void LimparCarrinho()
    {
        if (_servico.Carrinho.Itens.Count == 0) return;
        _servico.Carrinho.Limpar();
        AtualizarCarrinho();
    }

    // ======================= TECLADO =======================

    private void FormVendas_KeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case >= Keys.D1 and <= Keys.D9:
                AdicionarPorAtalho(e.KeyCode - Keys.D0); e.Handled = true; break;
            case >= Keys.NumPad1 and <= Keys.NumPad9:
                AdicionarPorAtalho(e.KeyCode - Keys.NumPad0); e.Handled = true; break;
            case Keys.F2:
            case Keys.Enter:
                AbrirPagamento(); e.Handled = true; break;
            case Keys.Escape:
                LimparCarrinho(); e.Handled = true; break;
        }
    }

    private void AdicionarPorAtalho(int numero)
    {
        var p = _produtos.FirstOrDefault(x => x.Atalho == numero);
        if (p is not null) AdicionarProduto(p);
    }

    // ======================= TELAS =======================

    private void GarantirCaixaAberto()
    {
        if (_servico.CaixaAberto) return;
        // Abre direto o modal de abertura (informar troco inicial) - sem caixa, nao vende.
        Dialogos.Modal(this, () => new FormAberturaCaixa(_servico));
        AtualizarStatus();
    }

    private void AbrirCaixaMenu()
    {
        if (_servico.CaixaAberto)
        {
            MessageBox.Show("Ja existe um caixa aberto. Feche-o antes de abrir outro.",
                "Abrir Caixa", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!Dialogos.LiberarAdmin(this, _servico)) return;
        Dialogos.Modal(this, () => new FormAberturaCaixa(_servico));
        AtualizarStatus();
    }

    private void AbrirPagamento()
    {
        if (_servico.Carrinho.Itens.Count == 0) return;
        if (!_servico.CaixaAberto)
        {
            MessageBox.Show("O caixa esta fechado. Abra o caixa (menu Arquivo > Abrir Caixa)\n" +
                "antes de registrar vendas.", "Caixa fechado",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (Dialogos.Modal(this, () => new FormPagamento(_servico)) == DialogResult.OK)
        {
            AtualizarCarrinho();
            AtualizarStatus();
        }
    }

    private void AbrirFechamento()
    {
        if (!Dialogos.LiberarAdmin(this, _servico)) return;
        Dialogos.Modal(this, () => new FormFechamento(_servico));
        AtualizarStatus();
    }

    private void AbrirGerenciarProdutos()
    {
        if (!Dialogos.LiberarAdmin(this, _servico)) return;
        Dialogos.Modal(this, () => new FormGerenciarProdutos(_servico));
        RecarregarProdutos();   // refresh dinamico: botoes/precos atualizados na hora
    }

    private void AbrirGerenciarCategorias()
    {
        if (!Dialogos.LiberarAdmin(this, _servico)) return;
        Dialogos.Modal(this, () => new FormCategorias(_servico));
        RecarregarProdutos();   // abas reordenadas/ocultadas na hora
    }

    private void AbrirConfigImpressora()
    {
        if (!Dialogos.LiberarAdmin(this, _servico)) return;
        Dialogos.Modal(this, () => new FormPrinterConfig(_servico));
        AtualizarStatus();
    }

    private void AbrirLayoutCupom()
    {
        if (!Dialogos.LiberarAdmin(this, _servico)) return;
        Dialogos.Modal(this, () => new FormLayoutCupom(_servico));
    }

    private void AbrirCustomizarAtalhos()
    {
        if (!Dialogos.LiberarAdmin(this, _servico)) return;
        Dialogos.Modal(this, () => new FormCustomizarAtalhos(_servico));
        RecarregarProdutos();   // badges de atalho atualizadas
    }

    private void AbrirBackup()
    {
        if (!Dialogos.LiberarAdmin(this, _servico)) return;
        Dialogos.Modal(this, () => new FormBackup(_servico));
    }

    private void AbrirMovimento(TipoMovimento tipo)
    {
        if (!_servico.CaixaAberto)
        {
            MessageBox.Show("Abra o caixa antes de registrar sangria/suprimento.",
                "Caixa fechado", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!Dialogos.LiberarAdmin(this, _servico)) return;
        Dialogos.Modal(this, () => new FormMovimento(_servico, tipo));
    }

    // ======================= STATUS =======================

    private void AtualizarStatus()
    {
        // BD
        try
        {
            var wal = _servico.Repo.ModoJournal();
            _stBd.Text = "BD: Conectado (" + wal.ToUpperInvariant() + ")";
            _stBd.ForeColor = Color.FromArgb(0, 110, 0);
        }
        catch
        {
            _stBd.Text = "BD: ERRO";
            _stBd.ForeColor = Color.FromArgb(180, 0, 0);
        }

        // impressora
        var imp = _servico.ImpressoraPadrao;
        if (string.IsNullOrWhiteSpace(imp))
        {
            _stImpressora.Text = "Impressora: nao configurada (F12)";
            _stImpressora.ForeColor = Color.FromArgb(180, 100, 0);
        }
        else
        {
            _stImpressora.Text = "Impressora: " + imp;
            _stImpressora.ForeColor = Color.FromArgb(0, 90, 150);
        }

        // caixa
        if (_servico.CaixaAberto)
        {
            _stCaixa.Text = $"Caixa: ABERTO (#{_servico.TurnoAtual!.Id})";
            _stCaixa.ForeColor = Color.FromArgb(0, 110, 0);
        }
        else
        {
            _stCaixa.Text = "Caixa: FECHADO";
            _stCaixa.ForeColor = Color.FromArgb(180, 0, 0);
        }
    }
}
