# ADR-0009 — Cupom com modos e estilos de linha

**Status:** Aceito

## Contexto
No pico da festa, a barraca só precisa saber **o que entregar** — imprimir um recibo
completo gasta bobina e atrasa. Já o controle financeiro precisa do recibo detalhado.

## Decisão
`CupomFormatter.MontarTicket(venda, ConfigCupom)` produz uma `List<LinhaCupom>`, onde cada
linha carrega um **estilo** (`Normal`, `Titulo`, `Expandida`, `Corte`). Dois modos:

- **Recibo Completo:** cabeçalho, itens com valor, total, pagamento, troco.
- **Ficha de Consumo:** só `quantidade + nome` em **fonte expandida** (dupla largura →
  16 colunas, com word-wrap), sem valores; opção de **cortar/separar** uma ficha por item.

A camada ESC/POS (`EscPosPrinter.MontarBytes`) traduz cada estilo em comandos. Um preview
mono (Courier New) em `FormLayoutCupom` mostra o resultado antes de gastar papel.

## Consequências
- ✅ Economia de bobina e entrega rápida na barraca.
- ✅ Regras de layout testáveis (largura por modo verificada em `TicketTests`).
- ✅ Fácil adicionar um 3º modo no futuro.
