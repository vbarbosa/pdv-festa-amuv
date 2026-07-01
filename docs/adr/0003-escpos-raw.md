# ADR-0003 — Impressão ESC/POS RAW via spooler

**Status:** Aceito

## Contexto
Impressoras térmicas de 58mm (ex: MPT-II) imprimem via comandos **ESC/POS**. Usar
`PrintDocument`/GDI abre diálogo, renderiza como imagem (lento) e desconfigura margens.

## Decisão
Enviar **bytes ESC/POS crus** direto ao spooler do Windows via P/Invoke de `winspool.drv`
(`OpenPrinter`/`StartDocPrinter` com datatype `RAW`/`WritePrinter`). Codepage **PC860**
(português) para acentos. Comandos isolados em constantes nomeadas
(`ESC_DOUBLE_HEIGHT_WIDTH`, `GS_CUT_PAPER`, ...).

## Consequências
- ✅ Sem janela de diálogo; impressão instantânea; layout fixo de 32 colunas.
- ✅ Fonte expandida (dupla) para a "ficha de consumo"; corte de papel controlado.
- ⚠️ Específico de Windows (`[SupportedOSPlatform("windows")]`).
- 🔒 `EnviarRaw` é **blindado**: qualquer falha de hardware vira `(false, msg)`, nunca exceção.
