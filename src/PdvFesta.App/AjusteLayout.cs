using System.Runtime.Versioning;

namespace PdvFesta.App;

/// <summary>
/// BLINDAGEM DE LAYOUT (anti-corte). Garante que nenhuma janela abra com botoes/paineis
/// cortados exigindo scroll. Roda uma vez, quando o form fica visivel:
///   1. Deixa cada FlowLayoutPanel de botoes crescer o suficiente para NAO precisar de
///      barra de rolagem na largura atual (mede quantas fileiras os botoes ocupam).
///   2. Aumenta o MinimumSize / tamanho do form para caber o conteudo preferido.
/// E conservador: so AUMENTA (nunca encolhe), entao nunca quebra um layout ja correto.
/// Aplicado de forma central em Dialogos.Modal => vale para todas as telas, hoje e futuras.
/// </summary>
[SupportedOSPlatform("windows")]
public static class AjusteLayout
{
    /// <summary>Liga a blindagem: ajusta o form ANTES de aparecer (Load) e reforça no Shown.</summary>
    public static void Blindar(Form form)
    {
        void Ajustar()
        {
            try { AjustarAgora(form); } catch { /* layout nunca derruba o caixa */ }
        }
        // Load: ajusta o tamanho ANTES de a janela aparecer (evita "piscar cortado" e abrir
        // menor que o conteudo). Shown: reforca depois que ha tamanho real (fontes/DPI).
        form.Load += (_, _) => Ajustar();
        form.Shown += (_, _) => Ajustar();
    }

    private static void AjustarAgora(Form form)
    {
        // DPI: garante escala consistente em telas 125%/150%/4K (sem isto, alguns modais
        // abrem menores que o conteudo e cortam botoes).
        if (form.AutoScaleMode == AutoScaleMode.None)
            form.AutoScaleMode = AutoScaleMode.Dpi;

        form.SuspendLayout();

        // 1) barras de botoes (FlowLayoutPanel) que quebram em varias fileiras:
        //    calcula a altura necessaria e a aplica para nenhum botao ficar escondido.
        foreach (var fluxo in EncontrarFlow(form))
            GarantirAlturaFlow(fluxo);

        form.ResumeLayout(true);

        // 2) o form como um todo precisa caber o conteudo preferido (sem cortar o Bottom).
        var pref = form.PreferredSize;                 // tamanho que o layout gostaria de ter
        int larg = Math.Max(form.Width, pref.Width);
        int alt  = Math.Max(form.Height, pref.Height);

        // 2b) nunca maior que a area util da tela onde a janela esta (senao a barra
        //     de titulo/bordas saem da tela e o usuario nao consegue mover/fechar).
        var tela = Screen.FromControl(form).WorkingArea;
        larg = Math.Min(larg, tela.Width);
        alt  = Math.Min(alt, tela.Height);

        // nunca deixa o minimo menor que o necessario => usuario nao consegue "cortar" a tela
        var min = form.MinimumSize;
        form.MinimumSize = new Size(Math.Min(Math.Max(min.Width, larg), tela.Width),
                                    Math.Min(Math.Max(min.Height, alt), tela.Height));

        if (form.WindowState == FormWindowState.Normal && (larg > form.Width || alt > form.Height))
        {
            form.Size = new Size(larg, alt);
            // re-centraliza no pai apos crescer (senao parte da janela pode cair fora da tela)
            RecentralizarNaTela(form, tela);
        }
    }

    /// <summary>Garante que a janela (apos crescer) fique inteira dentro da area util da tela.</summary>
    private static void RecentralizarNaTela(Form form, Rectangle tela)
    {
        int x = form.Left, y = form.Top;
        if (x + form.Width > tela.Right) x = tela.Right - form.Width;
        if (y + form.Height > tela.Bottom) y = tela.Bottom - form.Height;
        if (x < tela.Left) x = tela.Left;
        if (y < tela.Top) y = tela.Top;
        form.Location = new Point(x, y);
    }

    /// <summary>Encontra recursivamente todos os FlowLayoutPanel do form.</summary>
    private static IEnumerable<FlowLayoutPanel> EncontrarFlow(Control raiz)
    {
        foreach (Control c in raiz.Controls)
        {
            if (c is FlowLayoutPanel f) yield return f;
            foreach (var neto in EncontrarFlow(c)) yield return neto;
        }
    }

    /// <summary>
    /// Ajusta a altura de um FlowLayoutPanel de botoes (LeftToRight + WrapContents) para
    /// caber TODAS as fileiras na largura atual, eliminando o scroll vertical.
    /// So faz sentido quando o painel esta ancorado (Dock/Anchor) e tem altura fixa.
    /// </summary>
    private static void GarantirAlturaFlow(FlowLayoutPanel fluxo)
    {
        if (fluxo.Controls.Count == 0) return;
        if (fluxo.FlowDirection != FlowDirection.LeftToRight || !fluxo.WrapContents) return;
        // paineis que ja crescem sozinhos (AutoSize) nao cortam nada.
        if (fluxo.AutoSize) return;

        int largDisp = fluxo.ClientSize.Width - fluxo.Padding.Horizontal;
        if (largDisp <= 0) return;

        // simula o wrap: soma larguras (com margem) ate estourar a largura -> nova fileira.
        int x = 0, fileiras = 1, alturaFileira = 0;
        foreach (Control b in fluxo.Controls)
        {
            int w = b.Width + b.Margin.Horizontal;
            int h = b.Height + b.Margin.Vertical;
            if (x > 0 && x + w > largDisp) { fileiras++; x = 0; }   // nao coube -> quebra
            x += w;
            alturaFileira = Math.Max(alturaFileira, h);
        }

        int necessaria = fileiras * alturaFileira + fluxo.Padding.Vertical;
        if (necessaria > fluxo.Height)
            fluxo.Height = necessaria;     // Dock.Bottom respeita a nova altura; some o scroll
    }
}
