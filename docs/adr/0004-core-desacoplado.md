# ADR-0004 — Core de domínio desacoplado da UI

**Status:** Aceito

## Contexto
Regra de negócio misturada com WinForms é impossível de testar sem tela e vira "código de
faculdade". Precisamos testar troco, consolidação e formatação de cupom automaticamente.

## Decisão
Duas assemblies: `PdvFesta.Core` (domínio puro, sem WinForms) e `PdvFesta.App` (UI).
Dependência **App → Core**, nunca o contrário. A UI conversa com o domínio por uma fachada
única (`Servico`).

## Consequências
- ✅ Core 100% testável em xUnit sem hardware nem tela (66 testes unitários).
- ✅ Trocar a UI (ex: web) no futuro não toca na regra de negócio.
- ✅ TDD: a lógica é escrita e testada antes da tela.
