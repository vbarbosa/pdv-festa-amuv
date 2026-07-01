# ADR-0022 — QA em sandbox volátil (Hyper-V) para testes E2E

**Status:** Aceito

## Contexto
Testes de UI (FlaUI/UIA3) exigem um desktop ativo e em foco; rodá-los na máquina em uso
trava o PC e é não-determinístico (ver ADR-0018, que os deixou `Skip`). Para ter cobertura
funcional real sem sujar o host, a solução é rodá-los numa VM isolada e descartável.

## Decisão
- **`qa/New-PDVTestVM.ps1`**: provisiona a VM base `PDV-Test-VM` (Gen2), habilita Guest
  Services e cria o checkpoint `Base-Limpa` (estado virgem).
- **`qa/Run-HyperVTests.ps1`**: ciclo fechado — restaura o checkpoint base → cria checkpoint
  temporário → `Copy-VMFile` injeta Setup + binários de teste → `Invoke-Command` (PowerShell
  Direct) instala silencioso e roda a bateria → extrai screenshots/logs para
  `test-reports/<data>` → **rollback no `finally`** (sempre reverte, mesmo em falha).
- **Evidências**: `E2ETestBase.Evidencia()` tira screenshot por etapa quando `PDV_E2E_EVID`
  aponta uma pasta (setada pelo orquestrador na VM).
- **Execução condicional**: os testes E2E checam `EmSandbox` (a env var) — rodam de verdade
  só dentro da VM; fora, são no-op (não travam o host).

## Consequências
- ✅ Cobertura funcional real sem lixo no host; a VM "explode" e volta ao limpo em segundos.
- ✅ Parametrizável (VM, credenciais, caminhos) e reaproveitável a cada bateria.
- ⚠️ Requer Hyper-V habilitado + admin + um ISO do Windows (setup único). Ver
  `qa/STATUS-EXECUCAO.md` para o estado no ambiente atual e o passo-a-passo.
