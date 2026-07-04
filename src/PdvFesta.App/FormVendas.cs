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

    // --- navegacao 100% por teclado: LETRA da categoria -> NUMERO do item -> Enter ---
    // Toda categoria ganha uma letra (a inicial; se colidir, a proxima livre). Cada aba
    // guarda seus produtos em ordem, para "C 2" destacar o 2o item de Comidas. Enter adiciona.
    private readonly Dictionary<char, string> _letraCategoria = new();     // 'C' -> "Comidas"
    private readonly Dictionary<string, List<Produto>> _itensPorCategoria = new();
    private readonly Dictionary<string, TabPage> _abaPorCategoria = new();
    private string? _catSelecionada;      // categoria em modo de selecao (null = fora do modo)
    private int _itemDestacado = -1;      // indice do item destacado dentro da categoria
    private ToolStripStatusLabel _stModo = new();

    public FormVendas(Servico servico)
    {
        _servico = servico;
        Name = "FormVendas";
        Text = "PDV Festa Junina - Terminal Caixa 01";
        Icon = Marca.Icone();
        KeyPreview = true;
        BackColor = Color.White;
        Font = new Font("Segoe UI", 11F);

        // MODO DEMO (gravacao do video): abre MAXIMIZADO forcado na TELA PRIMARIA. Assim a
        // captura por titulo pega a janela cheia numa tela so — nunca esticada nas 2 telas.
        // (Posiciona na primaria ANTES de maximizar; senao o Windows maximiza onde abriu.)
        if (Environment.GetEnvironmentVariable("PDV_DEMO") == "1")
        {
            StartPosition = FormStartPosition.Manual;
            var primaria = Screen.PrimaryScreen!.WorkingArea;
            Location = new Point(primaria.X, primaria.Y);
            Size = new Size(primaria.Width, primaria.Height);
            Shown += (_, _) =>
            {
                Location = new Point(primaria.X, primaria.Y);   // garante ancora na primaria
                WindowState = FormWindowState.Maximized;        // maximiza NELA
            };
        }
        else
        {
            WindowState = FormWindowState.Maximized;
        }
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
        mArquivo.DropDownItems.Add("Trocar Operador...", null, (s, e) => AbrirTrocaOperador());
        mArquivo.DropDownItems.Add(Item("Painel em Tempo Real", Keys.F4, (s, e) => AbrirDashboard()));
        mArquivo.DropDownItems.Add(Item("Histórico de Vendas", Keys.F3, (s, e) => AbrirHistorico()));
        mArquivo.DropDownItems.Add(Item("Fechamento de Caixa", Keys.F9, (s, e) => AbrirFechamento()));
        mArquivo.DropDownItems.Add("Exportar CSV do turno...", null, (s, e) => Dialogos.ExportarCsvComDialogo(this, _servico));
        mArquivo.DropDownItems.Add(new ToolStripSeparator());
        mArquivo.DropDownItems.Add("Sair", null, (s, e) => Close());

        var mConfig = new ToolStripMenuItem("&Configurações");
        mConfig.DropDownItems.Add("Gerenciar Produtos...", null, (s, e) => AbrirGerenciarProdutos());
        mConfig.DropDownItems.Add("Gerenciar Categorias...", null, (s, e) => AbrirGerenciarCategorias());
        mConfig.DropDownItems.Add("Gerenciar Promoções / Combos...", null, (s, e) => AbrirGerenciarPromocoes());
        mConfig.DropDownItems.Add(Item("Gerenciar Impressora", Keys.F12, (s, e) => AbrirConfigImpressora()));
        mConfig.DropDownItems.Add("Layout do Cupom...", null, (s, e) => AbrirLayoutCupom());
        mConfig.DropDownItems.Add(new ToolStripSeparator());
        mConfig.DropDownItems.Add("Permissões e Senha...", null, (s, e) => AbrirPermissoes());

        var mFerr = new ToolStripMenuItem("Ferramen&tas");
        mFerr.DropDownItems.Add(Item("Backup / Restauração", Keys.F8, (s, e) => AbrirBackup()));
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
        var atalhos = new ToolStripStatusLabel("[Letra]+[Nº]+[Enter] Adiciona   |   [Del] Remove item   |   [F2] Pagar   [Esc] Cancela venda   [F9] Fechamento   [F12] Impressora")
        { Spring = true, TextAlign = ContentAlignment.MiddleLeft };

        _stBd.Text = "BD: --";
        _stImpressora.Text = "Impressora: --";
        _stCaixa.Text = "Caixa: --";
        _stModo.Text = "";
        _stModo.ForeColor = Marca.Vermelho;
        _stModo.Font = new Font("Segoe UI", 10F, FontStyle.Bold);

        st.Items.Add(atalhos);
        st.Items.Add(new ToolStripStatusLabel("|"));
        st.Items.Add(_stModo);
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
            if (e.KeyCode is Keys.Delete or Keys.Back) { RemoverSelecionado(); e.Handled = true; }
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

        // botao visivel de correcao de pedido (alem de Delete/Backspace na grid)
        var btnRemover = new Button
        {
            Text = "🗑  Remover Item Selecionado (Del)", Dock = DockStyle.Top, Height = 40,
            BackColor = Color.FromArgb(200, 120, 40), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 11F, FontStyle.Bold), TabStop = false
        };
        btnRemover.Click += (s, e) => RemoverSelecionado();

        // ALEM do botao: clicar com o BOTAO DIREITO na linha, ou dar DUPLO-CLIQUE, remove o
        // item — a remocao deixa de ficar "amarrada" so ao botao Cancelar/Del.
        var menuGrid = new ContextMenuStrip();
        var miRemover = new ToolStripMenuItem("Remover este item")
        {
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ShortcutKeyDisplayString = "Del"   // mostra o atalho ao lado (informativo)
        };
        miRemover.Click += (s, e) => RemoverSelecionado();
        menuGrid.Items.Add(miRemover);
        menuGrid.Items.Add("Cancelar venda (limpar tudo)", null, (s, e) => LimparCarrinho());
        _grid.ContextMenuStrip = menuGrid;
        // botao direito seleciona a linha sob o cursor antes de abrir o menu (age no item certo)
        _grid.MouseDown += (s, e) =>
        {
            if (e.Button != MouseButtons.Right) return;
            var hit = _grid.HitTest(e.X, e.Y);
            if (hit.RowIndex >= 0) _grid.CurrentCell = _grid.Rows[hit.RowIndex].Cells[0];
        };
        _grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) RemoverSelecionado(); };

        // Dock: fill primeiro, depois bordas (cab fica no topo; btnRemover logo abaixo)
        painel.Controls.Add(gridHost);
        painel.Controls.Add(barra);
        painel.Controls.Add(_lblTotal);
        painel.Controls.Add(btnRemover);
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

        // (re)constroi os mapas de navegacao por teclado: letra, itens e aba de cada categoria.
        ConstruirMapaTeclado(ordem);

        // 1a aba "Todos": TODOS os produtos agrupados por categoria numa tela so (com scroll).
        if (_produtos.Count > 0)
            _abas.TabPages.Add(CriarAbaTodos(ordem));

        // abas por categoria
        foreach (var cat in ordem)
        {
            var letra = _letraCategoria.FirstOrDefault(kv => kv.Value == cat).Key;
            // titulo da aba mostra a letra do atalho: "Comidas (C)"
            var page = new TabPage(letra != '\0' ? $"{cat} ({letra})" : cat) { Tag = cat };
            var fluxo = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(10),
                BackColor = Color.White
            };
            foreach (var p in _itensPorCategoria[cat])
                fluxo.Controls.Add(CriarBotaoProduto(p));
            page.Controls.Add(fluxo);
            _abaPorCategoria[cat] = page;
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

    /// <summary>
    /// Monta os mapas da navegacao por teclado: para cada categoria (na ordem das abas)
    /// atribui uma LETRA (a inicial livre) e guarda a lista ordenada de itens. Assim
    /// "C 2 Enter" resolve deterministicamente o 2o produto de Comidas.
    /// </summary>
    private void ConstruirMapaTeclado(List<string> ordem)
    {
        _letraCategoria.Clear();
        _itensPorCategoria.Clear();
        _abaPorCategoria.Clear();

        foreach (var cat in ordem)
        {
            _itensPorCategoria[cat] = _produtos.Where(x => x.Categoria == cat).ToList();

            // letra = 1a letra livre do nome; se todas ocupadas, cai pra qualquer A-Z livre.
            char letra = '\0';
            foreach (var ch in cat.ToUpperInvariant())
                if (char.IsLetter(ch) && !_letraCategoria.ContainsKey(ch)) { letra = ch; break; }
            if (letra == '\0')
                for (char c = 'A'; c <= 'Z'; c++)
                    if (!_letraCategoria.ContainsKey(c)) { letra = c; break; }
            if (letra != '\0') _letraCategoria[letra] = cat;
        }
    }

    /// <summary>Entra no modo de selecao de uma categoria: troca a aba e destaca o 1o item.</summary>
    private void EntrarModoCategoria(string cat)
    {
        if (!_abaPorCategoria.TryGetValue(cat, out var aba)) return;
        _abas.SelectedTab = aba;
        _catSelecionada = cat;
        _itemDestacado = _itensPorCategoria[cat].Count > 0 ? 0 : -1;
        AtualizarDestaque();
    }

    /// <summary>Destaca o N-esimo item (0-based) da categoria ativa. Fora do range = ignora.</summary>
    private void DestacarItem(int indice)
    {
        if (_catSelecionada is null) return;
        var itens = _itensPorCategoria[_catSelecionada];
        if (indice < 0 || indice >= itens.Count) return;
        _itemDestacado = indice;
        AtualizarDestaque();
    }

    /// <summary>Adiciona o item destacado ao carrinho e sai do modo de selecao.</summary>
    private bool ConfirmarItemDestacado()
    {
        if (_catSelecionada is null || _itemDestacado < 0) return false;
        var itens = _itensPorCategoria[_catSelecionada];
        if (_itemDestacado >= itens.Count) return false;
        AdicionarProduto(itens[_itemDestacado]);
        SairModoSelecao();
        return true;
    }

    private void SairModoSelecao()
    {
        _catSelecionada = null;
        _itemDestacado = -1;
        AtualizarDestaque();
    }

    /// <summary>Pinta a borda do item destacado e atualiza a dica de modo no rodape.</summary>
    private void AtualizarDestaque()
    {
        // limpa qualquer destaque anterior em todas as abas
        foreach (var aba in _abaPorCategoria.Values)
            foreach (var btn in ProdutosDaAba(aba))
                btn.FlatAppearance.BorderColor = Color.FromArgb(210, 210, 210);

        if (_catSelecionada is null)
        {
            _stModo.Text = "";
            return;
        }

        var letra = _letraCategoria.FirstOrDefault(kv => kv.Value == _catSelecionada).Key;
        if (_itemDestacado >= 0 && _abaPorCategoria.TryGetValue(_catSelecionada, out var abaSel))
        {
            var botoes = ProdutosDaAba(abaSel);
            if (_itemDestacado < botoes.Count)
            {
                var alvo = botoes[_itemDestacado];
                alvo.FlatAppearance.BorderColor = Marca.Vermelho;
                var nome = _itensPorCategoria[_catSelecionada][_itemDestacado].Nome;
                _stModo.Text = $"► {_catSelecionada} ({letra}) {_itemDestacado + 1}: {nome} — Enter adiciona";
                return;
            }
        }
        _stModo.Text = $"► {_catSelecionada} ({letra}) — digite o Nº do item";
    }

    /// <summary>Botoes de produto de uma aba de categoria, na ordem em que aparecem.</summary>
    private static List<Button> ProdutosDaAba(TabPage aba)
    {
        var fluxo = aba.Controls.OfType<FlowLayoutPanel>().FirstOrDefault();
        if (fluxo is null) return new();
        return fluxo.Controls.OfType<Button>().ToList();
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

        // badge com o NUMERO do item dentro da sua categoria (1..N). TODO item ganha um,
        // entao a sequencia "Letra da categoria + Numero" opera qualquer produto pelo teclado.
        int idx = _itensPorCategoria.TryGetValue(p.Categoria, out var lst) ? lst.IndexOf(p) : -1;
        if (idx >= 0)
        {
            var letra = _letraCategoria.FirstOrDefault(kv => kv.Value == p.Categoria).Key;
            var badge = new Label
            {
                // "C2" = categoria Comidas, item 2. So o numero se nao houver letra.
                Text = letra != '\0' ? $"{letra}{idx + 1}" : (idx + 1).ToString(),
                AutoSize = false, Size = new Size(30, 20),
                Location = new Point(4, 4), TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Marca.Vermelho, ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
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
        _servico.AplicarPromocoes();       // auto-detecta combos/promocoes
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
        // linhas de DESCONTO de combo/promocao (verde, italico) - nao substitui os itens
        foreach (var d in _servico.Carrinho.Descontos)
        {
            int idx = _grid.Rows.Add("", "* " + d.Descricao, "", "-" + CupomFormatter.Moeda(d.ValorCentavos));
            _grid.Rows[idx].DefaultCellStyle.ForeColor = Color.FromArgb(0, 130, 0);
            _grid.Rows[idx].DefaultCellStyle.Font = new Font(_grid.Font, FontStyle.Bold | FontStyle.Italic);
            _grid.Rows[idx].Tag = null;   // linha de desconto nao e removivel individualmente
        }
        _lblTotal.Text = CupomFormatter.Moeda(_servico.Carrinho.TotalCentavos);
    }

    private void RemoverSelecionado()
    {
        if (_servico.Carrinho.Itens.Count == 0) return;

        // linha selecionada (se for um item de verdade, tem o ProdutoId no Tag).
        var id = _grid.CurrentRow?.Tag as string;

        // A prova de erro: se nada de valido esta selecionado (ex: foco fora do grid, ou
        // linha de desconto), remove o ULTIMO item adicionado — o caso tipico de correcao.
        if (id is null)
            id = _servico.Carrinho.Itens[^1].ProdutoId;

        _servico.Carrinho.Remover(id);
        _servico.AplicarPromocoes();   // reavalia: pode sumir a linha de desconto
        AtualizarCarrinho();
    }

    private void LimparCarrinho()
    {
        int n = _servico.Carrinho.Itens.Count;
        if (n == 0) return;

        // CONFIRMACAO: cancelar a venda inteira apaga o carrinho — pede um OK para nao perder
        // o pedido por um ESC/clique acidental. (Remover 1 item nao passa por aqui.)
        var r = MessageBox.Show(
            $"Cancelar a venda e limpar o carrinho ({n} item(ns))?",
            "Cancelar venda", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);   // default = Cancelar (nao apaga sem querer)
        if (r != DialogResult.OK) return;

        _servico.Carrinho.Limpar();
        AtualizarCarrinho();
    }

    // ======================= TECLADO =======================

    private void FormVendas_KeyDown(object? sender, KeyEventArgs e)
    {
        // ignora combinacoes com Ctrl/Alt (sao dos menus) para nao roubar atalhos do sistema.
        if (e.Control || e.Alt) return;

        // 1) LETRA de categoria -> entra no modo de selecao daquela aba (destaca 1o item).
        if (e.KeyCode is >= Keys.A and <= Keys.Z)
        {
            char letra = (char)('A' + (e.KeyCode - Keys.A));
            if (_letraCategoria.TryGetValue(letra, out var cat))
            {
                EntrarModoCategoria(cat);
                e.Handled = true; e.SuppressKeyPress = true;
                return;
            }
            return;   // letra sem categoria: deixa passar (nao atrapalha)
        }

        switch (e.KeyCode)
        {
            // 2) NUMERO: SO tem efeito DENTRO do modo de selecao (destaca o N-esimo item da
            //    categoria ativa). Fora do modo, numero e ignorado — assim NAO existe o
            //    atalho global 1-9 competindo com a sequencia, e nunca adiciona item errado.
            case >= Keys.D1 and <= Keys.D9:
                if (_catSelecionada is not null) { DestacarItem(e.KeyCode - Keys.D0 - 1); e.Handled = true; }
                break;
            case >= Keys.NumPad1 and <= Keys.NumPad9:
                if (_catSelecionada is not null) { DestacarItem(e.KeyCode - Keys.NumPad0 - 1); e.Handled = true; }
                break;

            // 3) ENTER: no modo, adiciona o item destacado; fora do modo, vai pagar.
            case Keys.Enter:
                if (!ConfirmarItemDestacado()) AbrirPagamento();
                e.Handled = true; break;
            case Keys.F2:
                AbrirPagamento(); e.Handled = true; break;

            // 4) ESC: sai do modo de selecao; se ja estava fora, limpa o carrinho.
            case Keys.Escape:
                if (_catSelecionada is not null) SairModoSelecao();
                else LimparCarrinho();
                e.Handled = true; break;

            // 5) DELETE/BACKSPACE: remove item do carrinho de QUALQUER lugar (correcao de
            //    pedido a prova de erro — nao exige que o foco esteja no grid).
            case Keys.Delete:
            case Keys.Back:
                RemoverSelecionado(); e.Handled = true; break;
        }
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
        if (!Dialogos.LiberarAcao(this, _servico, AcaoProtegida.AbrirCaixa)) return;
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

    private void AbrirHistorico()
    {
        // Ver o historico e livre; o ESTORNO dentro dele e que pede senha de admin.
        if (!_servico.CaixaAberto)
        {
            MessageBox.Show("Abra o caixa para ver o histórico de vendas do turno.",
                "Histórico", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        Dialogos.Modal(this, () => new FormHistoricoVendas(_servico));
        AtualizarStatus();
    }

    // Painel em tempo real: SO visualizacao (nao mexe em nada) -> sem senha de admin.
    private void AbrirDashboard()
    {
        if (!_servico.CaixaAberto)
        {
            MessageBox.Show("Abra o caixa para acompanhar as vendas em tempo real.",
                "Painel", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        Dialogos.Modal(this, () => new FormDashboard(_servico));
    }

    private void AbrirTrocaOperador()
    {
        if (!_servico.CaixaAberto)
        {
            MessageBox.Show("Abra o caixa antes de trocar de operador.",
                "Troca de Operador", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        // as permissoes decidem se pede senha (padrao: pede — envolve dinheiro/turno).
        if (!Dialogos.LiberarAcao(this, _servico, AcaoProtegida.TrocarOperador)) return;
        Dialogos.Modal(this, () => new FormTrocaOperador(_servico));
        AtualizarStatus();
    }

    private void AbrirFechamento()
    {
        if (!Dialogos.LiberarAcao(this, _servico, AcaoProtegida.FecharCaixa)) return;
        Dialogos.Modal(this, () => new FormFechamento(_servico));
        AtualizarStatus();
    }

    private void AbrirGerenciarProdutos()
    {
        if (!Dialogos.LiberarAcao(this, _servico, AcaoProtegida.GerenciarProdutos)) return;
        Dialogos.Modal(this, () => new FormGerenciarProdutos(_servico));
        RecarregarProdutos();   // refresh dinamico: botoes/precos atualizados na hora
    }

    private void AbrirGerenciarCategorias()
    {
        if (!Dialogos.LiberarAcao(this, _servico, AcaoProtegida.GerenciarCategorias)) return;
        Dialogos.Modal(this, () => new FormCategorias(_servico));
        RecarregarProdutos();   // abas reordenadas/ocultadas na hora
    }

    private void AbrirGerenciarPromocoes()
    {
        if (!Dialogos.LiberarAcao(this, _servico, AcaoProtegida.GerenciarPromocoes)) return;
        Dialogos.Modal(this, () => new FormPromocoes(_servico));
        _servico.RecarregarPromocoes();   // novas regras valem no proximo item
    }

    private void AbrirConfigImpressora()
    {
        if (!Dialogos.LiberarAcao(this, _servico, AcaoProtegida.ConfigImpressora)) return;
        Dialogos.Modal(this, () => new FormPrinterConfig(_servico));
        AtualizarStatus();
    }

    private void AbrirLayoutCupom()
    {
        if (!Dialogos.LiberarAcao(this, _servico, AcaoProtegida.LayoutCupom)) return;
        Dialogos.Modal(this, () => new FormLayoutCupom(_servico));
    }

    private void AbrirBackup()
    {
        if (!Dialogos.LiberarAcao(this, _servico, AcaoProtegida.Backup)) return;
        Dialogos.Modal(this, () => new FormBackup(_servico));
    }

    private void AbrirPermissoes()
    {
        // a tela de permissoes SEMPRE exige senha (nao pode ser desligada).
        if (!Dialogos.LiberarAcao(this, _servico, AcaoProtegida.Permissoes)) return;
        Dialogos.Modal(this, () => new FormPermissoes(_servico));
    }

    private void AbrirMovimento(TipoMovimento tipo)
    {
        if (!_servico.CaixaAberto)
        {
            MessageBox.Show("Abra o caixa antes de registrar sangria/suprimento.",
                "Caixa fechado", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!Dialogos.LiberarAcao(this, _servico, AcaoProtegida.SangriaSuprimento)) return;
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
