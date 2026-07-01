# ADR-0018 — Hardening de UI, testes E2E e vídeo de demonstração

**Status:** Aceito

## Contexto
Vários acabamentos de robustez e apresentação surgiram no uso real: telas com texto
cortado, mensagens sem acento, senha de admin pedida até para operação corriqueira, testes
E2E travando o PC, e o vídeo de treinamento capturando as duas telas do monitor.

## Decisão
- **Acentuação PT-BR** em toda a UI visível (menus, títulos, mensagens, rótulos de coluna),
  sem tocar em chaves lógicas (nome de categoria, AutomationId de teste).
- **Anti-corte de layout**: `AjusteLayout.Blindar` (aplicado central em `Dialogos.Modal`)
  cresce a janela/painéis para caber o conteúdo; `EstiloGrid.Padronizar` dá altura/fonte de
  cabeçalho a todos os grids (o "Pagamento" não corta mais).
- **Senha de admin só no crítico**: abrir/fechar caixa, sangria/suprimento, produtos,
  backup, estorno. Config leve (categorias, promoções, impressora, layout, atalhos) liberada.
- **Testes E2E de UI marcados `Skip`** (`E2ETestBase.SkipUI`): passam isolados, mas
  dependem de um desktop dedicado em foco (limitação do FlaUI/UIA3). Rodam em máquina livre.
  Um **porteiro anti-órfão** (`E2ETestBase`) mata apps órfãos antes/depois de cada teste, e
  `xunit.runner.json` força execução serial (1 app por vez).
- **Vídeo (`Gerar-VideoTreinamento.ps1`)**: no modo demo (`PDV_DEMO=1`) a janela abre
  **maximizada na tela primária**, e o ffmpeg captura **só a janela** (`gdigrab -i title=`),
  nunca o desktop das 2 telas. Cuidados que custaram caro: título com espaços precisa de
  aspas embutidas no `-ArgumentList`; dimensão ímpar quebra libx264 (filtro força pares);
  `—` (em-dash) unicode quebra o parser do PowerShell 5.1.

## Consequências
- ✅ App com acabamento profissional; nada cortado; senha só onde importa.
- ✅ Suíte unitária (97 testes) sempre verde e rápida; E2E disponível sob demanda.
- ✅ Vídeo de treinamento limpo (uma tela), com trilha real e sem popup de impressora.
- ⚠️ Testes E2E de UI não rodam no CI headless nem em máquina em uso — por design.
