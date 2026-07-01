using System.Runtime.Versioning;
using PdvFesta.Core;

namespace PdvFesta.App;

/// <summary>
/// Mapeia as teclas numericas 1-9 para produtos. Permite, no meio da festa, trocar
/// o atalho [1] de Quentao para Bolo sem recompilar. Salva no proprio produto
/// (campo Atalho), garantindo unicidade (um atalho -> um produto).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FormCustomizarAtalhos : Form
{
    private readonly Servico _servico;
    private readonly ComboBox[] _combos = new ComboBox[9];
    private List<Produto> _ativos = new();

    private sealed record Opcao(string Id, string Nome)
    {
        public override string ToString() => Nome;
    }

    public FormCustomizarAtalhos(Servico servico)
    {
        _servico = servico;
        Text = "Customizar Atalhos (1-9)";
        Name = "FormCustomizarAtalhos";
        Icon = Marca.Icone();
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(460, 560);
        ClientSize = new Size(480, 620);
        Font = new Font("Segoe UI", 11F);

        MontarLayout();
        Carregar();
    }

    private void MontarLayout()
    {
        var titulo = new Label
        {
            Text = "ATALHOS DO TECLADO", Dock = DockStyle.Top, Height = 44,
            TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.White,
            BackColor = Color.FromArgb(60, 60, 60), Font = new Font("Segoe UI", 14F, FontStyle.Bold)
        };

        var grade = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 9, Padding = new Padding(16), AutoScroll = true
        };
        grade.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        grade.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        for (int k = 0; k < 9; k++)
        {
            grade.Controls.Add(new Label
            {
                Text = $"Tecla [{k + 1}]", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, k);

            _combos[k] = new ComboBox
            {
                Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 12F), Margin = new Padding(3, 6, 3, 6)
            };
            grade.Controls.Add(_combos[k], 1, k);
        }

        var barra = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 60, Padding = new Padding(12) };
        barra.Controls.Add(Botao("Salvar atalhos", Color.FromArgb(0, 130, 0), (s, e) => Salvar()));
        barra.Controls.Add(Botao("Auto (1..9 em ordem)", Color.FromArgb(70, 70, 90), (s, e) => AutoAtribuir()));
        barra.Controls.Add(Botao("Limpar todos", Color.FromArgb(180, 80, 0), (s, e) => LimparTodos()));

        Controls.Add(grade);
        Controls.Add(barra);
        Controls.Add(titulo);
    }

    private static Button Botao(string texto, Color cor, EventHandler onClick)
    {
        var b = new Button
        {
            Text = texto, Width = 145, Height = 40, Margin = new Padding(3),
            BackColor = cor, ForeColor = Color.White, FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
        };
        b.Click += onClick;
        return b;
    }

    private void Carregar()
    {
        _ativos = _servico.Produtos();
        var opcoes = new List<Opcao> { new("", "— nenhum —") };
        opcoes.AddRange(_ativos.Select(p => new Opcao(p.Id, p.Nome)));

        for (int k = 1; k <= 9; k++)
        {
            var cmb = _combos[k - 1];
            cmb.Items.Clear();
            cmb.Items.AddRange(opcoes.ToArray());
            var atual = _ativos.FirstOrDefault(p => p.Atalho == k);
            cmb.SelectedIndex = atual is null ? 0 : opcoes.FindIndex(o => o.Id == atual.Id);
        }
    }

    private void AutoAtribuir()
    {
        for (int k = 0; k < 9; k++)
            _combos[k].SelectedIndex = (k + 1) < _combos[k].Items.Count ? (k + 1) : 0;
    }

    private void LimparTodos()
    {
        foreach (var cmb in _combos) cmb.SelectedIndex = 0;
    }

    private void Salvar()
    {
        // monta o mapa tecla->produtoId a partir dos combos
        var mapa = new Dictionary<int, string>();
        for (int k = 1; k <= 9; k++)
            if (_combos[k - 1].SelectedItem is Opcao o && !string.IsNullOrEmpty(o.Id))
            {
                if (mapa.ContainsValue(o.Id))
                {
                    MessageBox.Show($"O produto \"{o.Nome}\" foi atribuido a mais de uma tecla.\n" +
                        "Cada produto pode ter apenas um atalho.", "Atalhos",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                mapa[k] = o.Id;
            }

        // aplica: zera todos os atalhos e grava os novos (unicidade garantida)
        foreach (var p in _ativos)
        {
            int novo = mapa.FirstOrDefault(kv => kv.Value == p.Id).Key; // 0 se nao mapeado
            if (p.Atalho != novo)
            {
                p.Atalho = novo;
                _servico.Repo.SalvarProduto(p);
            }
        }

        MessageBox.Show("Atalhos salvos! Eles ja valem na tela do caixa.", "Atalhos",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
        DialogResult = DialogResult.OK;
        Close();
    }
}
