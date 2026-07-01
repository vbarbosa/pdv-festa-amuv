# ADR-0011 — Blindagem global de exceções + logs em arquivo

**Status:** Aceito

## Contexto
No caos de um evento ao vivo, um erro não tratado abre a tela de "Unhandled Exception" do
.NET e **fecha o programa na cara do operador**. E sem logs, diagnosticar depois é adivinhação.

## Decisão
- **Blindagem global:** `Program` registra `Application.ThreadException` e
  `AppDomain.UnhandledException`; qualquer erro é logado e mostrado como aviso amigável,
  **sem derrubar** o app.
- **Impressão nunca lança** (ver [ADR-0003](0003-escpos-raw.md)); falha vira diálogo
  Repetir/Ignorar, com a venda já salva.
- **Logs em arquivo:** classe `Log` grava em `%AppData%\FestaJuninaPDV\logs\pdv-AAAAMMDD.log`
  (thread-safe, rotação de ~14 dias). Registra ciclo de vida, turnos, vendas, falhas de
  impressão e exceções.

## Consequências
- ✅ O caixa sobrevive a erros inesperados e a falhas de hardware.
- ✅ Diagnóstico pós-fato (e durante o desenvolvimento) — os logs já ajudaram a achar e
  corrigir o crash de `SplitContainer` no startup.
