# ADR-0012 — Valores do enum de pagamento estáveis e aditivos

**Status:** Aceito

## Contexto
`FormaPagamento` é gravado como `int` no SQLite. Reordenar ou remover valores
reinterpretaria vendas antigas (um "Pix" viraria "Cartão"). O dashboard passou a exigir
separar Débito e Crédito, que não existiam no MVP.

## Decisão
O enum é **append-only**: `Dinheiro=0, Pix=1, Cartao=2, CartaoDebito=3, CartaoCredito=4`.
Nunca reordenar/remover. `Cartao=2` permanece como genérico legado; `Caixa.Consolidar`
soma todas as bandeiras em `TotalCartaoCentavos` e ainda expõe débito/crédito separados.

## Consequências
- ✅ Vendas históricas continuam com o significado correto.
- ✅ Novos meios de pagamento entram sem migração.
- ⚠️ A UI de pagamento não oferece mais o "Cartão genérico"; ele existe só para leitura de
  dados antigos.
