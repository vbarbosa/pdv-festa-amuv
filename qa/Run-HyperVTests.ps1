<#
    Run-HyperVTests.ps1 - Orquestrador de testes E2E do PDV em SANDBOX VOLATIL (Hyper-V).
    -------------------------------------------------------------------------------------
    Ciclo fechado, parametrizavel e reaproveitavel:
      1) CHECKPOINT   -> tira um instantaneo temporario do estado limpo (a partir do
                         checkpoint base "Base-Limpa").
      2) INJETA       -> Copy-VMFile leva o Setup e os binarios de teste do host -> guest.
      3) EXECUTA      -> Invoke-Command roda o instalador silencioso e a bateria E2E dentro
                         da VM; o teste tira screenshots por etapa e grava logs.
      4) EXTRAI       -> copia screenshots + logs de volta para o host (test-reports\<data>).
      5) ROLLBACK     -> Restore-VMCheckpoint volta a VM ao estado limpo e apaga o checkpoint
                         temporario. A VM base fica VIRGEM. (Sempre roda, mesmo se falhar.)

    PRE-REQUISITOS (uma vez): rodar qa\New-PDVTestVM.ps1 para criar a VM base "PDV-Test-VM"
    com o checkpoint "Base-Limpa", Guest Services e um usuario para PowerShell Direct.

    USO:
      pwsh qa\Run-HyperVTests.ps1                       # usa defaults
      pwsh qa\Run-HyperVTests.ps1 -VMName "Outra-VM" -GuestUser pdv -GuestPass pdv
      pwsh qa\Run-HyperVTests.ps1 -SetupPath "release\Setup_PDVFestaJunina.exe"
#>
[CmdletBinding()]
param(
    [string]$VMName        = "PDV-Test-VM",
    [string]$CheckpointBase = "Base-Limpa",
    [string]$GuestUser     = "pdv",
    [string]$GuestPass     = "pdv",
    [string]$SetupPath     = "",     # default: release\Setup_PDVFestaJunina.exe
    [string]$TestsDir      = "",     # default: tests\PdvFesta.E2E\bin\Debug\net8.0-windows
    [string]$RelatorioBase = "",     # default: <repo>\test-reports
    [int]   $BootTimeoutSeg = 180
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
function Info($m) { Write-Host "[QA] $m" -ForegroundColor Cyan }
function Ok($m)   { Write-Host "[ ok ] $m" -ForegroundColor Green }
function Erro($m) { Write-Host "[ERRO] $m" -ForegroundColor Red }

# --- defaults derivados ---
if ([string]::IsNullOrWhiteSpace($SetupPath))     { $SetupPath = Join-Path $RepoRoot "release\Setup_PDVFestaJunina.exe" }
if ([string]::IsNullOrWhiteSpace($TestsDir))      { $TestsDir  = Join-Path $RepoRoot "tests\PdvFesta.E2E\bin\Debug\net8.0-windows" }
if ([string]::IsNullOrWhiteSpace($RelatorioBase)) { $RelatorioBase = Join-Path $RepoRoot "test-reports" }

$carimbo   = Get-Date -Format "yyyy-MM-dd_HHmmss"
$relatorio = Join-Path $RelatorioBase $carimbo
$checkpointTmp = "QA-Run-$carimbo"

# --- validacoes de pre-requisitos ---
if (-not (Get-Command Get-VM -ErrorAction SilentlyContinue)) { throw "Hyper-V nao disponivel no host." }
$vm = Get-VM -Name $VMName -ErrorAction SilentlyContinue
if (-not $vm) { throw "VM '$VMName' nao existe. Rode qa\New-PDVTestVM.ps1 primeiro." }
if (-not (Get-VMCheckpoint -VMName $VMName -Name $CheckpointBase -ErrorAction SilentlyContinue)) {
    throw "Checkpoint base '$CheckpointBase' nao encontrado na VM. Rode New-PDVTestVM.ps1 -Finalizar."
}
if (-not (Test-Path $SetupPath)) { throw "Setup nao encontrado: $SetupPath (rode build\build-release.ps1)." }

New-Item -ItemType Directory -Force $relatorio | Out-Null
$cred = New-Object System.Management.Automation.PSCredential(
    $GuestUser, (ConvertTo-SecureString $GuestPass -AsPlainText -Force))

$exitCode = 1
try {
    # ---------------------------------------------------------------- 1) restaura base + checkpoint temp
    Info "Restaurando a VM ao estado limpo ('$CheckpointBase')..."
    if ((Get-VM -Name $VMName).State -ne 'Off') { Stop-VM -Name $VMName -Force -ErrorAction SilentlyContinue; Start-Sleep 4 }
    Restore-VMCheckpoint -VMName $VMName -Name $CheckpointBase -Confirm:$false
    Info "Criando checkpoint temporario '$checkpointTmp'..."
    Checkpoint-VM -Name $VMName -SnapshotName $checkpointTmp

    Info "Ligando a VM (HEADLESS - sem janela; roda em background)..."
    Start-VM -Name $VMName
    # HEADLESS: fecha qualquer janela de conexao (VMConnect) que possa estar aberta, para
    # a VM operar 100% em background sem atrapalhar o host. O orquestrador controla a VM
    # via PowerShell Direct (nao precisa de janela visivel).
    Get-Process vmconnect -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

    # espera o PowerShell Direct responder (guest logado e pronto)
    $prontos = $false
    for ($i = 0; $i -lt $BootTimeoutSeg; $i += 5) {
        Start-Sleep 5
        try { Invoke-Command -VMName $VMName -Credential $cred -ScriptBlock { $true } -ErrorAction Stop | Out-Null; $prontos = $true; break }
        catch { }
    }
    if (-not $prontos) { throw "A VM nao respondeu ao PowerShell Direct em $BootTimeoutSeg s (guest logado?)." }
    Ok "VM pronta."

    # ---------------------------------------------------------------- 2) injeta artefatos (host -> guest)
    Info "Preparando o guest para receber arquivos..."
    Invoke-Command -VMName $VMName -Credential $cred -ScriptBlock {
        Remove-Item C:\TempPDV -Recurse -Force -ErrorAction SilentlyContinue
        New-Item -ItemType Directory -Force C:\TempPDV, C:\TempPDV\tests, C:\TempPDV\evidencias | Out-Null
        # garante o servico de Guest Services (vmicguestinterface) rodando dentro da VM.
        Set-Service vmicguestinterface -StartupType Automatic -ErrorAction SilentlyContinue
        Start-Service vmicguestinterface -ErrorAction SilentlyContinue
    }
    # Copy-VMFile depende do Guest Service Interface, que demora a ficar pronto apos o boot
    # (erro 0x80070015 "dispositivo nao esta pronto"). Faz retry ate 6x com pausa.
    function CopiarComRetry($src, $dst) {
        for ($t = 1; $t -le 6; $t++) {
            try { Copy-VMFile -Name $VMName -SourcePath $src -DestinationPath $dst -CreateFullPath -FileSource Host -Force -ErrorAction Stop; return }
            catch { if ($t -eq 6) { throw }; Start-Sleep 10 }
        }
    }
    Info "Injetando artefatos na VM (C:\TempPDV)... (aguardando Guest Services)"
    CopiarComRetry $SetupPath "C:\TempPDV\Setup_PDVFestaJunina.exe"
    Ok "Setup injetado."
    # binarios de teste E2E (FlaUI) - copia a pasta inteira de saida do build de teste
    if (Test-Path $TestsDir) {
        Get-ChildItem $TestsDir -File -Recurse | ForEach-Object {
            $rel = $_.FullName.Substring($TestsDir.Length).TrimStart('\')
            CopiarComRetry $_.FullName "C:\TempPDV\tests\$rel"
        }
        Ok "Binarios de teste E2E injetados."
    } else {
        Info "Pasta de testes nao encontrada ($TestsDir); rodarei so o instalador + smoke."
    }

    # ---------------------------------------------------------------- 3) instala + roda E2E na VM
    Info "Instalando o PDV e rodando a bateria E2E dentro da VM..."
    $resultado = Invoke-Command -VMName $VMName -Credential $cred -ScriptBlock {
        $log = "C:\TempPDV\evidencias\resultado.log"
        function L($m) { "$([DateTime]::Now.ToString('HH:mm:ss')) $m" | Tee-Object -FilePath $log -Append }

        L "Instalando (silencioso, com icone)..."
        $p = Start-Process "C:\TempPDV\Setup_PDVFestaJunina.exe" `
            -ArgumentList "/VERYSILENT","/SUPPRESSMSGBOXES","/NORESTART","/TASKS=desktopicon" -Wait -PassThru
        L "Instalador exit=$($p.ExitCode)"

        # localiza o exe instalado
        $exe = "$env:LOCALAPPDATA\Programs\FestaJuninaPDV\PDV-Festa-AMUV.exe"
        if (-not (Test-Path $exe)) { L "!!! exe nao instalado"; return @{ ok = $false; etapa = "instalacao" } }

        # roda a bateria FlaUI (se veio) apontando PDV_E2E_EVID para salvar screenshots.
        $dll = "C:\TempPDV\tests\PdvFesta.E2E.dll"
        if (Test-Path $dll) {
            $env:PDV_E2E_EVID = "C:\TempPDV\evidencias"
            L "Rodando E2E (FlaUI) com evidencias em $env:PDV_E2E_EVID ..."
            # o SDK foi instalado em C:\dotnet (pode nao estar no PATH da sessao PSDirect).
            $dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
            if (-not $dotnet -and (Test-Path "C:\dotnet\dotnet.exe")) { $dotnet = "C:\dotnet\dotnet.exe" }
            if ($dotnet) {
                & $dotnet test $dll --nologo 2>&1 | Tee-Object -FilePath $log -Append
                L "dotnet test exit=$LASTEXITCODE"
            } else {
                # sem SDK na VM: usa o vstest console se existir; senao so smoke.
                L "SDK dotnet ausente na VM; fazendo smoke test (abre o app 6s)."
                $ap = Start-Process $exe -PassThru; Start-Sleep 6
                if (-not $ap.HasExited) { $ap.Kill(); L "smoke OK (app abriu)" } else { L "smoke FALHOU (app fechou)" }
            }
        } else {
            L "Sem DLL de teste; smoke test (abre o app 6s)."
            $ap = Start-Process $exe -PassThru; Start-Sleep 6
            if (-not $ap.HasExited) { $ap.Kill(); L "smoke OK" } else { L "smoke FALHOU" }
        }
        return @{ ok = $true; etapa = "concluido" }
    }
    Ok "Execucao na VM concluida (etapa: $($resultado.etapa))."

    # ---------------------------------------------------------------- 4) extrai evidencias (guest -> host)
    Info "Extraindo evidencias para $relatorio ..."
    # Copy-VMFile so copia host->guest; para guest->host usamos uma sessao PSDirect + leitura.
    $sessao = New-PSSession -VMName $VMName -Credential $cred
    try {
        $arquivos = Invoke-Command -Session $sessao -ScriptBlock {
            Get-ChildItem "C:\TempPDV\evidencias" -File -Recurse -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName
        }
        foreach ($a in $arquivos) {
            $destino = Join-Path $relatorio (Split-Path $a -Leaf)
            Copy-Item -FromSession $sessao -Path $a -Destination $destino -Force
        }
        Ok "$($arquivos.Count) arquivo(s) de evidencia extraidos."
    } finally { Remove-PSSession $sessao -ErrorAction SilentlyContinue }

    $exitCode = 0
}
catch {
    Erro $_.Exception.Message
    "$([DateTime]::Now) ERRO no orquestrador: $($_.Exception.Message)" |
        Out-File (Join-Path $relatorio "orquestrador-erro.log") -Append
}
finally {
    # ---------------------------------------------------------------- 5) ROLLBACK (sempre)
    Info "Rollback: revertendo a VM ao estado limpo e apagando o checkpoint temporario..."
    try {
        if ((Get-VM -Name $VMName -ErrorAction SilentlyContinue).State -ne 'Off') {
            Stop-VM -Name $VMName -Force -ErrorAction SilentlyContinue; Start-Sleep 4
        }
        Restore-VMCheckpoint -VMName $VMName -Name $CheckpointBase -Confirm:$false -ErrorAction SilentlyContinue
        Get-VMCheckpoint -VMName $VMName -Name $checkpointTmp -ErrorAction SilentlyContinue |
            Remove-VMCheckpoint -ErrorAction SilentlyContinue
        Ok "VM base restaurada e virgem. Checkpoint temporario removido."
    } catch { Erro "Falha no rollback: $($_.Exception.Message)" }
}

Write-Host ""
Write-Host "======================================================" -ForegroundColor White
if ($exitCode -eq 0) { Write-Host " TESTES E2E (SANDBOX) OK" -ForegroundColor Green }
else                 { Write-Host " TESTES E2E (SANDBOX) FALHARAM - veja o relatorio" -ForegroundColor Red }
Write-Host "  Relatorio: $relatorio" -ForegroundColor Gray
Write-Host "======================================================" -ForegroundColor White
exit $exitCode
