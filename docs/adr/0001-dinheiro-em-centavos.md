# ADR-0001 — Dinheiro em centavos (`int`)

**Status:** Aceito

## Contexto
Valores monetários com `double`/`float` acumulam erro de arredondamento (ex: `0.1 + 0.2`).
Num caixa, um centavo perdido por venda vira furo de caixa ao fim da noite.

## Decisão
Todo valor monetário é armazenado e calculado em **centavos como `int`**. Formatação para
exibição (`R$ 12,50`) e parse de entrada ficam isolados em `CupomFormatter.Moeda` e na
classe `Dinheiro` (App).

## Consequências
- ✅ Aritmética exata; troco e consolidação sempre batem.
- ✅ Colunas SQLite `*_cent INTEGER`.
- ⚠️ Toda entrada de texto passa por `Dinheiro.ParseCentavos` (blindado contra vírgula/ponto/`R$`).
