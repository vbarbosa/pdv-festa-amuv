# ADR-0008 — Categoria referenciada por nome

**Status:** Aceito

## Contexto
As categorias organizam o cardápio em abas (Comidas, Doces, Bebidas...). O produto já
carregava a categoria como texto desde o MVP. Introduzir uma FK numérica exigiria migrar
todos os produtos e as vendas existentes.

## Decisão
Manter `produtos.categoria` como **texto** que casa com `categorias.nome` (chave da tabela
`categorias`). A tabela de categorias existe para controlar **ordem de exibição** e
**ativo/inativo**, não para uma FK numérica. Categoria nova digitada no cadastro de produto
é criada automaticamente (`Servico.GarantirCategoria`).

## Consequências
- ✅ Migração suave: nenhum produto/venda precisou mudar de schema.
- ✅ Relacional o suficiente (join por nome) para ordenar e ocultar abas.
- ⚠️ Renomear uma categoria não renomeia em cascata os produtos antigos (decisão consciente:
  o texto na venda é um snapshot histórico).
