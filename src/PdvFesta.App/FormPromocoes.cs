using System.Runtime.Versioning;
using PdvFesta.Core;

namespace PdvFesta.App;

/// <summary>
/// Gestao de Promocoes/Combos (Motor de Precos). Cadastra:
///  - Preco Especial: desconto num item (geralmente por horario).
///  - Combo: conjunto de itens que da um desconto fixo por conjunto completo.
/// Com janela de horario (DateTimePicker) e liga/desliga instantaneo. Soft delete.
/// A auto-deteccao no carrinho usa essas regras (linha verde de desconto).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FormPromocoes : Form
{
    private readonly Servico _servico;
    private readonly DataGridView _grid = new();
    private readonly TextBox _txtDesc = new();
    private readonly ComboBox _cmbTipo = new();
    private readonly TextBox _txtDesconto = new();
    private readonly CheckBox _chkAtivo = new();
    private readonly CheckBox _chkHorario = new();
    private readonly DateTimePicker _dtpInicio = new();
    private readonly DateTimePicker _dtpFim = new();
    // agendamento avancado: intervalo de datas + dias da semana
    private readonly CheckBox _chkDatas = new();
    private readonly DateTimePicker _dtpData1 = new();
    private readonly DateTimePicker _dtpData2 = new();
    private readonly CheckBox _chkDias = new();
    private readonly CheckBox[] _chkDia = new CheckBox[7];   // Dom..Sab
    private readonly ComboBox _cmbProduto = new();
    private readonly NumericUpDown _numQtd = new();
    private readonly ListBox _lstItens = new();
    private readonly Label _lblStatus = new();

    private long _editandoId;
    private readonly List<PromocaoItem> _itensEdit = new();
    private List<Produto> _produtos = new();
    private List<Promocao> _promos = new();

    private sealed record Opcao(string Id, string Nome) { public override string ToString() => Nome; }

    public FormPromocoes(Servico servico)
    {
        _servico = servico;
        Text = "Gerenciar Promoções / Combos";
        Name = "FormPromocoes";
        Icon = Marca.Icone();
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(900, 560);
        ClientSize = new Size(940, 580);
        Font = new Font("Segoe UI", 10.5F);

        MontarLayout();
        CarregarProdutos();
        CarregarGrid();
        Novo();
    }

    private void MontarLayout()
    {
        var painel = new TableLayoutPanel
        {
            Dock = DockStyle.Right, Width = 420, ColumnCount = 2, Padding = new Padding(12),
            BackColor = Color.FromArgb(245, 245, 245), AutoScroll = true
        };
        painel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        painel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        int row = 0;
        void Linha(string rot, Control c) { painel.Controls.Add(new Label { Text = rot, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row); painel.Controls.Add(c, 1, row); row++; }
        void Full(Control c) { painel.Controls.Add(c, 0, row); painel.SetColumnSpan(c, 2); row++; }

        Full(new Label { Text = "CADASTRO DE PROMOÇÃO", Font = new Font("Segoe UI", 13F, FontStyle.Bold), Height = 32, Dock = DockStyle.Fill });

        _txtDesc.Dock = DockStyle.Fill;
        Linha("Descrição:", _txtDesc);

        _cmbTipo.Dock = DockStyle.Fill; _cmbTipo.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbTipo.Items.AddRange(new object[] { "Preço Especial (1 item)", "Combo (vários itens)" });
        _cmbTipo.SelectedIndex = 0;
        Linha("Tipo:", _cmbTipo);

        _txtDesconto.Dock = DockStyle.Fill; _txtDesconto.TextAlign = HorizontalAlignment.Right; _txtDesconto.Text = "0,00";
        Linha("Desconto R$:", _txtDesconto);

        _chkAtivo.Text = "Ativa (liga/desliga na hora)"; _chkAtivo.Dock = DockStyle.Fill; _chkAtivo.Checked = true;
        Full(_chkAtivo);

        _chkHorario.Text = "Restringir por horário"; _chkHorario.Dock = DockStyle.Fill;
        _chkHorario.CheckedChanged += (s, e) => AtualizarEstadoAgendamento();
        Full(_chkHorario);

        _dtpInicio.Format = DateTimePickerFormat.Time; _dtpInicio.ShowUpDown = true; _dtpInicio.Dock = DockStyle.Fill;
        _dtpFim.Format = DateTimePickerFormat.Time; _dtpFim.ShowUpDown = true; _dtpFim.Dock = DockStyle.Fill;
        Linha("Das (hora):", _dtpInicio);
        Linha("Até (hora):", _dtpFim);

        // ---- intervalo de DATAS (de/ate; para 1 dia so, ponha a mesma data nos dois) ----
        _chkDatas.Text = "Restringir por data (período)"; _chkDatas.Dock = DockStyle.Fill;
        _chkDatas.CheckedChanged += (s, e) => AtualizarEstadoAgendamento();
        Full(_chkDatas);
        _dtpData1.Format = DateTimePickerFormat.Short; _dtpData1.Dock = DockStyle.Fill;
        _dtpData2.Format = DateTimePickerFormat.Short; _dtpData2.Dock = DockStyle.Fill;
        Linha("De (dia):", _dtpData1);
        Linha("Até (dia):", _dtpData2);

        // ---- DIAS DA SEMANA (checkboxes Dom..Sab) ----
        _chkDias.Text = "Só em certos dias da semana"; _chkDias.Dock = DockStyle.Fill;
        _chkDias.CheckedChanged += (s, e) => AtualizarEstadoAgendamento();
        Full(_chkDias);
        var nomesDia = new[] { "Dom", "Seg", "Ter", "Qua", "Qui", "Sex", "Sab" };
        var painelDias = new FlowLayoutPanel { Dock = DockStyle.Fill, Height = 32, AutoSize = true, Margin = new Padding(0) };
        for (int i = 0; i < 7; i++)
        {
            _chkDia[i] = new CheckBox { Text = nomesDia[i], AutoSize = true, Margin = new Padding(2, 4, 2, 0) };
            painelDias.Controls.Add(_chkDia[i]);
        }
        Full(painelDias);

        Full(new Label { Text = "ITENS EXIGIDOS (produto x qtd):", Font = new Font("Segoe UI", 10F, FontStyle.Bold), Height = 26, Dock = DockStyle.Fill });
        _cmbProduto.Dock = DockStyle.Fill; _cmbProduto.DropDownStyle = ComboBoxStyle.DropDownList;
        _numQtd.Dock = DockStyle.Fill; _numQtd.Minimum = 1; _numQtd.Maximum = 99; _numQtd.Value = 1;
        Linha("Produto:", _cmbProduto);
        Linha("Qtd:", _numQtd);
        var barraItem = new FlowLayoutPanel { Dock = DockStyle.Fill, Height = 40, AutoSize = true };
        barraItem.Controls.Add(Botao("Adicionar item", Color.FromArgb(0, 120, 160), 130, (s, e) => AddItem()));
        barraItem.Controls.Add(Botao("Remover item", Color.FromArgb(150, 80, 0), 130, (s, e) => RemItem()));
        Full(barraItem);
        _lstItens.Dock = DockStyle.Fill; _lstItens.Height = 90;
        Full(_lstItens);

        var barra = new FlowLayoutPanel { Dock = DockStyle.Fill, Height = 46, AutoSize = true };
        barra.Controls.Add(Botao("Novo", Color.FromArgb(70, 70, 90), 90, (s, e) => Novo()));
        barra.Controls.Add(Botao("Salvar", Color.FromArgb(0, 130, 0), 120, (s, e) => Salvar()));
        barra.Controls.Add(Botao("Inativar", Color.FromArgb(180, 80, 0), 100, (s, e) => Inativar()));
        barra.Controls.Add(Botao("Excluir", Color.FromArgb(160, 0, 0), 100, (s, e) => Excluir()));
        Full(barra);

        _lblStatus.Dock = DockStyle.Fill; _lblStatus.Height = 44; _lblStatus.ForeColor = Color.FromArgb(60, 60, 60);
        Full(_lblStatus);

        // grid a esquerda
        _grid.Name = "gridPromocoes"; _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true; _grid.AllowUserToAddRows = false; _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; _grid.RowHeadersVisible = false;
        _grid.BorderStyle = BorderStyle.None; _grid.BackgroundColor = Color.White;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        EstiloGrid.Padronizar(_grid);   // cabecalho legivel (sem cortar texto)
        _grid.Columns.Add("id", "Id"); _grid.Columns["id"]!.Visible = false;
        _grid.Columns.Add("desc", "Promoção");
        _grid.Columns.Add("tipo", "Tipo");
        _grid.Columns.Add("desc2", "Desconto");
        _grid.Columns.Add("hora", "Horário");
        _grid.Columns.Add("agenda", "Agenda");
        _grid.Columns.Add("ativo", "Ativa");
        _grid.SelectionChanged += (s, e) => SelecionarLinha();

        Controls.Add(_grid);
        Controls.Add(painel);

        AtualizarEstadoAgendamento();
    }

    private static Button Botao(string t, Color cor, int w, EventHandler onClick)
    {
        var b = new Button { Text = t, Width = w, Height = 38, Margin = new Padding(3), BackColor = cor, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
        b.Click += onClick; return b;
    }

    private void AtualizarEstadoAgendamento()
    {
        _dtpInicio.Enabled = _chkHorario.Checked;
        _dtpFim.Enabled = _chkHorario.Checked;
        _dtpData1.Enabled = _chkDatas.Checked;
        _dtpData2.Enabled = _chkDatas.Checked;
        foreach (var c in _chkDia) c.Enabled = _chkDias.Checked;
    }

    /// <summary>Le os checkboxes Dom..Sab e monta as flags DiasSemana. Nenhum marcado = Todos.</summary>
    private DiasSemana LerDiasSelecionados()
    {
        var bits = new[] { DiasSemana.Domingo, DiasSemana.Segunda, DiasSemana.Terca, DiasSemana.Quarta,
                           DiasSemana.Quinta, DiasSemana.Sexta, DiasSemana.Sabado };
        var res = DiasSemana.Nenhum;
        for (int i = 0; i < 7; i++) if (_chkDia[i].Checked) res |= bits[i];
        return res == DiasSemana.Nenhum ? DiasSemana.Todos : res;
    }

    /// <summary>Marca os checkboxes Dom..Sab a partir das flags. Todos = todos marcados.</summary>
    private void AplicarDiasNaTela(DiasSemana dias)
    {
        var bits = new[] { DiasSemana.Domingo, DiasSemana.Segunda, DiasSemana.Terca, DiasSemana.Quarta,
                           DiasSemana.Quinta, DiasSemana.Sexta, DiasSemana.Sabado };
        for (int i = 0; i < 7; i++) _chkDia[i].Checked = (dias & bits[i]) != 0;
    }

    private void CarregarProdutos()
    {
        _produtos = _servico.Produtos();
        _cmbProduto.Items.Clear();
        _cmbProduto.Items.AddRange(_produtos.Select(p => new Opcao(p.Id, p.Nome)).ToArray());
        if (_cmbProduto.Items.Count > 0) _cmbProduto.SelectedIndex = 0;
    }

    private void CarregarGrid()
    {
        _promos = _servico.Promocoes();
        _grid.Rows.Clear();
        foreach (var p in _promos)
        {
            string horario = p.HoraInicio is not null && p.HoraFim is not null
                ? $"{p.HoraInicio:hh\\:mm}-{p.HoraFim:hh\\:mm}" : "sempre";
            _grid.Rows.Add(p.Id, p.Descricao, p.Tipo == TipoPromocao.Combo ? "Combo" : "Preco Esp.",
                CupomFormatter.Moeda(p.ValorDescontoCentavos), horario, ResumoAgenda(p), p.Ativo ? "Sim" : "NAO");
        }
    }

    /// <summary>Resumo curto do agendamento (datas + dias) para a coluna "Agenda" do grid.</summary>
    private static string ResumoAgenda(Promocao p)
    {
        var partes = new List<string>();
        if (p.DataInicio is DateTime di && p.DataFim is DateTime df)
            partes.Add(di.Date == df.Date ? di.ToString("dd/MM") : $"{di:dd/MM}-{df:dd/MM}");
        else if (p.DataInicio is DateTime di2) partes.Add($"a partir {di2:dd/MM}");
        else if (p.DataFim is DateTime df2) partes.Add($"ate {df2:dd/MM}");

        if (p.Dias != DiasSemana.Todos && p.Dias != DiasSemana.Nenhum)
        {
            var nomes = new (DiasSemana bit, string txt)[]
            {
                (DiasSemana.Domingo, "Dom"), (DiasSemana.Segunda, "Seg"), (DiasSemana.Terca, "Ter"),
                (DiasSemana.Quarta, "Qua"), (DiasSemana.Quinta, "Qui"), (DiasSemana.Sexta, "Sex"),
                (DiasSemana.Sabado, "Sab")
            };
            partes.Add(string.Join("/", nomes.Where(n => (p.Dias & n.bit) != 0).Select(n => n.txt)));
        }
        return partes.Count == 0 ? "todo dia" : string.Join("  ", partes);
    }

    private void SelecionarLinha()
    {
        if (_grid.CurrentRow is null || _grid.CurrentRow.Index < 0) return;
        var id = Convert.ToInt64(_grid.CurrentRow.Cells["id"].Value ?? 0L);
        var p = _promos.FirstOrDefault(x => x.Id == id);
        if (p is null) return;
        _editandoId = p.Id;
        _txtDesc.Text = p.Descricao;
        _cmbTipo.SelectedIndex = p.Tipo == TipoPromocao.Combo ? 1 : 0;
        _txtDesconto.Text = (p.ValorDescontoCentavos / 100m).ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
        _chkAtivo.Checked = p.Ativo;
        _chkHorario.Checked = p.HoraInicio is not null;
        if (p.HoraInicio is not null) _dtpInicio.Value = DateTime.Today.Add(p.HoraInicio.Value);
        if (p.HoraFim is not null) _dtpFim.Value = DateTime.Today.Add(p.HoraFim.Value);

        // agendamento avancado: datas + dias da semana
        _chkDatas.Checked = p.DataInicio is not null || p.DataFim is not null;
        if (p.DataInicio is DateTime di) _dtpData1.Value = di;
        if (p.DataFim is DateTime df) _dtpData2.Value = df;
        _chkDias.Checked = p.Dias != DiasSemana.Todos && p.Dias != DiasSemana.Nenhum;
        AplicarDiasNaTela(p.Dias == DiasSemana.Nenhum ? DiasSemana.Todos : p.Dias);

        _itensEdit.Clear();
        _itensEdit.AddRange(p.Itens.Select(i => new PromocaoItem { ProdutoId = i.ProdutoId, Quantidade = i.Quantidade }));
        RedesenharItens();
        AtualizarEstadoAgendamento();
        _lblStatus.Text = $"Editando: {p.Descricao}";
    }

    private void RedesenharItens()
    {
        _lstItens.Items.Clear();
        foreach (var it in _itensEdit)
        {
            var nome = _produtos.FirstOrDefault(p => p.Id == it.ProdutoId)?.Nome ?? it.ProdutoId;
            _lstItens.Items.Add($"{it.Quantidade}x {nome}");
        }
    }

    private void AddItem()
    {
        if (_cmbProduto.SelectedItem is not Opcao o) return;
        var existente = _itensEdit.FirstOrDefault(i => i.ProdutoId == o.Id);
        if (existente is not null) existente.Quantidade = (int)_numQtd.Value;
        else _itensEdit.Add(new PromocaoItem { ProdutoId = o.Id, Quantidade = (int)_numQtd.Value });
        RedesenharItens();
    }

    private void RemItem()
    {
        int i = _lstItens.SelectedIndex;
        if (i >= 0 && i < _itensEdit.Count) { _itensEdit.RemoveAt(i); RedesenharItens(); }
    }

    private void Novo()
    {
        _editandoId = 0;
        _txtDesc.Text = ""; _cmbTipo.SelectedIndex = 0; _txtDesconto.Text = "0,00";
        _chkAtivo.Checked = true; _chkHorario.Checked = false;
        _dtpInicio.Value = DateTime.Today.AddHours(8); _dtpFim.Value = DateTime.Today.AddHours(20);
        _chkDatas.Checked = false; _dtpData1.Value = DateTime.Today; _dtpData2.Value = DateTime.Today;
        _chkDias.Checked = false; AplicarDiasNaTela(DiasSemana.Todos);
        _itensEdit.Clear(); RedesenharItens(); AtualizarEstadoAgendamento();
        _lblStatus.Text = "Nova promocao.";
        _txtDesc.Focus();
    }

    private void Salvar()
    {
        var desc = _txtDesc.Text.Trim();
        if (string.IsNullOrWhiteSpace(desc)) { Aviso("Informe a descricao."); return; }
        var valor = Dinheiro.ParseCentavos(_txtDesconto.Text);
        if (valor is null or 0) { Aviso("Informe o desconto (ex: 2,00)."); return; }
        if (_itensEdit.Count == 0) { Aviso("Adicione ao menos 1 item exigido."); return; }

        // datas: aceita de/ate em qualquer ordem (troca se o usuario inverteu).
        DateTime? d1 = null, d2 = null;
        if (_chkDatas.Checked)
        {
            d1 = _dtpData1.Value.Date;
            d2 = _dtpData2.Value.Date;
            if (d1 > d2) (d1, d2) = (d2, d1);
        }

        var p = new Promocao
        {
            Id = _editandoId,
            Descricao = desc,
            Tipo = _cmbTipo.SelectedIndex == 1 ? TipoPromocao.Combo : TipoPromocao.PrecoEspecial,
            ValorDescontoCentavos = valor.Value,
            Ativo = _chkAtivo.Checked,
            HoraInicio = _chkHorario.Checked ? _dtpInicio.Value.TimeOfDay : null,
            HoraFim = _chkHorario.Checked ? _dtpFim.Value.TimeOfDay : null,
            DataInicio = d1,
            DataFim = d2,
            Dias = _chkDias.Checked ? LerDiasSelecionados() : DiasSemana.Todos,
            Itens = _itensEdit.Select(i => new PromocaoItem { ProdutoId = i.ProdutoId, Quantidade = i.Quantidade }).ToList()
        };
        _editandoId = _servico.SalvarPromocao(p);   // persiste + recarrega o cache; retorna o Id
        CarregarGrid();
        _lblStatus.ForeColor = Color.FromArgb(0, 120, 0);
        _lblStatus.Text = $"Promocao \"{desc}\" salva.";
    }

    private void Inativar()
    {
        if (_editandoId <= 0) { Aviso("Selecione uma promoção para inativar."); return; }
        var r = MessageBox.Show(
            $"Inativar a promoção \"{_txtDesc.Text.Trim()}\"?\nEla para de valer, mas fica salva.",
            "Inativar promoção", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (r != DialogResult.Yes) return;
        _servico.InativarPromocao(_editandoId);
        CarregarGrid(); Novo();
        _lblStatus.ForeColor = Color.FromArgb(180, 80, 0);
        _lblStatus.Text = "Promocao inativada.";
    }

    private void Excluir()
    {
        if (_editandoId <= 0) { Aviso("Selecione uma promoção para excluir."); return; }
        var desc = _txtDesc.Text.Trim();
        // Promocoes podem ser excluidas com seguranca: a venda grava a LINHA de desconto,
        // nao a promocao. Excluir nao afeta vendas passadas. Mesmo assim, confirma.
        var r = MessageBox.Show(
            $"EXCLUIR permanentemente a promoção \"{desc}\"?\n\nEssa acao NAO pode ser desfeita.",
            "Excluir promoção", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (r != DialogResult.Yes) return;

        _servico.ExcluirPromocao(_editandoId);
        CarregarGrid(); Novo();
        _lblStatus.ForeColor = Color.FromArgb(160, 0, 0);
        _lblStatus.Text = "Promocao excluida permanentemente.";
    }

    private void Aviso(string m) { _lblStatus.ForeColor = Color.FromArgb(180, 0, 0); _lblStatus.Text = m; }
}
