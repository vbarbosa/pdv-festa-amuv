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
    /// <summary>Largura util quando a fonte esta EXPANDIDA (dupla largura): metade das colunas.</summary>
    public const int LarguraExpandida = 16;

    /// <summary>Nome legivel da forma de pagamento para o cupom.</summary>
    public static string NomeForma(FormaPagamento forma) => forma switch
    {
        FormaPagamento.Dinheiro => "DINHEIRO",
        FormaPagamento.Pix => "PIX",
        FormaPagamento.CartaoDebito => "CARTAO DEBITO",
        FormaPagamento.CartaoCredito => "CARTAO CREDITO",
        _ => "CARTAO"
    };

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
        l.Add(LinhaItem("Pagamento", NomeForma(venda.Forma), largura));

        if (venda.Forma == FormaPagamento.Dinheiro)
        {
            l.Add(LinhaItem("Recebido", Moeda(venda.RecebidoCentavos), largura));
            l.Add(LinhaItem("Troco", Moeda(venda.TrocoCentavos), largura));
        }

        l.Add(Divisoria(largura));
        l.Add(Centralizar("Obrigado! Bom Arraia!", largura));

        return l;
    }

    /// <summary>
    /// Monta o ticket de consumo respeitando a <see cref="ConfigCupom"/>:
    ///  - Completo: recibo com valores, total, pagamento e troco.
    ///  - FichaConsumo: so quantidade + nome em fonte EXPANDIDA (16 col), sem valores;
    ///    opcionalmente corta/separa uma ficha por item (barracas diferentes).
    /// Retorna linhas com ESTILO (a camada ESC/POS traduz em bytes).
    /// </summary>
    public static List<LinhaCupom> MontarTicket(Venda venda, ConfigCupom cfg, int largura = LarguraPadrao)
    {
        return cfg.Modo switch
        {
            ModoCupom.FichaConsumo  => MontarFicha(venda, cfg, largura),
            ModoCupom.ReciboComVales => MontarReciboComVales(venda, cfg, largura),
            _ => MontarReciboCompleto(venda, cfg, largura)
        };
    }

    /// <summary>Divisoria DUPLA (===) que separa o recibo de pagamento das fichas.</summary>
    public static string DivisoriaDupla(int largura = LarguraPadrao) => new('=', largura);

    /// <summary>Divisoria PONTILHADA (linha de rasgar) entre um vale e o proximo.</summary>
    public static string DivisoriaPontilhada(int largura = LarguraPadrao) => new('-', largura);

    /// <summary>
    /// MODELO QUERMESSE: recibo gerencial completo + VALES INDIVIDUAIS destacaveis.
    /// Desmembra cada item em N fichas de "1x NOME" (uma por unidade fisica), separadas
    /// por pontilhado, para o cliente rasgar e entregar em barracas/momentos diferentes.
    /// </summary>
    private static List<LinhaCupom> MontarReciboComVales(Venda venda, ConfigCupom cfg, int largura)
    {
        // 1) Recibo gerencial normal (total, pagamento, troco, itens agrupados).
        var l = MontarReciboCompleto(venda, cfg, largura);

        // 2) Divisoria DUPLA separando o recibo das fichas de consumo.
        l.Add(new LinhaCupom(DivisoriaDupla(largura)));
        l.Add(new LinhaCupom(Centralizar("FICHAS DE CONSUMO", largura)));
        l.Add(new LinhaCupom(DivisoriaDupla(largura)));
        l.Add(new LinhaCupom(""));   // margem antes do 1o vale (mesma folga dos demais)

        // 3) Desmembramento: para CADA item de venda (ignorando linhas de desconto de
        //    combo, que tem ProdutoId vazio), imprime 'Quantidade' vales de "1x NOME".
        foreach (var item in venda.Itens)
        {
            if (string.IsNullOrEmpty(item.ProdutoId)) continue;   // linha de desconto -> nao vira vale

            var qtd = Math.Max(0, item.Quantidade);
            for (int n = 0; n < qtd; n++)
            {
                // nome GRANDE (altura dupla, largura normal => cabe mais que a 2x2), sempre
                // "1X" (unidade fisica). Wrap em ~28 col porque a largura da fonte e 1x.
                foreach (var linha in Wrap($"1X {item.Nome.ToUpperInvariant()}", 28))
                    l.Add(new LinhaCupom(linha, EstiloLinha.Expandida));
                l.Add(new LinhaCupom(Centralizar("Vale 1 item", largura)));
                // FOLGA para dobrar/rasgar: 1 linha em branco de cada lado do pontilhado.
                l.Add(new LinhaCupom(""));
                l.Add(new LinhaCupom(DivisoriaPontilhada(largura)));   // linha de rasgar
                l.Add(new LinhaCupom(""));
            }
        }

        // remove as linhas em branco no FIM (o corte ja avanca a bobina) para nao sobrar
        // quase um vale inteiro de papel branco depois do ultimo ticket.
        while (l.Count > 0 && l[^1].Estilo == EstiloLinha.Normal && l[^1].Texto == "")
            l.RemoveAt(l.Count - 1);

        return l;
    }

    private static List<LinhaCupom> MontarReciboCompleto(Venda venda, ConfigCupom cfg, int largura)
    {
        var l = new List<LinhaCupom>();

        if (!string.IsNullOrWhiteSpace(cfg.Evento))
            l.Add(new LinhaCupom(cfg.Evento.Trim(), EstiloLinha.Titulo));
        if (!string.IsNullOrWhiteSpace(cfg.Subtitulo))
            l.Add(new LinhaCupom(Centralizar(cfg.Subtitulo.Trim(), largura)));

        l.Add(new LinhaCupom(Centralizar("Cupom nao fiscal", largura)));
        l.Add(new LinhaCupom(Divisoria(largura)));
        l.Add(new LinhaCupom($"Venda #{venda.Id}"));
        l.Add(new LinhaCupom($"Data: {venda.DataHora:dd/MM/yyyy HH:mm}"));
        if (!string.IsNullOrWhiteSpace(venda.Operador))
            l.Add(new LinhaCupom($"Operador: {venda.Operador}"));
        l.Add(new LinhaCupom(Divisoria(largura)));

        foreach (var item in venda.Itens)
        {
            // linha de DESCONTO de combo/promocao: ProdutoId vazio e subtotal negativo.
            if (string.IsNullOrEmpty(item.ProdutoId) && item.SubtotalCentavos < 0)
            {
                l.Add(new LinhaCupom(LinhaItem("* " + item.Nome, "-" + Moeda(-item.SubtotalCentavos), largura)));
                continue;
            }
            // itens normais: SEMPRE com a quantidade como prefixo ("2x Pipoca .... R$ 10,00").
            l.Add(new LinhaCupom(LinhaItem($"{item.Quantidade}x {item.Nome}", Moeda(item.SubtotalCentavos), largura)));
        }

        l.Add(new LinhaCupom(Divisoria(largura)));
        // Se houve desconto de combo, deixa claro: Subtotal, Descontos e o TOTAL ja com desconto.
        int subtotalCent = venda.Itens.Where(i => i.SubtotalCentavos > 0).Sum(i => i.SubtotalCentavos);
        int descontoCent = venda.Itens.Where(i => i.SubtotalCentavos < 0).Sum(i => -i.SubtotalCentavos);
        if (descontoCent > 0)
        {
            l.Add(new LinhaCupom(LinhaItem("Subtotal", Moeda(subtotalCent), largura)));
            l.Add(new LinhaCupom(LinhaItem("Descontos (combo)", "-" + Moeda(descontoCent), largura)));
        }
        l.Add(new LinhaCupom(LinhaItem("TOTAL", Moeda(venda.TotalCentavos), largura)));
        l.Add(new LinhaCupom(LinhaItem("Pagamento", NomeForma(venda.Forma), largura)));

        if (venda.Forma == FormaPagamento.Dinheiro)
        {
            l.Add(new LinhaCupom(LinhaItem("Recebido", Moeda(venda.RecebidoCentavos), largura)));
            l.Add(new LinhaCupom(LinhaItem("Troco", Moeda(venda.TrocoCentavos), largura)));
        }

        l.Add(new LinhaCupom(Divisoria(largura)));
        if (!string.IsNullOrWhiteSpace(cfg.Rodape))
            foreach (var linha in Wrap(cfg.Rodape.Trim(), largura))
                l.Add(new LinhaCupom(Centralizar(linha, largura)));

        return l;
    }

    private static List<LinhaCupom> MontarFicha(Venda venda, ConfigCupom cfg, int largura)
    {
        var l = new List<LinhaCupom>();

        if (!string.IsNullOrWhiteSpace(cfg.Evento))
            l.Add(new LinhaCupom(cfg.Evento.Trim(), EstiloLinha.Titulo));

        var itens = venda.Itens;
        for (int idx = 0; idx < itens.Count; idx++)
        {
            var item = itens[idx];
            // "2X BOLO DE MILHO" em fonte expandida (16 col) com quebra por palavra.
            var texto = $"{item.Quantidade}X {item.Nome.ToUpperInvariant()}";
            foreach (var linha in Wrap(texto, LarguraExpandida))
                l.Add(new LinhaCupom(linha, EstiloLinha.Expandida));

            bool ultimo = idx == itens.Count - 1;
            if (!ultimo)
            {
                if (cfg.SepararPorItem)
                    l.Add(LinhaCupom.CorteFicha);   // corta -> ficha separada por barraca
                else
                    l.Add(new LinhaCupom(""));       // so um respiro entre itens
            }
        }

        if (!string.IsNullOrWhiteSpace(cfg.Rodape))
        {
            l.Add(new LinhaCupom(Divisoria(largura)));
            foreach (var linha in Wrap(cfg.Rodape.Trim(), largura))
                l.Add(new LinhaCupom(Centralizar(linha, largura)));
        }

        return l;
    }

    /// <summary>
    /// Leitura Z (relatorio de fechamento de turno) para a bobina 58mm:
    /// entradas por forma de pagamento, Total em Gaveta e itens vendidos (auditoria).
    /// </summary>
    public static List<LinhaCupom> MontarFechamentoZ(
        ResumoTurno resumo, IEnumerable<ItemVendido> itens, ConfigCupom cfg, int largura = LarguraPadrao)
    {
        var l = new List<LinhaCupom>();
        var t = resumo.Turno;
        var v = resumo.Vendas;

        l.Add(new LinhaCupom("FECHAMENTO DE CAIXA", EstiloLinha.Titulo));
        l.Add(new LinhaCupom(Centralizar("Leitura Z", largura)));
        if (!string.IsNullOrWhiteSpace(cfg.Evento))
            l.Add(new LinhaCupom(Centralizar(cfg.Evento.Trim(), largura)));
        l.Add(new LinhaCupom(Divisoria(largura)));

        l.Add(new LinhaCupom($"Turno #{t.Id}"));
        l.Add(new LinhaCupom($"Abertura: {t.Abertura:dd/MM HH:mm}"));
        l.Add(new LinhaCupom($"Fechamento: {(t.Fechamento ?? DateTime.Now):dd/MM HH:mm}"));
        if (!string.IsNullOrWhiteSpace(t.Operador))
            l.Add(new LinhaCupom($"Operador: {t.Operador}"));
        l.Add(new LinhaCupom(Divisoria(largura)));

        l.Add(new LinhaCupom("ENTRADAS POR PAGAMENTO"));
        l.Add(new LinhaCupom(LinhaItem("Dinheiro", Moeda(v.TotalDinheiroCentavos), largura)));
        l.Add(new LinhaCupom(LinhaItem("Pix", Moeda(v.TotalPixCentavos), largura)));
        l.Add(new LinhaCupom(LinhaItem("Cartao Debito", Moeda(v.TotalDebitoCentavos), largura)));
        l.Add(new LinhaCupom(LinhaItem("Cartao Credito", Moeda(v.TotalCreditoCentavos), largura)));
        l.Add(new LinhaCupom(Divisoria(largura)));
        l.Add(new LinhaCupom(LinhaItem("VENDAS BRUTAS", Moeda(v.FaturamentoBrutoCentavos), largura)));
        l.Add(new LinhaCupom(LinhaItem("Nº de vendas", v.QuantidadeVendas.ToString(), largura)));
        l.Add(new LinhaCupom(Divisoria(largura)));

        l.Add(new LinhaCupom("CONFERENCIA DA GAVETA"));
        l.Add(new LinhaCupom(LinhaItem("Fundo inicial", Moeda(resumo.FundoCentavos), largura)));
        l.Add(new LinhaCupom(LinhaItem("(+) Dinheiro", Moeda(v.TotalDinheiroCentavos), largura)));
        l.Add(new LinhaCupom(LinhaItem("(+) Suprimentos", Moeda(resumo.SuprimentosCentavos), largura)));
        l.Add(new LinhaCupom(LinhaItem("(-) Sangrias", Moeda(resumo.SangriasCentavos), largura)));
        l.Add(new LinhaCupom(Divisoria(largura)));
        l.Add(new LinhaCupom("TOTAL EM GAVETA", EstiloLinha.Titulo));
        l.Add(new LinhaCupom(Centralizar(Moeda(resumo.TotalGavetaCentavos), largura)));
        l.Add(new LinhaCupom(Divisoria(largura)));

        var lista = itens as ICollection<ItemVendido> ?? itens.ToList();
        if (lista.Count > 0)
        {
            l.Add(new LinhaCupom("ITENS VENDIDOS (auditoria)"));
            foreach (var it in lista)
                l.Add(new LinhaCupom(LinhaItem($"{it.Quantidade}x {it.Nome}", Moeda(it.TotalCentavos), largura)));
            l.Add(new LinhaCupom(Divisoria(largura)));
        }

        l.Add(new LinhaCupom(Centralizar("Confira a gaveta!", largura)));
        l.Add(new LinhaCupom(Centralizar($"{DateTime.Now:dd/MM/yyyy HH:mm}", largura)));

        return l;
    }
}
