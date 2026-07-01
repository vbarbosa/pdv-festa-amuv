using System.Globalization;
using System.Text;

namespace PdvFesta.Core;

/// <summary>
/// Formata o texto do cupom para impressora termica de 58mm (32 colunas).
/// Puro (string in / string out) para ser 100% testavel sem hardware.
/// </summary>
public static class CupomFormatter
{
    public const int LarguraPadrao = 32;

    /// <summary>Formata centavos como "R$ 12,50".</summary>
    public static string Moeda(int centavos)
    {
        var reais = centavos / 100m;
        return "R$ " + reais.ToString("0.00", CultureInfo.GetCultureInfo("pt-BR"));
    }

    /// <summary>Linha de N tracos.</summary>
    public static string Divisoria(int largura = LarguraPadrao) => new('-', largura);

    /// <summary>Centraliza um texto na largura dada.</summary>
    public static string Centralizar(string texto, int largura = LarguraPadrao)
    {
        if (texto.Length >= largura) return texto[..largura];
        int total = largura - texto.Length;
        int esq = total / 2;
        return new string(' ', esq) + texto;
    }

    /// <summary>
    /// Monta "nome ....... preco" ocupando exatamente 'largura' colunas.
    /// Se nome + preco nao couber, trunca o nome.
    /// </summary>
    public static string LinhaItem(string nome, string preco, int largura = LarguraPadrao)
    {
        // espaco minimo de 1 entre nome e preco
        int maxNome = largura - preco.Length - 1;
        if (maxNome < 0) return preco[..Math.Min(preco.Length, largura)];

        if (nome.Length > maxNome)
            nome = nome[..maxNome];

        int pontos = largura - nome.Length - preco.Length;
        if (pontos < 1) pontos = 1;
        return nome + new string('.', pontos) + preco;
    }

    /// <summary>Quebra um texto em varias linhas de ate 'largura' colunas (por palavra).</summary>
    public static List<string> Wrap(string texto, int largura = LarguraPadrao)
    {
        var linhas = new List<string>();
        var palavras = texto.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var atual = new StringBuilder();

        foreach (var p in palavras)
        {
            var palavra = p.Length > largura ? p[..largura] : p;
            if (atual.Length == 0)
            {
                atual.Append(palavra);
            }
            else if (atual.Length + 1 + palavra.Length <= largura)
            {
                atual.Append(' ').Append(palavra);
            }
            else
            {
                linhas.Add(atual.ToString());
                atual.Clear().Append(palavra);
            }
        }
        if (atual.Length > 0) linhas.Add(atual.ToString());
        if (linhas.Count == 0) linhas.Add("");
        return linhas;
    }

    private static readonly string[] NomesForma = { "DINHEIRO", "PIX", "CARTAO" };

    /// <summary>
    /// Monta o cupom completo (lista de linhas de texto, todas &lt;= largura).
    /// A parte grafica/ESC-POS (fonte grande, corte) fica na camada de impressao.
    /// </summary>
    public static List<string> MontarCupom(Venda venda, string titulo, int largura = LarguraPadrao)
    {
        var l = new List<string>();

        l.Add(Centralizar(titulo, largura));
        l.Add(Centralizar("Cupom nao fiscal", largura));
        l.Add(Divisoria(largura));
        l.Add($"Venda #{venda.Id}");
        l.Add($"Data: {venda.DataHora:dd/MM/yyyy HH:mm}");
        if (!string.IsNullOrWhiteSpace(venda.Operador))
            l.Add($"Operador: {venda.Operador}");
        l.Add(Divisoria(largura));

        foreach (var item in venda.Itens)
        {
            // linha 1: nome do item + subtotal
            l.Add(LinhaItem(item.Nome, Moeda(item.SubtotalCentavos), largura));
            // linha 2 (se qtd > 1): detalhe "  qtd x unit"
            if (item.Quantidade > 1)
                l.Add($"  {item.Quantidade} x {Moeda(item.PrecoUnitarioCentavos)}");
        }

        l.Add(Divisoria(largura));
        l.Add(LinhaItem("TOTAL", Moeda(venda.TotalCentavos), largura));
        l.Add(LinhaItem("Pagamento", NomesForma[(int)venda.Forma], largura));

        if (venda.Forma == FormaPagamento.Dinheiro)
        {
            l.Add(LinhaItem("Recebido", Moeda(venda.RecebidoCentavos), largura));
            l.Add(LinhaItem("Troco", Moeda(venda.TrocoCentavos), largura));
        }

        l.Add(Divisoria(largura));
        l.Add(Centralizar("Obrigado! Bom Arraia!", largura));

        return l;
    }
}
