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
    operador      TEXT NOT NULL DEFAULT '',
    caixa_id      INTEGER NULL
);

CREATE TABLE IF NOT EXISTS caixa (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    abertura    TEXT NOT NULL,
    fechamento  TEXT NULL,
    fundo_cent  INTEGER NOT NULL DEFAULT 0,
    operador    TEXT NOT NULL DEFAULT '',
    status      INTEGER NOT NULL DEFAULT 0   -- 0=Aberto, 1=Fechado
);

CREATE TABLE IF NOT EXISTS caixa_mov (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    caixa_id    INTEGER NOT NULL,
    tipo        INTEGER NOT NULL,            -- 0=Sangria, 1=Suprimento
    valor_cent  INTEGER NOT NULL,
    motivo      TEXT NOT NULL DEFAULT '',
    data_hora   TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_mov_caixa ON caixa_mov(caixa_id);
CREATE INDEX IF NOT EXISTS ix_vendas_caixa ON vendas(caixa_id);

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
);

CREATE TABLE IF NOT EXISTS categorias (
    nome   TEXT PRIMARY KEY,
    ordem  INTEGER NOT NULL DEFAULT 0,
    ativo  INTEGER NOT NULL DEFAULT 1
);";
        cmd.ExecuteNonQuery();

        MigrarColunaCaixaId(conn);
    }

    /// <summary>
    /// Migracao suave: bancos criados antes dos turnos nao tem a coluna vendas.caixa_id.
    /// Adiciona se faltar (SQLite ALTER TABLE ADD COLUMN), preservando o historico.
    /// </summary>
    private static void MigrarColunaCaixaId(SqliteConnection conn)
    {
        bool existe = false;
        using (var check = conn.CreateCommand())
        {
            check.CommandText = "PRAGMA table_info(vendas);";
            using var r = check.ExecuteReader();
            while (r.Read())
                if (string.Equals(r.GetString(1), "caixa_id", StringComparison.OrdinalIgnoreCase))
                    existe = true;
        }
        if (!existe)
        {
            using var alter = conn.CreateCommand();
            alter.CommandText = "ALTER TABLE vendas ADD COLUMN caixa_id INTEGER NULL;";
            alter.ExecuteNonQuery();
        }
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
INSERT INTO vendas (data_hora, total_cent, forma, recebido_cent, troco_cent, operador, caixa_id)
VALUES ($dh, $total, $forma, $rec, $troco, $op, $caixa);
SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("$dh", venda.DataHora.ToString("o"));
            cmd.Parameters.AddWithValue("$total", venda.TotalCentavos);
            cmd.Parameters.AddWithValue("$forma", (int)venda.Forma);
            cmd.Parameters.AddWithValue("$rec", venda.RecebidoCentavos);
            cmd.Parameters.AddWithValue("$troco", venda.TrocoCentavos);
            cmd.Parameters.AddWithValue("$op", venda.Operador);
            cmd.Parameters.AddWithValue("$caixa", (object?)venda.CaixaId ?? DBNull.Value);
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
            cmd.CommandText = "SELECT id, data_hora, total_cent, forma, recebido_cent, troco_cent, operador, caixa_id FROM vendas ORDER BY id;";
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
                    Operador = r.GetString(6),
                    CaixaId = r.IsDBNull(7) ? null : r.GetInt64(7)
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

    // ---------- CRUD DE PRODUTO (parametrizado; sem SQL injection) ----------

    /// <summary>Insere ou atualiza UM produto pelo Id (upsert). Preserva o historico.</summary>
    public void SalvarProduto(Produto p)
    {
        using var conn = Abrir();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO produtos (id, nome, preco_cent, categoria, atalho, ativo, composicao)
VALUES ($id, $nome, $preco, $cat, $atalho, $ativo, $comp)
ON CONFLICT(id) DO UPDATE SET
    nome = excluded.nome, preco_cent = excluded.preco_cent, categoria = excluded.categoria,
    atalho = excluded.atalho, ativo = excluded.ativo, composicao = excluded.composicao;";
        cmd.Parameters.AddWithValue("$id", p.Id);
        cmd.Parameters.AddWithValue("$nome", p.Nome);
        cmd.Parameters.AddWithValue("$preco", p.PrecoCentavos);
        cmd.Parameters.AddWithValue("$cat", p.Categoria);
        cmd.Parameters.AddWithValue("$atalho", p.Atalho);
        cmd.Parameters.AddWithValue("$ativo", p.Ativo ? 1 : 0);
        cmd.Parameters.AddWithValue("$comp", ComboSerializer.Serializar(p.Composicao));
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// SOFT DELETE: apenas marca ativo=0. NUNCA apaga o registro, para nao quebrar o
    /// historico financeiro (vendas antigas continuam referenciando o produto).
    /// </summary>
    public void InativarProduto(string id)
    {
        using var conn = Abrir();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE produtos SET ativo = 0 WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    /// <summary>True se o produto ja aparece em alguma venda (nao pode ser apagado de vez).</summary>
    public bool ProdutoTemVendas(string id)
    {
        using var conn = Abrir();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT EXISTS(SELECT 1 FROM venda_itens WHERE produto_id = $id);";
        cmd.Parameters.AddWithValue("$id", id);
        return Convert.ToInt64(cmd.ExecuteScalar() ?? 0L) == 1;
    }

    /// <summary>True se o Id de produto ja existe no catalogo.</summary>
    public bool ProdutoExiste(string id)
    {
        using var conn = Abrir();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT EXISTS(SELECT 1 FROM produtos WHERE id = $id);";
        cmd.Parameters.AddWithValue("$id", id);
        return Convert.ToInt64(cmd.ExecuteScalar() ?? 0L) == 1;
    }

    // ---------- CATEGORIAS ----------

    /// <summary>Lista categorias ordenadas por Ordem, depois Nome. Ativas por padrao.</summary>
    public List<Categoria> ListarCategorias(bool incluirInativas = false)
    {
        using var conn = Abrir();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = incluirInativas
            ? "SELECT nome, ordem, ativo FROM categorias ORDER BY ordem, nome;"
            : "SELECT nome, ordem, ativo FROM categorias WHERE ativo = 1 ORDER BY ordem, nome;";
        var lista = new List<Categoria>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            lista.Add(new Categoria { Nome = r.GetString(0), Ordem = r.GetInt32(1), Ativo = r.GetInt32(2) == 1 });
        return lista;
    }

    /// <summary>Insere ou atualiza uma categoria (upsert pelo Nome).</summary>
    public void SalvarCategoria(Categoria c)
    {
        using var conn = Abrir();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO categorias (nome, ordem, ativo) VALUES ($n, $o, $a)
ON CONFLICT(nome) DO UPDATE SET ordem = excluded.ordem, ativo = excluded.ativo;";
        cmd.Parameters.AddWithValue("$n", c.Nome);
        cmd.Parameters.AddWithValue("$o", c.Ordem);
        cmd.Parameters.AddWithValue("$a", c.Ativo ? 1 : 0);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Soft delete de categoria (oculta a aba, nao apaga os produtos).</summary>
    public void InativarCategoria(string nome)
    {
        using var conn = Abrir();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE categorias SET ativo = 0 WHERE nome = $n;";
        cmd.Parameters.AddWithValue("$n", nome);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Semeia categorias (na ordem dada) somente se a tabela estiver vazia.</summary>
    public void SemearCategoriasSeVazio(IEnumerable<string> nomesEmOrdem)
    {
        using var conn = Abrir();
        using (var check = conn.CreateCommand())
        {
            check.CommandText = "SELECT COUNT(*) FROM categorias;";
            if (Convert.ToInt64(check.ExecuteScalar() ?? 0L) > 0) return;
        }
        using var tx = conn.BeginTransaction();
        int ordem = 0;
        foreach (var nome in nomesEmOrdem)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT OR IGNORE INTO categorias (nome, ordem, ativo) VALUES ($n, $o, 1);";
            cmd.Parameters.AddWithValue("$n", nome);
            cmd.Parameters.AddWithValue("$o", ordem++);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    // ---------- TURNOS DE CAIXA ----------

    /// <summary>Abre um novo turno e retorna com o Id gerado.</summary>
    public Turno AbrirCaixa(int fundoCentavos, string operador)
    {
        using var conn = Abrir();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO caixa (abertura, fundo_cent, operador, status)
VALUES ($ab, $fundo, $op, 0);
SELECT last_insert_rowid();";
        var abertura = DateTime.Now;
        cmd.Parameters.AddWithValue("$ab", abertura.ToString("o"));
        cmd.Parameters.AddWithValue("$fundo", fundoCentavos);
        cmd.Parameters.AddWithValue("$op", operador);
        var id = (long)(cmd.ExecuteScalar() ?? 0L);
        return new Turno { Id = id, Abertura = abertura, FundoCentavos = fundoCentavos, Operador = operador };
    }

    /// <summary>Fecha o turno (status=1, grava data de fechamento).</summary>
    public void FecharCaixa(long caixaId)
    {
        using var conn = Abrir();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE caixa SET status = 1, fechamento = $f WHERE id = $id;";
        cmd.Parameters.AddWithValue("$f", DateTime.Now.ToString("o"));
        cmd.Parameters.AddWithValue("$id", caixaId);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Retorna o turno ABERTO mais recente, ou null se o caixa esta fechado.</summary>
    public Turno? CaixaAberto()
    {
        using var conn = Abrir();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT id, abertura, fechamento, fundo_cent, operador, status
FROM caixa WHERE status = 0 ORDER BY id DESC LIMIT 1;";
        using var r = cmd.ExecuteReader();
        return r.Read() ? LerTurno(r) : null;
    }

    private static Turno LerTurno(SqliteDataReader r) => new()
    {
        Id = r.GetInt64(0),
        Abertura = DateTime.Parse(r.GetString(1), null, System.Globalization.DateTimeStyles.RoundtripKind),
        Fechamento = r.IsDBNull(2) ? null : DateTime.Parse(r.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind),
        FundoCentavos = r.GetInt32(3),
        Operador = r.GetString(4),
        Status = (StatusCaixa)r.GetInt32(5)
    };

    /// <summary>Registra uma sangria/suprimento no turno.</summary>
    public void RegistrarMovimento(MovimentoCaixa mov)
    {
        using var conn = Abrir();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO caixa_mov (caixa_id, tipo, valor_cent, motivo, data_hora)
VALUES ($cx, $tipo, $valor, $motivo, $dh);";
        cmd.Parameters.AddWithValue("$cx", mov.CaixaId);
        cmd.Parameters.AddWithValue("$tipo", (int)mov.Tipo);
        cmd.Parameters.AddWithValue("$valor", mov.ValorCentavos);
        cmd.Parameters.AddWithValue("$motivo", mov.Motivo);
        cmd.Parameters.AddWithValue("$dh", mov.DataHora.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    /// <summary>Lista os movimentos (sangria/suprimento) de um turno.</summary>
    public List<MovimentoCaixa> ListarMovimentos(long caixaId)
    {
        using var conn = Abrir();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT id, caixa_id, tipo, valor_cent, motivo, data_hora
FROM caixa_mov WHERE caixa_id = $cx ORDER BY id;";
        cmd.Parameters.AddWithValue("$cx", caixaId);
        var lista = new List<MovimentoCaixa>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            lista.Add(new MovimentoCaixa
            {
                Id = r.GetInt64(0),
                CaixaId = r.GetInt64(1),
                Tipo = (TipoMovimento)r.GetInt32(2),
                ValorCentavos = r.GetInt32(3),
                Motivo = r.GetString(4),
                DataHora = DateTime.Parse(r.GetString(5), null, System.Globalization.DateTimeStyles.RoundtripKind)
            });
        return lista;
    }

    /// <summary>Vendas de um turno especifico (para o fechamento Z).</summary>
    public List<Venda> ListarVendasPorCaixa(long caixaId) =>
        ListarVendas().Where(v => v.CaixaId == caixaId).ToList();

    public void Dispose()
    {
        // Conexoes sao abertas/fechadas por operacao; nada global a liberar.
        SqliteConnection.ClearAllPools();
    }
}
