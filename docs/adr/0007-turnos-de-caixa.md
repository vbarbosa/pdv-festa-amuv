# ADR-0007 — Turnos de caixa

**Status:** Aceito

## Contexto
Um PDV de verdade tem começo e fim de operação. Sem isso, não há como bater o dinheiro
da gaveta nem separar a contabilidade de sábado e domingo no mesmo banco.

## Decisão
Modelar **turnos** (`caixa`): abertura com **fundo de caixa** (troco inicial), fechamento
com **Leitura Z**. Cada venda recebe `caixa_id`. Movimentos manuais de dinheiro
(**sangria/suprimento**) ficam em `caixa_mov`. Sem caixa aberto, o sistema **não registra
vendas**.

Indicador central: **Total em Gaveta** = `fundo + vendas em dinheiro + suprimentos − sangrias`
(Pix e cartão não entram — não há dinheiro físico na gaveta por eles).

## Consequências
- ✅ Conferência de gaveta objetiva; controle contra furo de caixa.
- ✅ Contabilidade separada por turno no mesmo `.db`.
- ✅ Auditoria de barracas: a Leitura Z lista itens vendidos (qtd + valor).
- ⚠️ `vendas.caixa_id` é nullable (vendas legadas anteriores aos turnos).
