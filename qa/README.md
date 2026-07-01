# QA — Testes E2E em Sandbox Volátil (Hyper-V)

Roda os testes de interface (FlaUI) e o instalador **dentro de uma VM isolada**, sem sujar
o host. Ciclo: tira checkpoint → injeta artefatos → instala e testa → extrai screenshots/logs
→ **reverte a VM ao estado limpo** (sandbox volátil). O host nunca vê lixo de registro/arquivos.

## Por que isso

Testes de UI (FlaUI/UIA3) precisam de um desktop **ativo e em foco** e podem travar a máquina
em uso. Rodando numa VM dedicada, eles têm a tela só pra eles — e no fim a VM "explode" e volta
ao estado virgem em segundos (via checkpoint), sem reinstalar nada.

## Arquivos

| Script | Função |
|---|---|
| `New-PDVTestVM.ps1` | Cria/configura a VM base `PDV-Test-VM` (uma vez). |
| `Run-HyperVTests.ps1` | Orquestrador do ciclo completo (roda a cada bateria). |

## Pré-requisitos no HOST

1. **Windows Pro/Enterprise** com **Hyper-V** habilitado
   (`Painel de Controle → Programas → Ativar recursos do Windows → Hyper-V`).
2. Abrir o PowerShell **como Administrador** (Hyper-V exige elevação).
3. Ter um **ISO do Windows** (10/11) para instalar na VM base.
4. Ter gerado o release: `pwsh build\build-release.ps1` (produz o `Setup_...exe`).

## Setup da VM base (uma única vez)

```powershell
# 1) cria a VM e liga (boot pelo ISO):
pwsh qa\New-PDVTestVM.ps1 -IsoPath "C:\ISOs\Windows11.iso"

# 2) no Hyper-V Manager, conecte na VM e:
#    - instale o Windows;
#    - crie um usuario LOCAL "pdv" com senha "pdv" (ou passe outros via parametro);
#    - deixe a VM LOGADA nesse usuario (o PowerShell Direct usa essa sessao).
#    - (opcional) instale o .NET SDK 8 na VM se quiser rodar `dotnet test` lá;
#      sem o SDK, o orquestrador faz um smoke test (abre o app e confere).

# 3) finaliza: habilita Guest Services e cria o checkpoint "Base-Limpa":
pwsh qa\New-PDVTestVM.ps1 -Finalizar -GuestUser pdv -GuestPass pdv
```

### Pré-requisitos DENTRO da VM (por que cada um)

- **Guest Service Interface** (habilitado pelo `-Finalizar`): necessário para `Copy-VMFile`
  injetar o Setup e os binários de teste do host → guest.
- **Usuário local logado**: `Invoke-Command -VMName` (PowerShell Direct) autentica com esse
  usuário/senha e usa a sessão interativa — por isso a VM deve estar logada.
- **ExecutionPolicy Bypass** (setado pelo `-Finalizar`): permite rodar os scripts.
- **.NET SDK 8** (opcional): só se quiser rodar a bateria `dotnet test` completa dentro da
  VM. O app é self-contained, então o smoke test funciona sem o SDK.

## Rodar a bateria (quantas vezes quiser)

```powershell
pwsh qa\Run-HyperVTests.ps1
# ou parametrizado:
pwsh qa\Run-HyperVTests.ps1 -VMName "PDV-Test-VM" -GuestUser pdv -GuestPass pdv `
    -SetupPath "release\Setup_PDVFestaJunina.exe"
```

O relatório (screenshots por etapa + logs) sai em **`test-reports\<data-hora>\`** no host.
Ao final, a VM é **sempre** revertida ao estado limpo — mesmo se os testes falharem.

## Evidências (screenshots por etapa)

Os testes E2E capturam a tela nas etapas críticas (abertura, itens no carrinho, pagamento/troco,
venda concluída) **apenas quando** a variável `PDV_E2E_EVID` aponta uma pasta — o orquestrador
seta isso dentro da VM. Em execução local normal, a captura é um no-op (não atrapalha nada).

## Notas de robustez

- `Restore-VMCheckpoint` (nome atual; o antigo `Restore-VMSnapshot` é um alias) faz o rollback.
- O rollback está num bloco `finally`: roda **sempre**, mesmo com exceção no meio.
- Cada execução cria um checkpoint temporário próprio (`QA-Run-<data>`) e o apaga no fim,
  deixando só o `Base-Limpa` — a VM base nunca acumula checkpoints.
