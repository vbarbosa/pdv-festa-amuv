# ADR-0006 — Soft delete de produtos e categorias

**Status:** Aceito

## Contexto
Apagar (`DELETE`) um produto que já foi vendido quebra o histórico financeiro e os
relatórios (a venda antiga referencia um produto inexistente). No dia da festa, itens
acabam e precisam sumir da tela rapidamente.

## Decisão
Nunca apagar. Produtos e categorias têm coluna **`ativo`**. "Excluir" na interface faz
`ativo = 0` (soft delete): o item some do caixa, mas o histórico continua íntegro.
`Repositorio.ProdutoTemVendas` permite decidir com segurança.

## Consequências
- ✅ Fechamento de caixa e Leitura Z sempre consistentes.
- ✅ Reativar é trivial (voltar `ativo = 1`).
- ✅ Vendas de sábado permanecem mesmo que o item seja inativado no domingo.
