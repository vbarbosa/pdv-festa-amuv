using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using Xunit;

namespace PdvFesta.E2E;

/// <summary>
/// MODO DEMONSTRACAO (ator do video). ANEXA ao app JA ABERTO (o orquestrador abre antes
/// de gravar e fecha depois) e o opera de forma didatica. Usa TECLADO (atalhos 1/3, F2, F9)
/// — como o operador de verdade — e Invoke nos botoes dos modais. NAO move o mouse fisico
/// (seu mouse fica livre). So roda sob PDV_DEMO=1.
/// Roteiro: 1 caixa livre -> 2 adiciona itens -> 3 paga/troco -> 4 fechamento.
/// </summary>
[Collection("e2e")]
public sealed class DemoMode
{
    [Fact]
    public void GravarDemonstracao()
    {
        if (Environment.GetEnvironmentVariable("PDV_DEMO") != "1") return;

        var procs = Process.GetProcessesByName("PDV-Festa-AMUV");
        if (procs.Length == 0) return;

        using var automation = new UIA3Automation();
        var app = FlaUI.Core.Application.Attach(procs[0]);
        var janela = Esperar(() => app.GetMainWindow(automation, TimeSpan.FromSeconds(1)));
        if (janela is null) return;
        Frente(janela);

        // ===== CENA 1: abre o caixa (se o modal aparecer) =====
        EsperarDesktop(automation, "btnAbrirCaixa", 8)?.AsButton().Invoke();
        Pausa(2600);
        Frente(janela);

        // ===== CENA 2: NAVEGACAO POR TECLADO (categoria -> item -> Enter) =====
        // "B" abre Bebidas e destaca o 1o item (Quentao); Enter adiciona. Depois "I" abre
        // Bingo, "1" destaca a 1a cartela, Enter adiciona -> repete p/ 2 cartelas.
        Keyboard.Type("b"); Pausa(1300);   // categoria Bebidas (destaca Quentao)
        Keyboard.Press(VirtualKeyShort.ENTER); Pausa(1200);   // adiciona Quentao
        Keyboard.Type("i"); Pausa(1100);   // categoria bIngo (B ja usado -> letra I)
        Keyboard.Type("1"); Pausa(900);    // destaca 1a Cartela Bingo
        Keyboard.Press(VirtualKeyShort.ENTER); Pausa(1000);   // adiciona 1x
        Keyboard.Type("i"); Pausa(700);
        Keyboard.Type("1"); Pausa(700);
        Keyboard.Press(VirtualKeyShort.ENTER); Pausa(1600);   // adiciona 2x (total)

        // ===== CENA 3: F2 -> valor rapido R$50 (Invoke) -> confirmar =====
        Keyboard.Press(VirtualKeyShort.F2); Pausa(1600);
        EsperarDesktop(automation, "btnRapido_5000")?.AsButton().Invoke(); Pausa(2000);   // troco 3000
        EsperarDesktop(automation, "btnConfirmar")?.AsButton().Invoke(); Pausa(2000);

        // BLINDAGEM: em modo demo o app nem tenta imprimir (PDV_DEMO=1), entao o popup
        // "Impressora" nao deve aparecer. Mas se por qualquer motivo ele surgir, fechamos
        // (Cancelar) para NUNCA vazar no video. Defesa em profundidade.
        FecharModalImpressora(automation);

        // ===== CENA 4: F9 -> senha 0000 -> dashboard (totais em verde) =====
        Frente(janela);
        Keyboard.Press(VirtualKeyShort.F9); Pausa(1300);
        var senha = EsperarDesktop(automation, "txtSenhaAdmin", 10);
        if (senha is not null)
        {
            Keyboard.Type("0000"); Pausa(600);
            Keyboard.Press(VirtualKeyShort.ENTER); Pausa(3200);
        }
        Pausa(1200);
    }

    // ---------- helpers ----------
    // Ritmo do video: fator 1.5 deixa cada pausa 50% mais lenta (bem mais facil de acompanhar).
    private const double RitmoFator = 1.5;
    private static void Pausa(int ms) => System.Threading.Thread.Sleep((int)(ms * RitmoFator));

    private static void Frente(Window janela)
    {
        try { janela.Focus(); janela.SetForeground(); } catch { }
        System.Threading.Thread.Sleep(200);
    }

    private static T? Esperar<T>(Func<T?> obter, int tentativas = 30) where T : class
    {
        for (int i = 0; i < tentativas; i++)
        {
            var v = obter();
            if (v is not null) return v;
            System.Threading.Thread.Sleep(400);
        }
        return null;
    }

    private static AutomationElement? EsperarDesktop(UIA3Automation aut, string automationId, int tentativas = 25) =>
        Esperar(() => aut.GetDesktop().FindFirstDescendant(cf => cf.ByAutomationId(automationId)), tentativas);

    /// <summary>
    /// Fecha o MessageBox "Impressora" (Cancelar) se ele existir. Em demo o app nao
    /// deveria abri-lo, mas isto garante que ele nunca fique no video. Nao espera:
    /// se nao houver modal em ~1s, segue em frente sem atrapalhar o ritmo.
    /// </summary>
    private static void FecharModalImpressora(UIA3Automation aut)
    {
        var modal = Esperar(() =>
            aut.GetDesktop().FindFirstChild(cf => cf.ByName("Impressora").And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window))),
            tentativas: 3);
        if (modal is null) return;

        // botao "Cancelar" do MessageBoxButtons.RetryCancel
        var cancelar = modal.FindFirstDescendant(cf => cf.ByName("Cancelar"));
        if (cancelar is not null) { cancelar.AsButton().Invoke(); return; }
        try { modal.AsWindow().Close(); } catch { }
    }
}
