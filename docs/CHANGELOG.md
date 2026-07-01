# Changelog

Formato baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/).
Versionamento [SemVer](https://semver.org/lang/pt-BR/).

## [2.0.0] — Refatoração PDV profissional

### Adicionado
- **Turnos de caixa**: abertura com fundo, sangria/suprimento, fechamento com **Leitura Z**.
- **Total em Gaveta** e **dashboard** de fechamento com **gráficos** (GDI+), export **CSV**.
- **CRUD de Produtos** (soft delete) e **CRUD de Categorias** (ordem/ativo).
- **Customizar Atalhos** (teclas 1–9 → produtos, dinâmico).
- **Layout do Cupom** com **preview ao vivo**; modo **Ficha de Consumo** (fonte expandida,
  corte por item) além do **Recibo Completo**.
- Pagamento com **Débito/Crédito** separados, **valores rápidos** e pré-preenchimento.
- **Senha de administrador** nas telas sensíveis; janelas modais singleton com Dispose.
- **Blindagem global** de exceções + **sistema de logs** em `%AppData%\...\logs`.
- Tela principal reconstruída: **MenuStrip**, **StatusStrip**, **SplitContainer** (carrinho
  fixo ≥420px), **DataGridView**, **TabControl** por categoria + aba **"Todos"**.
- Documentação: arquitetura, ADRs, funcionalidades, diagnóstico, manual.

### Alterado
- `FormaPagamento` agora inclui `CartaoDebito`/`CartaoCredito` (aditivo, valores estáveis).
- Impressão migrada para `ConfigCupom`/`LinhaCupom` (estilos por linha) e constantes ESC/POS nomeadas.
- `EscPosPrinter.EnviarRaw` blindado: falha de hardware nunca lança.

### Corrigido
- Crash de `SplitContainer` no startup (min sizes aplicados antes da largura real).
- Cabeçalho/coluna do carrinho cortados na borda (grid em host com margem + estilo de header).

### Migração
- `vendas.caixa_id` adicionado via `ALTER TABLE` (bancos antigos preservados).
- Tabela `categorias` semeada na ordem do cartaz se estiver vazia.

## [1.0.0] — MVP
- Venda, carrinho, cupom 32 col, SQLite WAL, backup, fechamento simples, instalador, CI/CD.
