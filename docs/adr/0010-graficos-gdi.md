# ADR-0010 — Gráficos do dashboard em GDI+ próprio

**Status:** Aceito

## Contexto
O dashboard de fechamento pede gráficos simples (vendas por forma de pagamento, itens mais
vendidos). Bibliotecas de chart (LiveCharts, ScottPlot, etc.) adicionam dependências,
peso ao single-file e risco de incompatibilidade — contra o [ADR-0005](0005-single-file-zero-deps.md).

## Decisão
Implementar `GraficoBarras : Panel`, um gráfico de barras horizontais desenhado à mão em
**GDI+** (`OnPaint`). Fontes e barras são **proporcionais ao tamanho** do controle, então
ele **escala com a janela** (redimensionar = "zoom"). Cores passadas por dado.

## Consequências
- ✅ Zero dependências novas; funciona no single-file em qualquer PC.
- ✅ Controle total de aparência; escala/zoom grátis via `Dock=Fill`.
- ⚠️ Sem recursos avançados (tooltips, animação) — desnecessários aqui.
