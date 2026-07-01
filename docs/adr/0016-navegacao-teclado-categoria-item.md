# ADR-0016 — Navegação por teclado: categoria → item → Enter

**Status:** Aceito

## Contexto
O atalho antigo era um campo `Atalho` (1-9) por produto: só **9 produtos** no sistema
inteiro podiam ter atalho — os demais (água, doces, jogos, combos) ficavam **sem nenhum**.
O operador precisava clicar. Todo item deveria ser operável por teclado, e produtos novos
deveriam ganhar atalho sozinhos.

## Decisão
- Atalho **derivado da posição**, não mais cadastrado: cada categoria recebe uma **letra**
  (a inicial livre; se colidir, a próxima letra A-Z livre), e cada item um **número** pela
  sua posição na categoria. Badge no botão mostra `C2`, `B1`, etc.
- Sequência: **Letra** (abre a aba e entra em modo seleção) → **Número** (destaca o item)
  → **Enter** (adiciona). `Esc` sai do modo ou limpa; `Delete/Backspace` remove item.
- O **atalho global 1-9 foi removido** — ele competia com o número-do-item e causava
  adicionar o produto errado. Fora do modo de seleção, número não faz nada (à prova de erro).
- Mapas reconstruídos em `RecarregarProdutos` → **produto/categoria novos ganham atalho
  automaticamente**, sem configuração manual.
- Ordenação dos produtos por **nome** (`ORDER BY nome`), previsível — o campo `atalho`
  legado deixou de ordenar (gerava ordem estranha, ex: combo de R$10 antes da cartela).

## Consequências
- ✅ Todo item operável por teclado; nada de "só 9 com atalho".
- ✅ Plug-and-play de catálogo: cadastrou, já tem atalho e badge.
- ⚠️ Testes E2E de UI que dependem de foco de janela ficam frágeis em máquina
  compartilhada (ver ADR-0018); a lógica é coberta por testes unitários.
