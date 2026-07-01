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

## Decisão (revisada) — dois caminhos: USB e Bluetooth
`EnviarRaw` escolhe o caminho pelo alvo selecionado:
- **USB / fila do Windows** (nome de impressora instalada) → spooler (`winspool`, RAW).
- **Bluetooth / serial** (`COMx`) → porta serial (`System.IO.Ports.SerialPort`, 9600 8N1).

Assim o **mesmo binário** imprime nas duas conexões. A descoberta rotula as portas
(`COM6 (Bluetooth)`, `COM3 (serial)`) para o operador saber qual escolher. Detalhes de
setup em [IMPRESSORA.md](../IMPRESSORA.md).

## Consequências
- ✅ Sem janela de diálogo; impressão instantânea; layout fixo de 32 colunas.
- ✅ Fonte expandida (dupla) para a "ficha de consumo"; corte de papel controlado.
- ✅ Funciona por **USB (driver/fila)** e por **Bluetooth (porta COM SPP)** — validado numa
  MPT-II real na COM6 (duas vendas atômicas impressas).
- ⚠️ Específico de Windows (`[SupportedOSPlatform("windows")]`).
- 🔒 `EnviarRaw` é **blindado**: qualquer falha de hardware vira `(false, msg)`, nunca exceção.
