# ADR-0014 — Motor de preços (combos/promoções auto-detectados)

**Status:** Aceito

## Contexto
Combos não deveriam depender de um "botão de combo" — o operador aperta os itens na correria
e o sistema deve reconhecer a intenção. E promoções têm janela de horário (ex: "até 20h").

## Decisão
- **`PricingEngine`** (Core, puro): dado o carrinho + promoções ativas + hora, devolve os
  descontos aplicáveis, **um por conjunto completo** encontrado.
- **UX rastreável**: o carrinho recebe uma **linha de desconto** (verde) — os itens originais
  **não são substituídos**, para o operador conferir/remover. Na venda, o desconto é "assado"
  como uma linha de item com `ProdutoId` vazio e subtotal **negativo** (persiste, imprime,
  entra no total; `ContarItens` a ignora).
- **Persistência**: tabelas `promocoes` + `promocao_itens`; horários como texto `HH:mm`;
  soft delete via `ativo`.
- **Seed**: os combos do cartaz ("até 20h") vêm no `cardapio.json`.

## Consequências
- ✅ Combos automáticos e auditáveis; economia real sem confundir o caixa.
- ✅ Regras testáveis (conjuntos, horário, ativo) sem UI nem banco.
- ⚠️ Descontos que se sobrepõem (mesmo item em duas promos) são aplicados de forma
  independente (simples e suficiente para o evento).
