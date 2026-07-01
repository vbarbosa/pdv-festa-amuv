using System.Windows.Forms;
using PdvFesta.App;
using Xunit;

namespace PdvFesta.Tests;

/// <summary>
/// EstiloGrid.Padronizar deve dar ao cabecalho altura/fonte legiveis (evita o corte de
/// texto tipo "Pagamento"). Teste leve das propriedades resultantes.
/// </summary>
public class EstiloGridTests
{
    [Fact]
    public void Padronizar_AplicaCabecalhoLegivel()
    {
        using var grid = new DataGridView();
        EstiloGrid.Padronizar(grid);

        Assert.False(grid.EnableHeadersVisualStyles);              // usa estilo proprio
        Assert.Equal(36, grid.ColumnHeadersHeight);               // altura suficiente
        Assert.Equal(DataGridViewColumnHeadersHeightSizeMode.DisableResizing, grid.ColumnHeadersHeightSizeMode);
        Assert.True(grid.ColumnHeadersDefaultCellStyle.Font!.Bold);   // cabecalho em negrito
        Assert.False(grid.AllowUserToResizeRows);
        Assert.Equal(30, grid.RowTemplate.Height);
    }
}
