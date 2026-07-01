# ADR-0013 — Estorno de venda por soft delete (audit trail)

**Status:** Aceito

## Contexto
Erro humano e desistência de cliente acontecem no caixa ("cancela e passa no cartão").
Apagar a venda do banco quebraria a auditoria e a numeração; e sem excluir a venda dos
totais, a gaveta não bate no fim da noite.

## Decisão
Cancelar uma venda é um **soft delete**: `vendas.status` vai para `Cancelada` (nunca `DELETE`).
- O estorno exige **senha de admin** (operador comum não estorna) e avisa que a devolução
  física (dinheiro/maquininha) é manual.
- `Caixa.Consolidar` e `Caixa.ContarItens` **ignoram** vendas canceladas → dashboard,
  **Total em Gaveta** e **Leitura Z** batem centavo a centavo.
- Correção pré-venda (remover item do carrinho) é imediata, sem registro contábil (a venda
  ainda nem existe).

## Consequências
- ✅ Rastro de auditoria completo (a venda cancelada continua no banco).
- ✅ Conciliação financeira exata no fechamento.
- ✅ Coerente com o [ADR-0006](0006-soft-delete.md) (nunca apagar dados com histórico).
- ⚠️ `vendas.status` migrado via `ALTER TABLE` em bancos antigos.
