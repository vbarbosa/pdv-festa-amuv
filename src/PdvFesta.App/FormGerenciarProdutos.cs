using System.Globalization;
using System.Runtime.Versioning;
using System.Text;
using PdvFesta.Core;

namespace PdvFesta.App;

/// <summary>
/// Gestao completa do catalogo (CRUD). Permite adicionar itens, mudar preco no meio
/// do evento e OCULTAR produtos que acabaram (soft delete: ativo=0, nunca apaga).
/// Ao fechar, o MainForm recarrega os botoes automaticamente.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FormGerenciarProdutos : Form
{
    private readonly Servico _servico;
    private readonly DataGridView _grid = new();
    private readonly TextBox _txtNome = new();
    private readonly TextBox _txtPreco = new();
    private readonly ComboBox _cmbCategoria = new();
    private readonly CheckBox _chkAtivo = new();
    private readonly Label _lblStatus = new();

    private string? _editandoId;   // null = criando um produto novo

    public FormGerenciarProdutos(Servico servico)
    {
        _servico = servico;
        Text = "Gerenciar Produtos";
        Name = "FormGerenciarProdutos";
        Icon = Marca.Icone();
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(760, 520);
        ClientSize = new Size(860, 560);
        Font = new Font("Segoe UI", 11F);

        MontarLayout();
        CarregarGrid();
        NovoProduto();
    }

    private void MontarLayout()
    {
        // ---- painel direito: formulario de cadastro ----
        var painel = new TableLayoutPanel
        {
            Dock = DockStyle.Right, Width = 330, ColumnCount = 2, Padding = new Padding(12),
            BackColor = Color.FromArgb(245, 245, 245)
        };
        painel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        painel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var cab = new Label
        {
            Text = "CADASTRO", Dock = DockStyle.Fill, Height = 36,
            Font = new Font("Segoe UI", 13F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft
        };
        painel.Controls.Add(cab, 0, 0); painel.SetColumnSpan(cab, 2);

        _txtNome.Name = "txtNomeProduto"; _txtNome.Dock = DockStyle.Fill;
        _txtNome.Font = new Font("Segoe UI", 12F);
        AddCampo(painel, 1, "Nome:", _txtNome);

        _txtPreco.Name = "txtPrecoProduto"; _txtPreco.Dock = DockStyle.Fill;
        _txtPreco.Font = new Font("Segoe UI", 12F); _txtPreco.TextAlign = HorizontalAlignment.Right;
        _txtPreco.Text = "0,00";
        AddCampo(painel, 2, "Preço R$:", _txtPreco);

        _cmbCategoria.Dock = DockStyle.Fill; _cmbCategoria.Font = new Font("Segoe UI", 12F);
        _cmbCategoria.DropDownStyle = ComboBoxStyle.DropDown;
        AddCampo(painel, 3, "Categoria:", _cmbCategoria);

        // (O atalho do teclado NAO e mais cadastrado: e derivado da POSICAO do item na
        //  categoria — ver navegacao Letra+Numero. Por isso nao ha campo de atalho aqui.)

        _chkAtivo.Text = "Produto ativo (aparece no caixa)"; _chkAtivo.Dock = DockStyle.Fill;
        _chkAtivo.Checked = true;
        painel.Controls.Add(new Label { Text = "", Width = 1 }, 0, 4);
        painel.Controls.Add(_chkAtivo, 1, 4);

        var btnNovo = Botao("Novo", Color.FromArgb(70, 70, 90), (s, e) => NovoProduto());
        btnNovo.Name = "btnNovoProduto";
        var btnSalvar = Botao("Salvar / Atualizar", Color.FromArgb(0, 130, 0), (s, e) => Salvar());
        btnSalvar.Name = "btnSalvarProduto";
        painel.Controls.Add(btnNovo, 0, 5);
        painel.Controls.Add(btnSalvar, 1, 5);

        var btnInativar = Botao("Inativar (ocultar do caixa)", Color.FromArgb(180, 80, 0), (s, e) => Inativar());
        btnInativar.Name = "btnInativarProduto";
        painel.Controls.Add(btnInativar, 0, 6); painel.SetColumnSpan(btnInativar, 2);

        var btnExcluir = Botao("Excluir permanentemente", Color.FromArgb(160, 0, 0), (s, e) => Excluir());
        btnExcluir.Name = "btnExcluirProduto";
        painel.Controls.Add(btnExcluir, 0, 7); painel.SetColumnSpan(btnExcluir, 2);

        _lblStatus.Dock = DockStyle.Fill; _lblStatus.Height = 60;
        _lblStatus.ForeColor = Color.FromArgb(60, 60, 60);
        painel.Controls.Add(_lblStatus, 0, 8); painel.SetColumnSpan(_lblStatus, 2);

        // ---- grid a esquerda (catalogo completo, inclui inativos) ----
        _grid.Name = "gridProdutos";
        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.RowHeadersVisible = false;
        _grid.BorderStyle = BorderStyle.None;
        _grid.BackgroundColor = Color.White;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        EstiloGrid.Padronizar(_grid);   // cabecalho legivel (sem cortar texto)
        _grid.Columns.Add("id", "Id");
        _grid.Columns.Add("nome", "Nome");
        _grid.Columns.Add("preco", "Preço");
        _grid.Columns.Add("cat", "Categoria");
        _grid.Columns.Add("ativo", "Ativo");
        _grid.Columns["id"]!.Visible = false;
        _grid.Columns["preco"]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _grid.SelectionChanged += (s, e) => SelecionarLinha();

        // ---- barra inferior: versionamento do cardapio (exportar/importar .json) ----
        var barraCardapio = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, Height = 52, Padding = new Padding(8), BackColor = Color.FromArgb(238, 238, 238)
        };
        var btnExportar = Botao("Exportar cardapio (.json)", Color.FromArgb(0, 120, 200), (s, e) => ExportarCardapio());
        btnExportar.Name = "btnExportarCardapio"; btnExportar.Width = 210;
        var btnImportar = Botao("Importar cardapio (.json)", Color.FromArgb(180, 100, 0), (s, e) => ImportarCardapio());
        btnImportar.Name = "btnImportarCardapio"; btnImportar.Width = 210;
        barraCardapio.Controls.Add(btnExportar);
        barraCardapio.Controls.Add(btnImportar);

        Controls.Add(_grid);
        Controls.Add(painel);
        Controls.Add(barraCardapio);
    }

    private void ExportarCardapio()
    {
        try
        {
            using var dlg = new FolderBrowserDialog { Description = "Onde salvar o cardapio exportado?" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            var caminho = _servico.ExportarCardapio(dlg.SelectedPath);
            MessageBox.Show("Cardapio exportado:\n" + caminho, "Exportar cardapio",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Erro ao exportar: " + ex.Message, "Exportar cardapio",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ImportarCardapio()
    {
        var confirma = MessageBox.Show(
            "Importar vai SUBSTITUIR o cardapio atual (produtos e categorias) pelo do arquivo.\n" +
            "As vendas e turnos NAO sao afetados.\n\nContinuar?",
            "Importar cardapio", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (confirma != DialogResult.Yes) return;

        using var dlg = new OpenFileDialog { Filter = "Cardapio (*.json)|*.json" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            int n = _servico.ImportarCardapio(dlg.FileName);
            CarregarGrid();
            NovoProduto();
            MessageBox.Show($"Cardapio importado: {n} produto(s).", "Importar cardapio",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Erro ao importar (arquivo invalido?): " + ex.Message, "Importar cardapio",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void AddCampo(TableLayoutPanel painel, int row, string rotulo, Control ctrl)
    {
        painel.Controls.Add(new Label { Text = rotulo, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        painel.Controls.Add(ctrl, 1, row);
    }

    private static Button Botao(string texto, Color cor, EventHandler onClick)
    {
        var b = new Button
        {
            Text = texto, Dock = DockStyle.Fill, Height = 44, Margin = new Padding(3),
            BackColor = cor, ForeColor = Color.White, FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold)
        };
        b.Click += onClick;
        return b;
    }

    // -------------------------------------------------------------------------

    private List<Produto> _produtos = new();

    private void CarregarGrid()
    {
        _produtos = _servico.ProdutosTodos();
        _grid.Rows.Clear();
        foreach (var p in _produtos)
            _grid.Rows.Add(p.Id, p.Nome, Dinheiro.Formatar(p.PrecoCentavos), p.Categoria,
                p.Ativo ? "Sim" : "NAO");

        // categorias ATIVAS no combo (na ordem definida); usuario ainda pode digitar uma nova
        _cmbCategoria.Items.Clear();
        _cmbCategoria.Items.AddRange(_servico.CategoriasAtivas().Select(c => c.Nome).ToArray());
    }

    private void SelecionarLinha()
    {
        if (_grid.CurrentRow is null || _grid.CurrentRow.Index < 0) return;
        var id = _grid.CurrentRow.Cells["id"].Value?.ToString();
        var p = _produtos.FirstOrDefault(x => x.Id == id);
        if (p is null) return;

        _editandoId = p.Id;
        _txtNome.Text = p.Nome;
        _txtPreco.Text = (p.PrecoCentavos / 100m).ToString("0.00", CultureInfo.GetCultureInfo("pt-BR"));
        _cmbCategoria.Text = p.Categoria;
        _chkAtivo.Checked = p.Ativo;
        _lblStatus.ForeColor = Color.FromArgb(60, 60, 60);
        _lblStatus.Text = $"Editando: {p.Nome}" + (_servico.Repo.ProdutoTemVendas(p.Id) ? "\n(ja tem vendas - use Inativar, nao apague)" : "");
    }

    private void NovoProduto()
    {
        _editandoId = null;
        _txtNome.Text = "";
        _txtPreco.Text = "0,00";
        _cmbCategoria.Text = "Geral";
        _chkAtivo.Checked = true;
        _lblStatus.ForeColor = Color.FromArgb(60, 60, 60);
        _lblStatus.Text = "Novo produto. O atalho do teclado e automatico (letra da categoria + numero).";
        _txtNome.Focus();
    }

    private void Salvar()
    {
        var nome = _txtNome.Text.Trim();
        if (string.IsNullOrWhiteSpace(nome))
        {
            AvisoStatus("Informe o nome do produto."); _txtNome.Focus(); return;
        }
        var preco = Dinheiro.ParseCentavos(_txtPreco.Text);
        if (preco is null)
        {
            AvisoStatus("Preço inválido (ex: 6,00)."); _txtPreco.Focus(); _txtPreco.SelectAll(); return;
        }

        var id = _editandoId ?? GerarIdUnico(nome);

        var produto = new Produto
        {
            Id = id,
            Nome = nome,
            PrecoCentavos = preco.Value,
            Categoria = string.IsNullOrWhiteSpace(_cmbCategoria.Text) ? "Geral" : _cmbCategoria.Text.Trim(),
            Atalho = 0,   // legado: atalho do teclado agora e por posicao (nao cadastrado)
            Ativo = _chkAtivo.Checked
        };
        _servico.GarantirCategoria(produto.Categoria);   // cria a categoria se for nova
        _servico.Repo.SalvarProduto(produto);

        CarregarGrid();
        _editandoId = produto.Id;
        _lblStatus.ForeColor = Color.FromArgb(0, 120, 0);
        _lblStatus.Text = $"Salvo: {produto.Nome} ({Dinheiro.Formatar(produto.PrecoCentavos)}).";
    }

    private void Inativar()
    {
        if (_editandoId is null)
        {
            AvisoStatus("Selecione um produto na lista para inativar.");
            return;
        }
        var r = MessageBox.Show(
            "Inativar oculta o produto do caixa, mas PRESERVA o historico de vendas.\n\nContinuar?",
            "Inativar produto", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (r != DialogResult.Yes) return;

        _servico.Repo.InativarProduto(_editandoId);
        CarregarGrid();
        NovoProduto();
        _lblStatus.ForeColor = Color.FromArgb(180, 80, 0);
        _lblStatus.Text = "Produto inativado (oculto do caixa).";
    }

    private void Excluir()
    {
        if (_editandoId is null)
        {
            AvisoStatus("Selecione um produto na lista para excluir.");
            return;
        }
        var nome = _txtNome.Text.Trim();

        // TRAVA: produto com vendas NUNCA e apagado (protege a auditoria) -> so Inativar.
        if (_servico.Repo.ProdutoTemVendas(_editandoId))
        {
            MessageBox.Show(
                $"'{nome}' ja tem vendas registradas e NAO pode ser excluido (isso quebraria o historico).\n\n" +
                "Use 'Inativar' para oculta-lo do caixa.",
                "Excluir produto", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // dupla confirmacao para acao destrutiva e irreversivel.
        var r = MessageBox.Show(
            $"EXCLUIR permanentemente o produto '{nome}'?\n\nEssa acao NAO pode ser desfeita.",
            "Excluir produto", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (r != DialogResult.Yes) return;
        var r2 = MessageBox.Show(
            $"Tem certeza? '{nome}' sera apagado de vez.",
            "Confirmar exclusao", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (r2 != DialogResult.Yes) return;

        _servico.Repo.ExcluirProduto(_editandoId);
        CarregarGrid();
        NovoProduto();
        _lblStatus.ForeColor = Color.FromArgb(160, 0, 0);
        _lblStatus.Text = $"Produto '{nome}' excluido permanentemente.";
    }

    private void AvisoStatus(string msg)
    {
        _lblStatus.ForeColor = Color.FromArgb(180, 0, 0);
        _lblStatus.Text = msg;
    }

    private string GerarIdUnico(string nome)
    {
        var baseId = Slug(nome);
        var id = baseId;
        int n = 2;
        while (_servico.Repo.ProdutoExiste(id))
            id = $"{baseId}_{n++}";
        return id;
    }

    /// <summary>Transforma "Maca do Amor" em "maca_do_amor" (sem acentos/simbolos).</summary>
    private static string Slug(string nome)
    {
        var norm = nome.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in norm)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            else if (ch is ' ' or '-' or '_') sb.Append('_');
        }
        var s = sb.ToString().Trim('_');
        return string.IsNullOrEmpty(s) ? "produto" : s;
    }
}
