using System.Runtime.Versioning;

namespace PdvFesta.App;

/// <summary>
/// Padroniza o visual das DataGridView do sistema. O foco e o CABECALHO: sem isto,
/// o header fica curto/estreito e CORTA o texto (ex: "Pagamento" virava "Pagament").
/// Aqui damos altura fixa, negrito, fundo cinza e padding — igual ao grid do carrinho.
/// </summary>
[SupportedOSPlatform("windows")]
public static class EstiloGrid
{
    /// <summary>Aplica o cabecalho legivel (altura 36, negrito 11, sem corte) a um grid.</summary>
    public static void Padronizar(DataGridView grid)
    {
        // headers com estilo proprio (nao o do tema do Windows, que ignora altura/fonte).
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.ColumnHeadersHeight = 36;                       // caixa alta o bastante p/ a fonte
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(230, 230, 230);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(40, 40, 40);
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(4, 4, 4, 4);
        grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;

        // linhas com altura confortavel (nao corta a fonte dos dados tambem).
        grid.RowTemplate.Height = 30;
        grid.AllowUserToResizeRows = false;
    }
}
