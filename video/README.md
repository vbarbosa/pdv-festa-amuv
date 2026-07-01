# 🎬 Vídeo de Treinamento Autônomo

O sistema grava o **próprio vídeo de treinamento/demonstração** — o computador abre o
caixa, o mouse anda sozinho clicando nos produtos, paga, mostra o fechamento, e o
FFmpeg edita tudo com letreiros estilo WordArt/anos 2000 e cospe um MP4 pronto pro
WhatsApp.

## Como gerar

```powershell
pwsh video/Gerar-VideoTreinamento.ps1
```

O script faz tudo sozinho:
1. Baixa o **FFmpeg** (open-source) para `tools/ffmpeg` se não existir.
2. Compila o app (Debug win-x64).
3. Grava a tela (`gdigrab`) enquanto o **FlaUI `DemoMode`** opera o caixa devagar.
4. Edita: letreiros WordArt (Impact, amarelo com borda azul), cartões de abertura/
   encerramento e transições (fade).
5. Gera **`video/Treinamento_PDV_FestaJunina.mp4`** — H.264, `yuv420p`, 720p, CRF 27,
   `+faststart` (compatível com WhatsApp, sem reencode no celular).

## Roteiro (4 cenas)

| Cena | Ação | Letreiro |
|------|------|----------|
| 1 | Tela principal | CAIXA LIVRE! |
| 2 | Clica 1x Quentão + 2x Cartela Bingo | ATALHOS 1 A 9 OU CLIQUE! |
| 3 | Paga (F2) e digita o recebido | TROCO AUTOMÁTICO! |
| 4 | Fechamento de caixa (totais em verde) | FECHAMENTO BLINDADO! |

> A rotina do ator vive em `tests/PdvFesta.E2E/DemoMode.cs` (só roda sob `PDV_DEMO=1`,
> definido pelo orquestrador; em CI/local normal ela passa trivialmente).

> Requer sessão de desktop ativa (o FlaUI move o mouse de verdade). Não rode em servidor
> headless.
