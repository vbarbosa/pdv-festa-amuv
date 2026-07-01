using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using PdvFesta.Core;
using Xunit;

namespace PdvFesta.E2E;

[Collection("e2e")]
public sealed class TourCompletoE2ETests : E2ETestBase
{
    private readonly string _dbPath;

    public TourCompletoE2ETests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"tour_{Guid.NewGuid():N}.db");
    }

    public override void Dispose()
    {
        base.Dispose();
        foreach (var ext in new[] { "", "-wal", "-shm" })
            if (File.Exists(_dbPath + ext)) { try { File.Delete(_dbPath + ext); } catch { } }
    }

    [Fact]
    public void OperacaoDePico_VariasVendasRapidas_ETodasAsTelas()
    {
        if (!EmSandbox) return;

        using var automation = new UIA3Automation();
        var janela = SubirApp(automation);
        AbrirCaixaComFundo(automation, "100,00");
        Evidencia("01-caixa-aberto");

        // ---- OPERACAO DE PICO: varias vendas rapidas por teclado ----
        for (int venda = 0; venda < 3; venda++)
        {
            janela.Focus();
            Thread.Sleep(400);
            // categoria + item + Enter, repetido (uso intensivo dos atalhos)
            DigitarSequencia("b", VirtualKeyShort.ENTER);      // 1a Bebida
            DigitarSequencia("c", VirtualKeyShort.ENTER);      // 1a Comida
            Thread.Sleep(300);
            Keyboard.Press(VirtualKeyShort.F2);                // pagar
            Thread.Sleep(600);
            var txt = RetentarAcharDesktop(automation, "txtRecebido");
            if (txt is not null)
            {
                txt.AsTextBox().Enter("50,00");
                Thread.Sleep(200);
                Keyboard.Type(VirtualKeyShort.ENTER);
                Thread.Sleep(700);
            }
            Evidencia($"02-venda-{venda + 1}");
        }

        // ---- ESC limpa carrinho (comeca uma venda e cancela) ----
        janela.Focus(); Thread.Sleep(300);
        DigitarSequencia("b", VirtualKeyShort.ENTER);
        Thread.Sleep(300);
        Keyboard.Press(VirtualKeyShort.ESCAPE);
        Thread.Sleep(300);
        Evidencia("03-carrinho-limpo");

        // ---- Painel em tempo real (F4) ----
        janela.Focus();
        Keyboard.Press(VirtualKeyShort.F4);
        Thread.Sleep(1500);
        Evidencia("04-dashboard-tempo-real");
        FecharModalPorTitulo(automation, "Painel em Tempo Real");

        // ---- Historico de vendas (F3) ----
        janela.Focus();
        Keyboard.Press(VirtualKeyShort.F3);
        Thread.Sleep(1200);
        Evidencia("05-historico");
        FecharModalPorTitulo(automation, "Histórico de Vendas do Turno");

        // fecha o app e valida no banco: 3 vendas registradas
        try { if (!Proc!.HasExited) Proc.Kill(true); } catch { }
        Thread.Sleep(500);

        using var repo = new Repositorio(_dbPath);
        repo.Inicializar();
        var vendas = repo.ListarVendas();
        Assert.True(vendas.Count >= 3, $"esperava >= 3 vendas, veio {vendas.Count}");
        Assert.All(vendas, v => Assert.True(v.TotalCentavos > 0));
    }

    // ---------- helpers ----------
    private Window SubirApp(UIA3Automation automation)
    {
        Assert.True(File.Exists(ExePath), $"Compile a App primeiro. Nao achei: {ExePath}");
        var psi = new ProcessStartInfo(ExePath) { UseShellExecute = false };
        psi.EnvironmentVariables["PDVFESTA_DB"] = _dbPath;
        Proc = Process.Start(psi)!;
        var app = FlaUI.Core.Application.Attach(Proc);
        Window? janela = null;
        for (int i = 0; i < 30 && janela is null; i++) { janela = app.GetMainWindow(automation, TimeSpan.FromSeconds(1)); if (janela is null) Thread.Sleep(500); }
        Assert.NotNull(janela);
        return janela!;
    }

    private void AbrirCaixaComFundo(UIA3Automation automation, string fundo)
    {
        var txt = RetentarAcharDesktop(automation, "txtFundo");
        if (txt is not null) { txt.AsTextBox().Enter(fundo); Thread.Sleep(200); }
        RetentarAcharDesktop(automation, "btnAbrirCaixa")?.AsButton().Invoke();
        Thread.Sleep(600);
    }

    private static void DigitarSequencia(string letra, VirtualKeyShort tecla)
    {
        Keyboard.Type(letra); Thread.Sleep(350);
        Keyboard.Press(tecla); Thread.Sleep(350);
    }

    private static void FecharModalPorTitulo(UIA3Automation aut, string titulo)
    {
        for (int i = 0; i < 10; i++)
        {
            var el = aut.GetDesktop().FindFirstDescendant(cf => cf.ByName(titulo));
            if (el is not null) { try { el.AsWindow().Close(); } catch { } return; }
            Thread.Sleep(200);
        }
        Keyboard.Press(VirtualKeyShort.ESCAPE);
    }

    private static AutomationElement? RetentarAcharDesktop(UIA3Automation aut, string automationId)
    {
        for (int i = 0; i < 15; i++)
        {
            var el = aut.GetDesktop().FindFirstDescendant(cf => cf.ByAutomationId(automationId));
            if (el is not null) return el;
            Thread.Sleep(300);
        }
        return null;
    }
}
