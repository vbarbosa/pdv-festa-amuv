# ADR-0015 — Fichas de consumo destacáveis (modelo quermesse)

**Status:** Aceito

## Contexto
Na quermesse o cliente paga tudo no caixa central e sai com uma "tripinha" de fichas para
rasgar e entregar nas barracas. Se comprou 3 cachorros e 2 refris, precisa de **5 papéis
individuais**, separados por linha pontilhada, para usar em barracas/momentos diferentes.

## Decisão
- Novo `ModoCupom.ReciboComVales` (valor `2`): imprime o **recibo gerencial completo**
  (total, troco, itens agrupados) e, abaixo de uma divisória dupla `====`, **desmembra**
  cada item em N vales de "1X NOME" — um por unidade física.
- O desmembramento é feito em `CupomFormatter.MontarReciboComVales`: para cada item de
  venda (ignorando linhas de desconto de combo, `ProdutoId` vazio), um laço imprime
  `Quantidade` blocos de vale.
- **Layout do vale**: nome em fonte de **altura dupla, largura normal** (`ESC ! 0x10`),
  mais estreita que a 2x2 (cabe o nome sem quebrar), + "Vale 1 item" + pontilhado, com
  **1 linha em branco de cada lado** do pontilhado para dobrar/rasgar sem cortar o texto.
- Selecionável na tela **Layout do Cupom** (3º rádio "Recibo + Vales destacáveis").

## Consequências
- ✅ Um vale por unidade, prontos para destacar — modelo clássico de quermesse.
- ✅ Testado (`FichasIndividuaisTests`): 5 unidades = 5 vales; recibo preservado; desconto
  de combo não vira vale.
- ⚠️ Gasta mais bobina que o recibo simples (esperado); o espaçamento foi calibrado no
  meio-termo para não desperdiçar papel.
