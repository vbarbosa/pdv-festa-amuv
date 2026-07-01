# ADR-0002 — SQLite em modo WAL

**Status:** Aceito

## Contexto
O caixa roda em um PC comum, sem servidor de banco, e uma festa pode ter queda de energia.
Precisamos de persistência local, embutível no `.exe`, resistente a corrupção.

## Decisão
Usar **SQLite** (arquivo único) em modo **WAL** (Write-Ahead Logging) com `synchronous=NORMAL`.
O binário nativo (`e_sqlite3`) é embutido no single-file. O banco fica em
`%AppData%\FestaJuninaPDV` (sobrevive a reinstalação); se não for gravável, cai para a
pasta do `.exe` (pen drive).

## Consequências
- ✅ Uma queda de energia no meio de uma gravação **não corrompe** o histórico.
- ✅ Leituras (dashboard) não travam gravações (vendas).
- ✅ Zero instalação de banco.
- ⚠️ Gera arquivos `-wal`/`-shm` ao lado do `.db` (tratados no backup e nos testes).
