using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.Versioning;

namespace PdvFesta.App;

/// <summary>
/// Grafico de barras horizontais desenhado em GDI+ (zero dependencias externas).
/// Escala automaticamente com o tamanho do controle: janela maior = grafico maior
/// (efeito de "zoom"). Fontes e barras sao proporcionais a altura.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class GraficoBarras : Panel
{
    public string Titulo { get; set; } = "";
    /// <summary>Como formatar o numero no fim da barra (ex: moeda ou "154 un").</summary>
    public Func<long, string> FormatarValor { get; set; } = v => v.ToString();

    private List<(string rotulo, long valor, Color cor)> _dados = new();

    public GraficoBarras()
    {
        DoubleBuffered = true;
        BackColor = Color.White;
        Padding = new Padding(4);
    }

    public void Definir(IEnumerable<(string rotulo, long valor, Color cor)> dados)
    {
        _dados = dados.ToList();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        float w = Width, h = Height;
        const float pad = 12f;
        float tituloH = Math.Clamp(h * 0.12f, 22f, 46f);

        using var fTitulo = new Font("Segoe UI", Math.Clamp(h * 0.045f, 11f, 20f), FontStyle.Bold);
        using var fRotulo = new Font("Segoe UI", Math.Clamp(h * 0.032f, 8.5f, 14f), FontStyle.Bold);
        using var brushText = new SolidBrush(Color.FromArgb(55, 55, 55));

        if (!string.IsNullOrEmpty(Titulo))
            g.DrawString(Titulo, fTitulo, brushText, pad, pad);

        if (_dados.Count == 0)
        {
            g.DrawString("(sem dados ainda)", fRotulo, Brushes.Gray, pad, tituloH + pad);
            return;
        }

        float top = pad + tituloH;
        float areaH = h - top - pad;
        float rowH = areaH / _dados.Count;
        float barH = Math.Min(rowH * 0.62f, 48f);
        long max = Math.Max(1, _dados.Max(d => d.valor));

        float labelW = w * 0.26f;
        float barX = pad + labelW;
        float barMaxW = w - barX - pad - w * 0.20f;   // reserva espaco p/ o valor
        if (barMaxW < 20) barMaxW = Math.Max(20, w - barX - pad);

        for (int i = 0; i < _dados.Count; i++)
        {
            var (rotulo, valor, cor) = _dados[i];
            float cy = top + i * rowH + (rowH - barH) / 2f;
            float txtY = cy + (barH - fRotulo.Height) / 2f;

            g.DrawString(Encurtar(rotulo, 16), fRotulo, brushText, pad, txtY);

            float bw = (float)valor / max * barMaxW;
            if (valor > 0 && bw < 3) bw = 3;
            using (var brush = new SolidBrush(cor))
                g.FillRectangle(brush, barX, cy, bw, barH);
            using (var pen = new Pen(Color.FromArgb(60, 0, 0, 0)))
                g.DrawRectangle(pen, barX, cy, Math.Max(bw, 1), barH);

            g.DrawString(FormatarValor(valor), fRotulo, brushText, barX + bw + 6, txtY);
        }
    }

    private static string Encurtar(string s, int max) =>
        s.Length <= max ? s : s[..(max - 1)] + "…";
}
