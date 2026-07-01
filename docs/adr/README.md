# Architecture Decision Records (ADR)

Registro das decisões de arquitetura relevantes do PDV Festa Junina. Cada ADR descreve
o **contexto**, a **decisão** tomada e as **consequências**. Formato inspirado no modelo
de Michael Nygard.

| # | Decisão | Status |
|---|---|---|
| [0001](0001-dinheiro-em-centavos.md) | Dinheiro em centavos (`int`), nunca ponto flutuante | Aceito |
| [0002](0002-sqlite-wal.md) | SQLite em modo WAL como persistência local | Aceito |
| [0003](0003-escpos-raw.md) | Impressão ESC/POS RAW via spooler (sem PrintDocument) | Aceito |
| [0004](0004-core-desacoplado.md) | Core de domínio desacoplado da UI (WinForms) | Aceito |
| [0005](0005-single-file-zero-deps.md) | Executável único, self-contained, zero dependências externas de UI | Aceito |
| [0006](0006-soft-delete.md) | Soft delete de produtos/categorias (nunca `DELETE`) | Aceito |
| [0007](0007-turnos-de-caixa.md) | Turnos de caixa com vínculo nas vendas | Aceito |
| [0008](0008-categoria-por-nome.md) | Categoria referenciada por nome (não por FK numérica) | Aceito |
| [0009](0009-cupom-por-modo.md) | Cupom com modos (Completo/Ficha) e estilos de linha | Aceito |
| [0010](0010-graficos-gdi.md) | Gráficos do dashboard em GDI+ próprio (sem lib de chart) | Aceito |
| [0011](0011-blindagem-e-logs.md) | Blindagem global de exceções + logs em arquivo | Aceito |
| [0012](0012-formas-pagamento-estaveis.md) | Valores do enum de pagamento estáveis e aditivos | Aceito |
