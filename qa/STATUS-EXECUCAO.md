# Status da execução da bateria E2E em Hyper-V

## Situação neste ambiente (host atual)

A bateria E2E em sandbox Hyper-V **não pôde ser EXECUTADA automaticamente** neste PC porque:

| Bloqueio | Detalhe |
|---|---|
| Hyper-V não habilitado | O módulo PowerShell `Hyper-V` está ausente (`Get-VM` indisponível). |
| Sem privilégio de admin | O ambiente de execução não é elevado e não pode acionar o UAC. |
| Sem ISO do Windows | Nenhuma imagem de instalação encontrada para criar a VM base. |

Habilitar o Hyper-V exige elevação **e reinício do Windows** — só pode ser feito por você.

## O que está PRONTO (validado)

- `New-PDVTestVM.ps1` e `Run-HyperVTests.ps1` — sintaxe validada pelo parser; o orquestrador
  **detecta a ausência do Hyper-V e falha com mensagem clara** (não crasha).
- E2E com screenshots por etapa e execução condicional (`EmSandbox`): rodam de verdade só
  quando `PDV_E2E_EVID` está setada (dentro da VM), e são no-op fora (não travam o host).
- Guia completo em `qa/README.md`.

## Como habilitar e rodar (você, uma vez)

```powershell
# 1) habilitar o Hyper-V (PowerShell como Administrador) — REINICIA o Windows:
Enable-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V-All -All

# 2) depois do reboot, criar a VM base (precisa de um ISO do Windows):
pwsh qa\New-PDVTestVM.ps1 -IsoPath "C:\ISOs\Windows11.iso"
#    (instale o Windows na VM, crie o usuario 'pdv', deixe logado)
pwsh qa\New-PDVTestVM.ps1 -Finalizar -GuestUser pdv -GuestPass pdv

# 3) rodar a bateria E2E em sandbox (quantas vezes quiser):
pwsh qa\Run-HyperVTests.ps1
#    -> relatorio com screenshots em test-reports\<data-hora>\
```

## Cobertura que FOI executada neste ambiente

- **157 testes unitários** (Core): passando. Cobrem preços/combos (incl. horário cruzando
  meia-noite, múltiplos conjuntos), troco, formatação de cupom/vales, robustez de banco
  (round-trip, retomada de caixa, valores extremos, estorno), config do cupom, exclusão com
  trava e versionamento do cardápio.
- **E2E**: compilam e são no-op seguro fora da sandbox (verificado). Prontos para a VM.
