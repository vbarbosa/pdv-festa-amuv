# ADR-0021 — Excluir seguro no CRUD + versionamento do cardápio

**Status:** Aceito

## Contexto
Até então os CRUDs só tinham **Inativar** (soft delete). Faltava excluir de vez itens
criados por engano — mas sem quebrar a auditoria (um produto com vendas não pode sumir).
E o cardápio precisava ser portável: salvar uma versão, levar pra outro PC, voltar atrás.

## Decisão
**Excluir seguro (com trava):**
- `Repositorio.ExcluirProduto` / `ExcluirCategoria` fazem DELETE real, mas **lançam** se o
  item tem uso (produto com vendas; categoria com produtos). `ExcluirPromocao` é livre (a
  venda grava a linha de desconto, não a promoção).
- Na UI, botão **"Excluir permanentemente"** com dupla confirmação (e o default do 2º
  diálogo é "Não"). Produto/categoria com uso mostram aviso e só permitem Inativar.

**Versionamento do cardápio:**
- `CardapioLoader.ExportarParaPasta` gera `cardapio_<data-hora>.json` (produtos + categorias
  + título) a partir do banco. `ImportarDeArquivo` substitui o catálogo (não toca em
  vendas/turnos). Botões **Exportar/Importar** em Gerenciar Produtos.

## Consequências
- ✅ Erros de cadastro somem de vez; dados com histórico ficam protegidos (só Inativar).
- ✅ Cardápio portável e reversível; útil pra montar/testar catálogo e replicar entre PCs.
- ⚠️ Importar substitui o catálogo inteiro (comportamento esperado; confirmado na UI).
