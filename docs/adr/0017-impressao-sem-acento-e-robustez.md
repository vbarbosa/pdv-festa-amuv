# ADR-0017 — Impressão sem acento + robustez plug-and-play da impressora

**Status:** Aceito

## Contexto
Impressoras térmicas baratas (POS58/YICHIP) frequentemente cospem lixo em caracteres
acentuados. E, na prática de campo, a conexão USB deu **"dispositivo desconhecido / falha
no descritor"** numa porta defeituosa, a fila do Windows ficou **offline/retida**, e jobs
RAW travados **embolavam** os tickets seguintes. O caixa não pode depender de o operador
adivinhar porta nem de a fila estar "boazinha".

## Decisão
**Impressão sem acento (só no papel):**
- `EscPosPrinter.RemoverAcentos` normaliza (FormD, remove marcas) e todo texto passa por
  `Bytes()` antes de virar ESC/POS. A **tela mantém acento**; só o cupom sai em ASCII.
- Travado por testes (`ImpressaoSemAcentoTests`): "Pão"→"Pao", "Açaí"→"Acai".

**Robustez plug-and-play:**
- `PrinterDiscovery.SugerirTermica` prioriza **USB** (impressora instalada) sobre
  **Bluetooth** (porta COM), e prefere a fila **online** (`Win32_Printer.WorkOffline=false`).
- Auto-detecção no construtor do `Servico`: sem impressora configurada, tenta achar a
  térmica sozinho ao abrir o app.
- `EscPosPrinter.PrepararFila` (antes de cada cupom, via WMI): **religa** a fila se estiver
  offline e **remove jobs presos** (Error/Retained/Blocked) que segurariam/embolariam os
  tickets. Best-effort — qualquer falha é ignorada e a impressão segue.

## Consequências
- ✅ Cupom legível em qualquer térmica; caixa não trava por acento nem por fila suja.
- ✅ USB tem prioridade (mais confiável para a festa), com Bluetooth de reserva.
- ⚠️ "Falha no descritor USB" é **hardware** (cabo/porta/energia) — o software não conserta;
  a mensagem orienta e o Bluetooth serve de alternativa. Trocar de porta USB resolveu.
