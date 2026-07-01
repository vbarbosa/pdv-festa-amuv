# Status da execução da bateria E2E em Hyper-V

## ✅ Funcionando de ponta a ponta

A bateria E2E em sandbox Hyper-V **roda e passa** neste ambiente:

```
[ ok ] VM pronta.
[ ok ] Setup injetado.
[ ok ] Binarios de teste E2E injetados.
[ ok ] Execucao na VM concluida.
[ ok ] 17 arquivo(s) de evidencia extraidos.
[ ok ] VM base restaurada e virgem.
 TESTES E2E (SANDBOX) OK   ->  5 aprovados, 1 skip (impressora), 0 falhas
```

Evidências visuais geradas em `test-reports/<data-hora>/` (não versionadas): pagamento
estilo PDV com troco, painel em tempo real (gaveta/faturamento/gráficos), histórico, carrinho
com itens por venda, etc.

## Setup que foi necessário (uma vez)

1. **Hyper-V no Windows 11 Home**: habilitado via `qa/Habilitar-HyperV-no-Home.bat` (método
   DISM da comunidade) + reboot.
2. **VM base `PDV-Test-VM`**: criada com `qa/Criar-VM.bat` (Gen2, Secure Boot, vTPM) a partir
   de um ISO x64 do Windows.
3. **Windows instalado na VM** + usuário **local** `pdv` (senha `pdv`) — conta local é
   obrigatória (o PowerShell Direct não usa conta Microsoft).
4. **Finalização** com `qa/Preparar-SDK-e-Recongelar.bat`: habilita Guest Services, instala o
   .NET 8 SDK na VM, configura **auto-login** do `pdv`, e congela o checkpoint `Base-Limpa`.

## Como rodar a bateria (quantas vezes quiser)

```powershell
# como administrador:
Start-Process ".\qa\Rodar-Testes-Sandbox.bat" -Verb RunAs
```

A VM roda **headless** (background, sem janela) e é **sempre revertida** ao estado limpo no
fim. Relatório com screenshots em `test-reports/<data-hora>/`.

## Detalhes técnicos que custaram caro (documentados para não repetir)

- **Injeção via `Copy-Item -ToSession`** (não `Copy-VMFile`): este último falha com
  `0x80070015` (Guest Service Interface frágil após boot).
- **Testes na sessão interativa** via **tarefa agendada `/IT`**: rodar pelo PowerShell Direct
  é não-interativo (`UserInteractive=false`) e os modais/FlaUI falham.
- **App blindado** contra sessão não-interativa (`Program.cs`): o tratador de erro não pode
  mostrar MessageBox cegamente, senão derruba o app em cascata ao reportar um erro.
- **`schtasks /ST`** precisa de hora futura (recusa `00:00` no passado).
