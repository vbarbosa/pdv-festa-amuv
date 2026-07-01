# ADR-0020 — Permissões por ação (admin delega ao operador)

**Status:** Aceito

## Contexto
A senha de admin protegia uma lista FIXA de ações críticas. Mas cada festa tem uma
organização diferente: às vezes o operador pode mexer em produtos sozinho, às vezes não.
O admin precisava poder **delegar** — decidir quais ações o operador faz sem pedir a senha.

## Decisão
- **`AcaoProtegida`** (enum) lista as ações protegíveis (abrir/fechar caixa, sangria,
  produtos, categorias, promoções, estorno, backup, impressora, layout, permissões).
- **`Permissoes`**: para cada ação, se **exige senha** ou está **liberada**. Persistido no
  banco (`perm_<acao>`), com defaults sensatos (dinheiro/dados/exclusão exigem; config leve
  libera). A tela de permissões **sempre** exige senha (não pode ser desligada).
- **`Dialogos.LiberarAcao(owner, servico, acao)`** centraliza a decisão: se a ação exige
  senha, pede; senão passa direto. As telas só declaram QUAL ação fazem.
- **`FormPermissoes`**: uma tela (com senha) onde o admin marca o que exige senha e troca
  a senha de administrador (atual + nova + confirmação).

## Consequências
- ✅ O admin delega sem tocar em código; a festa configura o rigor que quer.
- ✅ Modelo simples (sem cadastro de usuários): "admin" (com senha) vs "operador" (sem).
- ⚠️ Não há papéis múltiplos nem login por usuário — decisão consciente de escopo para o
  evento (menos atrito no caixa).
