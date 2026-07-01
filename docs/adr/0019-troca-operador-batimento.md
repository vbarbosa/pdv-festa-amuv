# ADR-0019 — Troca de operador com batimento de caixa opcional

**Status:** Aceito

## Contexto
Numa festa longa os voluntários se revezam no caixa. Trocar de operador não pode parar a
venda, e às vezes se quer **conferir a gaveta** na troca (bater o dinheiro), às vezes não
(festa corrida). A base de turnos (ADR-0007) já tinha abertura/fechamento e o Total em
Gaveta; faltava o fluxo de passar o bastão.

## Decisão
- **`Caixa.Bater(resumo, contado)`** (Core, puro): compara o CONTADO com o ESPERADO
  (`ResumoTurno.TotalGavetaCentavos`) e devolve `ResultadoBatimento` com a diferença
  (`Sobra` / `Falta` / `Bate`). Pix e cartão não entram (não há dinheiro físico por eles).
- **`Servico.TrocarOperador(novoOperador, fundoNovo)`**: fecha o turno atual (com sua
  Leitura Z / audit) e abre um novo, sem parar a festa. O fundo do novo turno é o dinheiro
  que fica na gaveta (sugerido = Total em Gaveta).
- **`FormTrocaOperador`**: batimento **OPCIONAL** via checkbox "Conferir a gaveta agora".
  Marcado, mostra o esperado, pede o contado e exibe a diferença ao vivo; se não bate, pede
  confirmação antes de trocar. Desmarcado, só passa o bastão (troca o operador).
- É ação **crítica** (fecha turno, mexe em dinheiro): exige **senha de admin** (ADR-0018).

## Consequências
- ✅ Revezamento de voluntários sem parar o caixa; conferência quando se quer.
- ✅ Lógica do batimento pura e testada (`BatimentoTests`): bate/sobra/falta, ignora
  pix/cartão, considera suprimentos e sangrias.
- ⚠️ Cada troca gera um turno novo — mais turnos no `.db` (esperado; é o que dá
  contabilidade por operador). A Leitura Z de cada turno continua disponível.
