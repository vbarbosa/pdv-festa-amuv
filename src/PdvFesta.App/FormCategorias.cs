using System.Runtime.Versioning;
using PdvFesta.Core;

namespace PdvFesta.App;

/// <summary>
/// CRUD de categorias (abas do caixa). Permite criar, renomear a ordem e inativar
/// (soft delete) categorias. A ordem controla a posicao das abas na tela de vendas.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FormCategorias : Form
{
    private readonly Servico _servico;
    private readonly DataGridView _grid = new();
    private readonly TextBox _txtNome = new();
    private readonly NumericUpDown _numOrdem = new();
    private readonly CheckBox _chkAtivo = new();
    private readonly Label _lblStatus = new();

    private string? _editandoNome;
    private List<Categoria> _categorias = new();

    public FormCategorias(Servico servico)
    {
        _servico = servico;
        Text = "Gerenciar Categorias";
        Name = "FormCategorias";
        Icon = Marca.Icone();
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(680, 460);
        ClientSize = new Size(720, 480);
        Font = new Font("Segoe UI", 11F);

        MontarLayout();
        CarregarGrid();
        NovaCategoria();
    }

    private void MontarLayout()
    {
        var painel = new TableLayoutPanel
        {
            Dock = DockStyle.Right, Width = 300, ColumnCount = 2, Padding = new Padding(12),
            BackColor = Color.FromArgb(245, 245, 245)
        };
        painel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        painel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var cab = new Label
        {
            Text = "CATEGORIA", Dock = DockStyle.Fill, Height = 36,
            Font = new Font("Segoe UI", 13F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft
        };
        painel.Controls.Add(cab, 0, 0); painel.SetColumnSpan(cab, 2);

        _txtNome.Dock = DockStyle.Fill; _txtNome.Font = new Font("Segoe UI", 12F);
        painel.Controls.Add(new Label { Text = "Nome:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
        painel.Controls.Add(_txtNome, 1, 1);

        _numOrdem.Dock = DockStyle.Fill; _numOrdem.Minimum = 0; _numOrdem.Maximum = 999;
        _numOrdem.Font = new Font("Segoe UI", 12F);
        painel.Controls.Add(new Label { Text = "Ordem:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 2);
        painel.Controls.Add(_numOrdem, 1, 2);

        _chkAtivo.Text = "Ativa (aparece no caixa)"; _chkAtivo.Dock = DockStyle.Fill; _chkAtivo.Checked = true;
        painel.Controls.Add(new Label { Text = "", Width = 1 }, 0, 3);
        painel.Controls.Add(_chkAtivo, 1, 3);

        painel.Controls.Add(Botao("Nova", Color.FromArgb(70, 70, 90), (s, e) => NovaCategoria()), 0, 4);
        painel.Controls.Add(Botao("Salvar / Atualizar", Color.FromArgb(0, 130, 0), (s, e) => Salvar()), 1, 4);
        var btnInativar = Botao("Inativar (ocultar aba)", Color.FromArgb(180, 80, 0), (s, e) => Inativar());
        painel.Controls.Add(btnInativar, 0, 5); painel.SetColumnSpan(btnInativar, 2);

        var btnExcluir = Botao("Excluir permanentemente", Color.FromArgb(160, 0, 0), (s, e) => Excluir());
        btnExcluir.Name = "btnExcluirCategoria";
        painel.Controls.Add(btnExcluir, 0, 6); painel.SetColumnSpan(btnExcluir, 2);

        _lblStatus.Dock = DockStyle.Fill; _lblStatus.Height = 54; _lblStatus.ForeColor = Color.FromArgb(60, 60, 60);
        painel.Controls.Add(_lblStatus, 0, 7); painel.SetColumnSpan(_lblStatus, 2);

        _grid.Name = "gridCategorias";
        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true; _grid.AllowUserToAddRows = false; _grid.AllowUserToDeleteRows = false;
        _grid.MultiSelect = false; _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.RowHeadersVisible = false; _grid.BorderStyle = BorderStyle.None; _grid.BackgroundColor = Color.White;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        EstiloGrid.Padronizar(_grid);   // cabecalho legivel (sem cortar texto)
        _grid.Columns.Add("nome", "Categoria");
        _grid.Columns.Add("ordem", "Ordem");
        _grid.Columns.Add("ativo", "Ativa");
        _grid.Columns["ordem"]!.FillWeight = 30;
        _grid.Columns["ativo"]!.FillWeight = 30;
        _grid.Columns["ordem"]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        _grid.SelectionChanged += (s, e) => SelecionarLinha();

        Controls.Add(_grid);
        Controls.Add(painel);
    }

    private static Button Botao(string texto, Color cor, EventHandler onClick)
    {
        var b = new Button
        {
            Text = texto, Dock = DockStyle.Fill, Height = 42, Margin = new Padding(3),
            BackColor = cor, ForeColor = Color.White, FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold)
        };
        b.Click += onClick;
        return b;
    }

    private void CarregarGrid()
    {
        _categorias = _servico.Categorias();
        _grid.Rows.Clear();
        foreach (var c in _categorias)
            _grid.Rows.Add(c.Nome, c.Ordem, c.Ativo ? "Sim" : "NAO");
    }

    private void SelecionarLinha()
    {
        if (_grid.CurrentRow is null || _grid.CurrentRow.Index < 0) return;
        var nome = _grid.CurrentRow.Cells["nome"].Value?.ToString();
        var c = _categorias.FirstOrDefault(x => x.Nome == nome);
        if (c is null) return;
        _editandoNome = c.Nome;
        _txtNome.Text = c.Nome;
        _numOrdem.Value = Math.Clamp(c.Ordem, 0, 999);
        _chkAtivo.Checked = c.Ativo;
        _lblStatus.Text = $"Editando: {c.Nome}";
    }

    private void NovaCategoria()
    {
        _editandoNome = null;
        _txtNome.Text = "";
        _numOrdem.Value = _categorias.Count == 0 ? 0 : Math.Min(999, _categorias.Max(c => c.Ordem) + 1);
        _chkAtivo.Checked = true;
        _lblStatus.Text = "Nova categoria.";
        _txtNome.Focus();
    }

    private void Salvar()
    {
        var nome = _txtNome.Text.Trim();
        if (string.IsNullOrWhiteSpace(nome))
        {
            _lblStatus.ForeColor = Color.FromArgb(180, 0, 0);
            _lblStatus.Text = "Informe o nome da categoria.";
            _txtNome.Focus();
            return;
        }
        // renomear = criar a nova + inativar a antiga (produtos antigos mantem o nome antigo)
        _servico.SalvarCategoria(new Categoria { Nome = nome, Ordem = (int)_numOrdem.Value, Ativo = _chkAtivo.Checked });
        CarregarGrid();
        _editandoNome = nome;
        _lblStatus.ForeColor = Color.FromArgb(0, 120, 0);
        _lblStatus.Text = $"Categoria \"{nome}\" salva.";
    }

    private void Inativar()
    {
        if (_editandoNome is null)
        {
            _lblStatus.ForeColor = Color.FromArgb(180, 0, 0);
            _lblStatus.Text = "Selecione uma categoria para inativar.";
            return;
        }
        var r = MessageBox.Show(
            $"Inativar \"{_editandoNome}\" oculta a aba no caixa.\nOs produtos dela ficam ocultos ate reativar.\n\nContinuar?",
            "Inativar categoria", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (r != DialogResult.Yes) return;
        _servico.InativarCategoria(_editandoNome);
        CarregarGrid();
        NovaCategoria();
        _lblStatus.ForeColor = Color.FromArgb(180, 80, 0);
        _lblStatus.Text = "Categoria inativada.";
    }

    private void Excluir()
    {
        if (_editandoNome is null)
        {
            _lblStatus.ForeColor = Color.FromArgb(180, 0, 0);
            _lblStatus.Text = "Selecione uma categoria para excluir.";
            return;
        }
        // TRAVA: categoria com produtos nao pode ser apagada (evita produtos orfaos).
        int qtd = _servico.ContarProdutosDaCategoria(_editandoNome);
        if (qtd > 0)
        {
            MessageBox.Show(
                $"A categoria \"{_editandoNome}\" tem {qtd} produto(s) e NAO pode ser excluida.\n\n" +
                "Mova/exclua os produtos primeiro, ou use 'Inativar' para ocultar a aba.",
                "Excluir categoria", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var r = MessageBox.Show(
            $"EXCLUIR permanentemente a categoria \"{_editandoNome}\"?\n\nEssa acao NAO pode ser desfeita.",
            "Excluir categoria", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (r != DialogResult.Yes) return;

        _servico.ExcluirCategoria(_editandoNome);
        CarregarGrid();
        NovaCategoria();
        _lblStatus.ForeColor = Color.FromArgb(160, 0, 0);
        _lblStatus.Text = "Categoria excluida permanentemente.";
    }
}
