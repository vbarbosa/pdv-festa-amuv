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
| [0013](0013-estorno-audit-trail.md) | Estorno de venda por soft delete (audit trail) | Aceito |
| [0014](0014-motor-de-precos.md) | Motor de preços (combos/promoções auto-detectados) | Aceito |
| [0015](0015-fichas-vales-destacaveis.md) | Fichas de consumo destacáveis (modelo quermesse) | Aceito |
| [0016](0016-navegacao-teclado-categoria-item.md) | Navegação por teclado: categoria → item → Enter | Aceito |
| [0017](0017-impressao-sem-acento-e-robustez.md) | Impressão sem acento + robustez plug-and-play da impressora | Aceito |
| [0018](0018-hardening-ui-e-video-demo.md) | Hardening de UI, testes E2E e vídeo de demonstração | Aceito |
| [0019](0019-troca-operador-batimento.md) | Troca de operador com batimento de caixa opcional | Aceito |
| [0020](0020-permissoes-por-acao.md) | Permissões por ação (admin delega ao operador) | Aceito |
| [0021](0021-crud-excluir-seguro-e-versionamento-cardapio.md) | Excluir seguro no CRUD + versionamento do cardápio | Aceito |
| [0022](0022-qa-sandbox-hyperv.md) | QA em sandbox volátil (Hyper-V) para testes E2E | Aceito |
