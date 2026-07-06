using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace PdvFesta.Core;

/// <summary>Formato de arquivo do export robusto (o operador escolhe).</summary>
public enum FormatoExport
{
    /// <summary>Um unico .csv com todas as secoes empilhadas (separadas por titulo).</summary>
    CsvUnico,
    /// <summary>Um .csv por secao, numa pasta (resumo.csv, vendas.csv, itens.csv, precos.csv).</summary>
    CsvMultiplos,
    /// <summary>Uma planilha .xlsx com TODAS as secoes numa unica aba (empilhadas).</summary>
    XlsxUnico,
    /// <summary>Uma planilha .xlsx com uma ABA por secao (Resumo | Vendas | Itens | Precos).</summary>
    XlsxAbas
}

/// <summary>
/// Grava as <see cref="TabelaExport"/> em arquivo, no formato escolhido. CSV e nativo; o XLSX
/// e gerado SEM dependencia externa (um .xlsx e um .zip de XMLs no padrao OpenXML minimo).
/// Retorna o(s) caminho(s) gerado(s).
/// </summary>
public static class ExportadorArquivos
{
    // -------------------------------------------------------------- CSV
    private static string Sep(CultureInfo c) { var s = c.TextInfo.ListSeparator; return string.IsNullOrEmpty(s) ? ";" : s; }

    private static string Campo(string s, string sep) =>
        (s.Contains(sep) || s.Contains('"') || s.Contains('\n') || s.Contains('\r'))
            ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;

    private static string TabelaParaCsv(TabelaExport t, string sep)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(sep, t.Cabecalho.Select(x => Campo(x, sep))));
        foreach (var linha in t.Linhas)
            sb.AppendLine(string.Join(sep, linha.Select(x => Campo(x ?? "", sep))));
        return sb.ToString();
    }

    /// <summary>
    /// Grava as tabelas. destino: para CsvUnico/Xlsx* e um ARQUIVO; para CsvMultiplos e a PASTA.
    /// prefixo compoe os nomes. Retorna os caminhos gerados.
    /// </summary>
    public static List<string> Gravar(IReadOnlyList<TabelaExport> tabelas, FormatoExport formato,
        string destino, string prefixo, CultureInfo? cultura = null)
    {
        var c = cultura ?? CultureInfo.CurrentCulture;
        var bom = new UTF8Encoding(true);   // BOM para o Excel abrir com acento certo
        var gerados = new List<string>();

        switch (formato)
        {
            case FormatoExport.CsvUnico:
            {
                var sep = Sep(c);
                var sb = new StringBuilder();
                foreach (var t in tabelas)
                {
                    sb.AppendLine($"===== {t.Nome.ToUpperInvariant()} =====");
                    sb.Append(TabelaParaCsv(t, sep));
                    sb.AppendLine();
                }
                var caminho = GarantirExtensao(destino, ".csv");
                File.WriteAllText(caminho, sb.ToString(), bom);
                gerados.Add(caminho);
                break;
            }
            case FormatoExport.CsvMultiplos:
            {
                Directory.CreateDirectory(destino);
                var sep = Sep(c);
                foreach (var t in tabelas)
                {
                    var nome = $"{prefixo}_{Slug(t.Nome)}.csv";
                    var caminho = Path.Combine(destino, nome);
                    File.WriteAllText(caminho, TabelaParaCsv(t, sep), bom);
                    gerados.Add(caminho);
                }
                break;
            }
            case FormatoExport.XlsxUnico:
            {
                var caminho = GarantirExtensao(destino, ".xlsx");
                // uma aba so, com as tabelas empilhadas (linha em branco + titulo entre elas)
                var unica = EmpilharNumaTabela(tabelas);
                EscreverXlsx(caminho, new[] { unica });
                gerados.Add(caminho);
                break;
            }
            case FormatoExport.XlsxAbas:
            {
                var caminho = GarantirExtensao(destino, ".xlsx");
                EscreverXlsx(caminho, tabelas);
                gerados.Add(caminho);
                break;
            }
        }
        return gerados;
    }

    private static TabelaExport EmpilharNumaTabela(IReadOnlyList<TabelaExport> tabelas)
    {
        // aba unica: repete o padrao "TITULO / cabecalho / linhas / (branco)".
        var larg = tabelas.Max(t => Math.Max(t.Cabecalho.Count, t.Linhas.Count == 0 ? 0 : t.Linhas.Max(l => l.Length)));
        var u = new TabelaExport("Relatorio");
        for (int i = 0; i < larg; i++) u.Cabecalho.Add(i == 0 ? "Relatório" : "");
        foreach (var t in tabelas)
        {
            u.Add(Pad(new[] { t.Nome.ToUpperInvariant() }, larg));
            u.Add(Pad(t.Cabecalho.ToArray(), larg));
            foreach (var l in t.Linhas) u.Add(Pad(l, larg));
            u.Add(Pad(Array.Empty<string>(), larg));   // linha em branco
        }
        return u;
    }

    private static string[] Pad(string[] src, int larg)
    {
        var r = new string[larg];
        for (int i = 0; i < larg; i++) r[i] = i < src.Length ? src[i] : "";
        return r;
    }

    private static string Slug(string s) =>
        new string(s.ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray())
            .Trim('_').Replace("__", "_");

    private static string GarantirExtensao(string caminho, string ext) =>
        caminho.EndsWith(ext, StringComparison.OrdinalIgnoreCase) ? caminho : caminho + ext;

    // -------------------------------------------------------------- XLSX (OpenXML minimo, sem lib)
    // Um .xlsx e um .zip com: [Content_Types].xml, _rels/.rels, xl/workbook.xml,
    // xl/_rels/workbook.xml.rels, xl/worksheets/sheetN.xml. Tudo como texto inline (t="inlineStr")
    // para nao precisar de sharedStrings — simples e robusto para relatorios.
    private static void EscreverXlsx(string caminho, IReadOnlyList<TabelaExport> abas)
    {
        if (File.Exists(caminho)) File.Delete(caminho);
        using var zip = ZipFile.Open(caminho, ZipArchiveMode.Create);

        Escrever(zip, "[Content_Types].xml", ContentTypes(abas.Count));
        Escrever(zip, "_rels/.rels", Rels());
        Escrever(zip, "xl/workbook.xml", Workbook(abas));
        Escrever(zip, "xl/_rels/workbook.xml.rels", WorkbookRels(abas.Count));
        for (int i = 0; i < abas.Count; i++)
            Escrever(zip, $"xl/worksheets/sheet{i + 1}.xml", Worksheet(abas[i]));
    }

    private static void Escrever(ZipArchive zip, string nome, string conteudo)
    {
        var e = zip.CreateEntry(nome, CompressionLevel.Optimal);
        using var s = new StreamWriter(e.Open(), new UTF8Encoding(false));
        s.Write(conteudo);
    }

    private static string Esc(string s) => System.Security.SecurityElement.Escape(s ?? "") ?? "";

    private static string ContentTypes(int nAbas)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
        sb.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
        sb.Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
        sb.Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
        for (int i = 1; i <= nAbas; i++)
            sb.Append($"<Override PartName=\"/xl/worksheets/sheet{i}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
        sb.Append("</Types>");
        return sb.ToString();
    }

    private static string Rels() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
        "</Relationships>";

    private static string Workbook(IReadOnlyList<TabelaExport> abas)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>");
        for (int i = 0; i < abas.Count; i++)
            sb.Append($"<sheet name=\"{Esc(NomeAba(abas[i].Nome, i))}\" sheetId=\"{i + 1}\" r:id=\"rId{i + 1}\"/>");
        sb.Append("</sheets></workbook>");
        return sb.ToString();
    }

    private static string NomeAba(string nome, int idx)
    {
        // Excel: nome de aba <= 31 chars e sem : \ / ? * [ ]
        var limpo = new string((nome ?? $"Aba{idx + 1}").Where(ch => ch is not (':' or '\\' or '/' or '?' or '*' or '[' or ']')).ToArray());
        return limpo.Length > 31 ? limpo[..31] : limpo;
    }

    private static string WorkbookRels(int nAbas)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
        for (int i = 1; i <= nAbas; i++)
            sb.Append($"<Relationship Id=\"rId{i}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i}.xml\"/>");
        sb.Append("</Relationships>");
        return sb.ToString();
    }

    private static string Worksheet(TabelaExport t)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
        int row = 1;
        if (t.Cabecalho.Count > 0) { sb.Append(LinhaXml(t.Cabecalho.ToArray(), row++)); }
        foreach (var l in t.Linhas) sb.Append(LinhaXml(l, row++));
        sb.Append("</sheetData></worksheet>");
        return sb.ToString();
    }

    private static string LinhaXml(string[] celulas, int row)
    {
        var sb = new StringBuilder();
        sb.Append($"<row r=\"{row}\">");
        for (int col = 0; col < celulas.Length; col++)
        {
            var texto = celulas[col] ?? "";
            var refCel = $"{Coluna(col)}{row}";
            // numero? escreve como numerico (facilita somas no Excel). Senao, string inline.
            if (double.TryParse(texto, NumberStyles.Any, CultureInfo.InvariantCulture, out _)
                && texto.All(ch => char.IsDigit(ch) || ch is '.' or '-' or '+'))
                sb.Append($"<c r=\"{refCel}\"><v>{texto}</v></c>");
            else if (double.TryParse(texto.Replace(".", "").Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var num)
                     && (texto.Contains(',') && texto.All(ch => char.IsDigit(ch) || ch is ',' or '.' or '-')))
                sb.Append($"<c r=\"{refCel}\"><v>{num.ToString(CultureInfo.InvariantCulture)}</v></c>");
            else
                sb.Append($"<c r=\"{refCel}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{Esc(texto)}</t></is></c>");
        }
        sb.Append("</row>");
        return sb.ToString();
    }

    private static string Coluna(int idx)
    {
        // 0->A, 25->Z, 26->AA...
        var sb = new StringBuilder();
        idx++;
        while (idx > 0) { idx--; sb.Insert(0, (char)('A' + idx % 26)); idx /= 26; }
        return sb.ToString();
    }
}
