using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.UIA3;
using PdvFesta.Core;
using Xunit;

namespace PdvFesta.E2E;

/// <summary>
/// E2E do CRUD de catalogo: abre o app, entra em Configuracoes -> Gerenciar Produtos
/// (com senha de admin), cadastra "Produto Teste 99", salva, fecha e valida que o
/// produto foi persistido e que o botao aparece na tela de vendas (refresh dinamico).
/// </summary>
[Collection("e2e")]
public sealed class CrudProdutoE2ETests : E2ETestBase
{
    private readonly string _dbPath;

    public CrudProdutoE2ETests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"e2ecrud_{Guid.NewGuid():N}.db");
    }

    public override void Dispose()
    {
        base.Dispose();   // mata o app e qualquer orfao
        foreach (var ext in new[] { "", "-wal", "-shm" })
            if (File.Exists(_dbPath + ext)) { try { File.Delete(_dbPath + ext); } catch { } }
    }

    [Fact(Skip = SkipUI)]
    public void CadastrarProduto_ApareceNaTelaENoBanco()
    {
        Assert.True(File.Exists(ExePath), $"Compile a App primeiro. Nao achei: {ExePath}");
        var psi = new ProcessStartInfo(ExePath) { UseShellExecute = false };
        psi.EnvironmentVariables["PDVFESTA_DB"] = _dbPath;
        Proc = Process.Start(psi)!;

        using var automation = new UIA3Automation();
        var app = FlaUI.Core.Application.Attach(Proc);
        var janela = RetentarObterJanela(app, automation);
        Assert.NotNull(janela);

        // abre o caixa (troco 0)
        RetentarAchar(janela!, "btnAbrirCaixa")!.AsButton().Invoke();
        System.Threading.Thread.Sleep(500);

        // menu Configuracoes -> Gerenciar Produtos... (o menu tem acento: "Configurações")
        var barra = janela!.FindFirstChild(cf => cf.ByControlType(ControlType.MenuBar));
        var itemConfig = barra.FindAllChildren()
            .FirstOrDefault(i => i.Name?.Replace("&", "").StartsWith("Config") == true);
        Assert.NotNull(itemConfig);
        var mConfig = itemConfig!.AsMenuItem();

        // Expande e ESPERA os subitens carregarem (o dropdown pode demorar a popular).
        FlaUI.Core.AutomationElements.AutomationElement? mProd = null;
        for (int i = 0; i < 15 && mProd is null; i++)
        {
            mConfig.Expand();
            System.Threading.Thread.Sleep(300);
            mProd = mConfig.Items.FirstOrDefault(x => x.Name?.StartsWith("Gerenciar Produtos") == true);
        }
        Assert.NotNull(mProd);
        mProd!.AsMenuItem().Invoke();
        System.Threading.Thread.Sleep(600);

        // senha de admin (0000)
        var senha = RetentarAcharDesktop(automation, "txtSenhaAdmin");
        Assert.NotNull(senha);
        senha!.AsTextBox().Enter("0000");
        System.Threading.Thread.Sleep(200);
        RetentarAcharDesktop(automation, "btnConfirmarSenha")!.AsButton().Invoke();
        System.Threading.Thread.Sleep(600);

        // formulario de produtos: preenche e salva
        var nome = RetentarAcharDesktop(automation, "txtNomeProduto");
        Assert.NotNull(nome);
        nome!.AsTextBox().Enter("Produto Teste 99");
        var preco = RetentarAcharDesktop(automation, "txtPrecoProduto")!.AsTextBox();
        preco.Text = "";
        preco.Enter("9,90");
        System.Threading.Thread.Sleep(200);
        RetentarAcharDesktop(automation, "btnSalvarProduto")!.AsButton().Invoke();
        System.Threading.Thread.Sleep(500);

        // fecha a janela de produtos (modal) pelo botao de fechar
        var janProd = RetentarJanelaPorTitulo(automation, "Gerenciar Produtos");
        janProd?.Close();
        System.Threading.Thread.Sleep(700);

        // valida no BANCO (fonte da verdade)
        // (o app ainda esta com o arquivo aberto em WAL; lemos assim mesmo p/ conferir)
        // seleciona a aba "Geral" e procura o botao renderizado na tela de vendas
        var abas = janela.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tab))?.AsTab();
        if (abas is not null)
        {
            // a 1a aba "Todos" mostra todos os produtos agrupados -> acha qualquer novo item.
            var todos = abas.TabItems.FirstOrDefault(t => t.Name?.StartsWith("Todos") == true);
            todos?.Select();
            System.Threading.Thread.Sleep(400);
        }
        // na aba "Todos" o botao usa o prefixo btnProdutoTodos_
        var botao = RetentarAchar(janela, "btnProdutoTodos_produto_teste_99")
                    ?? RetentarAchar(janela, "btnProduto_produto_teste_99");
        Assert.True(botao is not null, "O botao do novo produto nao apareceu na tela de vendas.");

        // encerra e confere persistencia
        try { if (!Proc.HasExited) Proc.Kill(true); } catch { }
        System.Threading.Thread.Sleep(500);
        using var repo = new Repositorio(_dbPath);
        repo.Inicializar();
        var prod = repo.ListarProdutos().FirstOrDefault(p => p.Nome == "Produto Teste 99");
        Assert.NotNull(prod);
        Assert.Equal(990, prod!.PrecoCentavos);
        Assert.True(prod.Ativo);
    }

    // ---- helpers ----
    private static Window? RetentarObterJanela(FlaUI.Core.Application app, UIA3Automation aut)
    {
        for (int i = 0; i < 30; i++)
        {
            var w = app.GetMainWindow(aut, TimeSpan.FromSeconds(1));
            if (w is not null) return w;
            System.Threading.Thread.Sleep(500);
        }
        return null;
    }

    private static AutomationElement? RetentarAchar(AutomationElement raiz, string automationId)
    {
        for (int i = 0; i < 20; i++)
        {
            var el = raiz.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
            if (el is not null) return el;
            System.Threading.Thread.Sleep(400);
        }
        return null;
    }

    private static AutomationElement? RetentarAcharDesktop(UIA3Automation aut, string automationId)
    {
        for (int i = 0; i < 20; i++)
        {
            var el = aut.GetDesktop().FindFirstDescendant(cf => cf.ByAutomationId(automationId));
            if (el is not null) return el;
            System.Threading.Thread.Sleep(400);
        }
        return null;
    }

    private static Window? RetentarJanelaPorTitulo(UIA3Automation aut, string titulo)
    {
        for (int i = 0; i < 15; i++)
        {
            var el = aut.GetDesktop().FindFirstDescendant(cf => cf.ByName(titulo));
            if (el is not null) return el.AsWindow();
            System.Threading.Thread.Sleep(300);
        }
        return null;
    }
}
