# Diagnóstico e Logs

O sistema registra tudo que importa em arquivo, para auditoria no dia da festa e para
depuração pelo desenvolvedor.

## Onde ficam os logs
```
%AppData%\FestaJuninaPDV\logs\pdv-AAAAMMDD.log
```
Um arquivo por dia; rotação automática (~14 dias). Nos testes E2E, o log fica na pasta do
banco temporário (`$env:PDVFESTA_DB`).

## Formato
```
[2026-07-04 19:32:10.482] INFO  Caixa ABERTO #1 fundo=10000c operador='Tia Ana'
[2026-07-04 19:35:41.907] INFO  Venda #12 Dinheiro total=1300c troco=700c caixa=1
[2026-07-04 20:10:02.113] WARN  Impressao falhou venda #40: Falha de hardware...
[2026-07-04 22:59:58.006] ERRO  (ThreadException) erro nao tratado
System.NullReferenceException: ...
```
Níveis: `INFO` (ciclo de vida, turno, venda), `WARN` (falha recuperável, ex: impressora),
`TRACE` (detalhe), `ERRO` (exceção capturada).

## O que é logado
- Início/fim do app e caminho do banco.
- Abertura/fechamento de caixa, sangria/suprimento.
- Cada venda (id, forma, total, troco, turno).
- Falhas de impressão.
- Toda exceção não tratada (com stack trace).

## Erros comuns

| Sintoma | Causa provável | Ação |
|---|---|---|
| "Erro na Impressora. Verifique o cabo e o papel." | Sem papel / cabo solto / porta ocupada | Repor papel/cabo e **Repetir**; a venda já está salva |
| Impressora não aparece na lista (F12) | Impressora desligada/não conectada | Ligar e conectar o USB; **Atualizar** |
| "O caixa está fechado" ao pagar | Turno não aberto | Menu **Arquivo → Abrir Caixa** |
| App não abre / fecha sozinho | Erro no startup | Ler o `pdv-*.log`; a linha `ERRO (Startup)` traz o stack trace |
| CSV abre "tudo numa coluna" no Excel | Separador de lista do Windows difere | O CSV usa o separador da **cultura do sistema**; confira Região do Windows |

## Dica para o desenvolvedor
O log foi o que permitiu diagnosticar o crash de `SplitContainer` no startup
(`SplitterDistance deve ficar entre Panel1MinSize e Width - Panel2MinSize`) sem depurador:
basta rodar o `.exe` com `PDVFESTA_DB` apontando para uma pasta temporária e ler o log.
