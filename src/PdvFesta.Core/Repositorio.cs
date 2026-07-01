using Microsoft.Data.Sqlite;

namespace PdvFesta.Core;

/// <summary>
/// Persistencia em SQLite. Usa modo WAL (Write-Ahead Logging) para tolerar
/// quedas de energia: uma queda no meio de uma gravacao NAO corrompe o historico.
/// Precos sempre em centavos (INTEGER).
/// </summary>
public sealed class Repositorio : IDisposable
{
    private readonly string _connString;

    public Repositorio(string dbPath)
    {
        _connString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    private SqliteConnection Abrir()
    {
        var conn = new SqliteConnection(_connString);
        conn.Open();
        // WAL + synchronous NORMAL: bom equilibrio entre seguranca e velocidade no caixa.
        using (var pragma = conn.CreateCommand())
        {
            pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
            pragma.ExecuteNonQuery();
        }
        return conn;
    }

    public void Inicializar()
    {
        using var conn = Abrir();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS produtos (
    id            TEXT PRIMARY KEY,
    nome          TEXT NOT NULL,
    preco_cent    INTEGER NOT NULL,
    categoria     TEXT NOT NULL DEFAULT 'Geral',
    atalho        INTEGER NOT NULL DEFAULT 0,
    ativo         INTEGER NOT NULL DEFAULT 1,
    composicao    TEXT NOT NULL DEFAULT ''
);

CREATE TABLE IF NOT EXISTS vendas (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    data_hora     TEXT NOT NULL,
    total_cent    INTEGER NOT NULL,
    forma         INTEGER NOT NULL,
    recebido_cent INTEGER NOT NULL DEFAULT 0,
    troco_cent    INTEGER NOT NULL DEFAULT 0,
    operador      TEXT NOT NULL DEFAULT ''
);

CREATE TABLE IF NOT EXISTS venda_itens (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    venda_id      INTEGER NOT NULL,
    produto_id    TEXT NOT NULL,
    nome          TEXT NOT NULL,
    preco_unit_cent INTEGER NOT NULL,
    quantidade    INTEGER NOT NULL,
    FOREIGN KEY (venda_id) REFERENCES vendas(id)
);
CREATE INDEX IF NOT EXISTS ix_itens_venda ON venda_itens(venda_id);

CREATE TABLE IF NOT EXISTS config (
    chave  TEXT PRIMARY KEY,
    valor  TEXT NOT NULL
);";
        cmd.ExecuteNonQuery();
    }

    // ---------- CONFIG (chave/valor) ----------

    /// <summary>Grava uma configuracao (ex: impressora padrao). Sobrescreve se existir.</summary>
    public void SalvarConfig(string chave, string valor)
    {
        using var conn = Abrir();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO config (chave, valor) VALUES ($k, $v)
ON CONFLICT(chave) DO UPDATE SET valor = excluded.valor;";
        cmd.Parameters.AddWithValue("$k", chave);
        cmd.Parameters.AddWithValue("$v", valor);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Le uma configuracao; retorna 'padrao' se nao existir.</summary>
    public string LerConfig(string chave, string padrao)
    {
        using var conn = Abrir();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT valor FROM config WHERE chave = $k;";
        cmd.Parameters.AddWithValue("$k", chave);
        var r = cmd.ExecuteScalar();
        return r?.ToString() ?? padrao;
    }

    /// <summary>Retorna o modo de journal atual (esperado: 'wal').</summary>
    public string ModoJournal()
    {
        using var conn = Abrir();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode;";
        return cmd.ExecuteScalar()?.ToString() ?? "";
    }

    /// <summary>Salva a venda + itens numa transacao (tudo ou nada). Retorna o Id gerado.</summary>
    public long SalvarVenda(Venda venda)
    {
        using var conn = Abrir();
        using var tx = conn.BeginTransaction();

        long vendaId;
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"
INSERT INTO vendas (data_hora, total_cent, forma, recebido_cent, troco_cent, operador)
VALUES ($dh, $total, $forma, $rec, $troco, $op);
SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("$dh", venda.DataHora.ToString("o"));
            cmd.Parameters.AddWithValue("$total", venda.TotalCentavos);
            cmd.Parameters.AddWithValue("$forma", (int)venda.Forma);
            cmd.Parameters.AddWithValue("$rec", venda.RecebidoCentavos);
            cmd.Parameters.AddWithValue("$troco", venda.TrocoCentavos);
            cmd.Parameters.AddWithValue("$op", venda.Operador);
            vendaId = (long)(cmd.ExecuteScalar() ?? 0L);
        }

        foreach (var item in venda.Itens)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
INSERT INTO venda_itens (venda_id, produto_id, nome, preco_unit_cent, quantidade)
VALUES ($vid, $pid, $nome, $preco, $qtd);";
            cmd.Parameters.AddWithValue("$vid", vendaId);
            cmd.Parameters.AddWithValue("$pid", item.ProdutoId);
            cmd.Parameters.AddWithValue("$nome", item.Nome);
            cmd.Parameters.AddWithValue("$preco", item.PrecoUnitarioCentavos);
            cmd.Parameters.AddWithValue("$qtd", item.Quantidade);
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
        venda.Id = vendaId;
        return vendaId;
    }

    public List<Venda> ListarVendas()
    {
        using var conn = Abrir();
        var vendas = new Dictionary<long, Venda>();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT id, data_hora, total_cent, forma, recebido_cent, troco_cent, operador FROM vendas ORDER BY id;";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var v = new Venda
                {
                    Id = r.GetInt64(0),
                    DataHora = DateTime.Parse(r.GetString(1), null, System.Globalization.DateTimeStyles.RoundtripKind),
                    TotalCentavos = r.GetInt32(2),
                    Forma = (FormaPagamento)r.GetInt32(3),
                    RecebidoCentavos = r.GetInt32(4),
                    TrocoCentavos = r.GetInt32(5),
                    Operador = r.GetString(6)
                };
                vendas[v.Id] = v;
            }
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT venda_id, produto_id, nome, preco_unit_cent, quantidade FROM venda_itens ORDER BY id;";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var vid = r.GetInt64(0);
                if (vendas.TryGetValue(vid, out var v))
                {
                    v.Itens.Add(new ItemVenda
                    {
                        ProdutoId = r.GetString(1),
                        Nome = r.GetString(2),
                        PrecoUnitarioCentavos = r.GetInt32(3),
                        Quantidade = r.GetInt32(4)
                    });
                }
            }
        }

        return vendas.Values.ToList();
    }

    // ---------- CATALOGO ----------

    public void SalvarCatalogo(IEnumerable<Produto> produtos)
    {
        using var conn = Abrir();
        using var tx = conn.BeginTransaction();
        using (var del = conn.CreateCommand())
        {
            del.Transaction = tx;
            del.CommandText = "DELETE FROM produtos;";
            del.ExecuteNonQuery();
        }
        foreach (var p in produtos)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
INSERT INTO produtos (id, nome, preco_cent, categoria, atalho, ativo, composicao)
VALUES ($id, $nome, $preco, $cat, $atalho, $ativo, $comp);";
            cmd.Parameters.AddWithValue("$id", p.Id);
            cmd.Parameters.AddWithValue("$nome", p.Nome);
            cmd.Parameters.AddWithValue("$preco", p.PrecoCentavos);
            cmd.Parameters.AddWithValue("$cat", p.Categoria);
            cmd.Parameters.AddWithValue("$atalho", p.Atalho);
            cmd.Parameters.AddWithValue("$ativo", p.Ativo ? 1 : 0);
            cmd.Parameters.AddWithValue("$comp", ComboSerializer.Serializar(p.Composicao));
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public List<Produto> ListarProdutos()
    {
        using var conn = Abrir();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, nome, preco_cent, categoria, atalho, ativo, composicao FROM produtos ORDER BY atalho, nome;";
        var produtos = new List<Produto>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            produtos.Add(new Produto
            {
                Id = r.GetString(0),
                Nome = r.GetString(1),
                PrecoCentavos = r.GetInt32(2),
                Categoria = r.GetString(3),
                Atalho = r.GetInt32(4),
                Ativo = r.GetInt32(5) == 1,
                Composicao = ComboSerializer.Desserializar(r.GetString(6))
            });
        }
        return produtos;
    }

    /// <summary>
    /// Substitui TODAS as vendas pelas fornecidas (usado no restore de backup,
    /// para nao duplicar dados). Preserva os Ids originais.
    /// </summary>
    public void SubstituirVendas(IEnumerable<Venda> vendas)
    {
        using var conn = Abrir();
        using var tx = conn.BeginTransaction();

        foreach (var sql in new[] { "DELETE FROM venda_itens;", "DELETE FROM vendas;" })
        {
            using var del = conn.CreateCommand();
            del.Transaction = tx;
            del.CommandText = sql;
            del.ExecuteNonQuery();
        }

        foreach (var v in vendas)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
INSERT INTO vendas (id, data_hora, total_cent, forma, recebido_cent, troco_cent, operador)
VALUES ($id, $dh, $total, $forma, $rec, $troco, $op);";
                cmd.Parameters.AddWithValue("$id", v.Id);
                cmd.Parameters.AddWithValue("$dh", v.DataHora.ToString("o"));
                cmd.Parameters.AddWithValue("$total", v.TotalCentavos);
                cmd.Parameters.AddWithValue("$forma", (int)v.Forma);
                cmd.Parameters.AddWithValue("$rec", v.RecebidoCentavos);
                cmd.Parameters.AddWithValue("$troco", v.TrocoCentavos);
                cmd.Parameters.AddWithValue("$op", v.Operador);
                cmd.ExecuteNonQuery();
            }
            foreach (var item in v.Itens)
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"
INSERT INTO venda_itens (venda_id, produto_id, nome, preco_unit_cent, quantidade)
VALUES ($vid, $pid, $nome, $preco, $qtd);";
                cmd.Parameters.AddWithValue("$vid", v.Id);
                cmd.Parameters.AddWithValue("$pid", item.ProdutoId);
                cmd.Parameters.AddWithValue("$nome", item.Nome);
                cmd.Parameters.AddWithValue("$preco", item.PrecoUnitarioCentavos);
                cmd.Parameters.AddWithValue("$qtd", item.Quantidade);
                cmd.ExecuteNonQuery();
            }
        }
        tx.Commit();
    }

    public void Dispose()
    {
        // Conexoes sao abertas/fechadas por operacao; nada global a liberar.
        SqliteConnection.ClearAllPools();
    }
}
