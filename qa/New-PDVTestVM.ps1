<#
    New-PDVTestVM.ps1 - Provisiona a VM base de testes do PDV (idempotente).
    -------------------------------------------------------------------------
    Cria (se ainda nao existir) uma VM Hyper-V "PDV-Test-VM" a partir de um ISO do
    Windows, com Guest Services habilitado (necessario para Copy-VMFile) e um checkpoint
    "Base-Limpa" que o orquestrador usa como estado virgem.

    NAO automatiza a INSTALACAO do Windows dentro da VM (isso exige um autounattend.xml e
    foge do escopo): o script cria a VM e liga; VOCE instala o Windows uma unica vez, cria
    o usuario, habilita o PowerShell Direct, e roda este script de novo com -Finalizar para
    ele instalar o .NET, habilitar Guest Services e tirar o checkpoint base.

    PRE-REQUISITOS NO HOST:
      - Windows Pro/Enterprise com Hyper-V habilitado (recurso do Windows).
      - Executar este script como ADMINISTRADOR.

    USO:
      # 1) cria a VM e liga para voce instalar o Windows manualmente (uma vez):
      pwsh qa\New-PDVTestVM.ps1 -IsoPath "C:\ISOs\Windows11.iso"

      # 2) depois de instalar o Windows + criar o usuario dentro da VM:
      pwsh qa\New-PDVTestVM.ps1 -Finalizar -GuestUser "pdv" -GuestPass "pdv"
#>
[CmdletBinding()]
param(
    [string]$VMName      = "PDV-Test-VM",
    [string]$IsoPath     = "",
    [int]   $MemoriaGB   = 4,
    [int]   $DiscoGB     = 60,
    [string]$SwitchName  = "Default Switch",
    [string]$VhdPath     = "",
    [switch]$Finalizar,
    [switch]$PularSdk,
    [string]$GuestUser   = "pdv",
    [string]$GuestPass   = "pdv"
)

$ErrorActionPreference = "Stop"
function Info($m) { Write-Host "[VM] $m" -ForegroundColor Cyan }
function Ok($m)   { Write-Host "[ ok ] $m" -ForegroundColor Green }
function Erro($m) { Write-Host "[ERRO] $m" -ForegroundColor Red }

# --- checagens de ambiente ---
if (-not (Get-Command Get-VM -ErrorAction SilentlyContinue)) {
    throw "Modulo Hyper-V nao encontrado. Habilite o Hyper-V no host e rode como Administrador."
}
$ehAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()
    ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $ehAdmin) { throw "Rode este script como ADMINISTRADOR (Hyper-V exige elevacao)." }

if ([string]::IsNullOrWhiteSpace($VhdPath)) {
    $VhdPath = Join-Path (Split-Path (Get-VMHost).VirtualHardDiskPath -ErrorAction SilentlyContinue) "$VMName.vhdx"
    if ([string]::IsNullOrWhiteSpace($VhdPath) -or $VhdPath -eq "\$VMName.vhdx") {
        $VhdPath = Join-Path $env:PUBLIC "Documents\Hyper-V\Virtual Hard Disks\$VMName.vhdx"
    }
}

$vm = Get-VM -Name $VMName -ErrorAction SilentlyContinue

# =====================================================================
# FASE 2: finalizar (VM ja tem Windows instalado) -> configura e tira o checkpoint base
# =====================================================================
if ($Finalizar) {
    if (-not $vm) { throw "VM '$VMName' nao existe. Rode primeiro sem -Finalizar para cria-la." }
    Info "Finalizando a VM base (Guest Services + .NET + checkpoint)..."

    # Guest Services: necessario para Copy-VMFile (injecao de artefatos do host -> guest).
    Enable-VMIntegrationService -VMName $VMName -Name "Guest Service Interface" -ErrorAction SilentlyContinue
    Ok "Guest Service Interface habilitado."

    if ((Get-VM -Name $VMName).State -ne 'Running') { Start-VM -Name $VMName; Start-Sleep -Seconds 40 }

    $cred = New-Object System.Management.Automation.PSCredential(
        $GuestUser, (ConvertTo-SecureString $GuestPass -AsPlainText -Force))

    try {
        Invoke-Command -VMName $VMName -Credential $cred -ScriptBlock {
            New-Item -ItemType Directory -Force "C:\TempPDV" | Out-Null
            Set-ExecutionPolicy -Scope LocalMachine Bypass -Force -ErrorAction SilentlyContinue
        } -ErrorAction Stop
        Ok "PowerShell Direct funcionando (guest respondeu)."
    } catch {
        throw "Nao consegui falar com o guest via PowerShell Direct. Confira o usuario/senha e se o Windows da VM esta logado. Detalhe: $($_.Exception.Message)"
    }

    # Instala o .NET 8 SDK DENTRO da VM (para rodar 'dotnet test' com a bateria FlaUI completa).
    # A VM tem rede; usamos o instalador oficial do dotnet (dotnet-install). Idempotente.
    if (-not $PularSdk) {
        Info "Instalando o .NET 8 SDK dentro da VM (pode levar alguns minutos)..."
        try {
            Invoke-Command -VMName $VMName -Credential $cred -ScriptBlock {
                if (Get-Command dotnet -ErrorAction SilentlyContinue) {
                    $v = (& dotnet --list-sdks) 2>$null
                    if ($v -match '^8\.') { return "ja instalado" }
                }
                [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
                $ps1 = "$env:TEMP\dotnet-install.ps1"
                Invoke-WebRequest "https://dot.net/v1/dotnet-install.ps1" -OutFile $ps1 -UseBasicParsing
                & $ps1 -Channel 8.0 -Quality GA -InstallDir "C:\dotnet"
                # coloca no PATH da MAQUINA (persiste no checkpoint)
                $cur = [Environment]::GetEnvironmentVariable("Path","Machine")
                if ($cur -notlike "*C:\dotnet*") {
                    [Environment]::SetEnvironmentVariable("Path", "$cur;C:\dotnet", "Machine")
                }
                return "instalado"
            } -ErrorAction Stop | ForEach-Object { Ok ".NET SDK na VM: $_" }
        } catch {
            Info "Nao consegui instalar o SDK na VM ($($_.Exception.Message.Split('.')[0])). O orquestrador cai no smoke test."
        }
    }

    # AUTO-LOGIN do usuario 'pdv': assim, toda vez que o checkpoint restaurar, a VM sobe JA
    # LOGADA na area de trabalho — o PowerShell Direct e o FlaUI funcionam sem intervencao.
    Info "Configurando auto-login do usuario '$GuestUser' na VM..."
    try {
        Invoke-Command -VMName $VMName -Credential $cred -ArgumentList $GuestUser, $GuestPass -ScriptBlock {
            param($u, $p)
            $k = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon"
            Set-ItemProperty $k -Name AutoAdminLogon -Value "1"
            Set-ItemProperty $k -Name DefaultUserName -Value $u
            Set-ItemProperty $k -Name DefaultPassword -Value $p -Type String
            Set-ItemProperty $k -Name DefaultDomainName -Value $env:COMPUTERNAME
        } -ErrorAction Stop
        Ok "Auto-login configurado."
    } catch { Info "Nao consegui configurar auto-login: $($_.Exception.Message.Split('.')[0])" }

    # desliga e tira o checkpoint BASE (estado virgem que o orquestrador restaura).
    Info "Desligando a VM para o checkpoint base..."
    Stop-VM -Name $VMName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 5
    Get-VMCheckpoint -VMName $VMName -Name "Base-Limpa" -ErrorAction SilentlyContinue |
        Remove-VMCheckpoint -ErrorAction SilentlyContinue
    Checkpoint-VM -Name $VMName -SnapshotName "Base-Limpa"
    Ok "Checkpoint 'Base-Limpa' criado. A VM esta pronta para o orquestrador."
    return
}

# =====================================================================
# FASE 1: criar a VM (Gen 2) e ligar para instalacao manual do Windows
# =====================================================================
if ($vm) {
    Info "VM '$VMName' ja existe. Nada a criar. (Use -Finalizar para configurar o checkpoint.)"
    return
}
if ([string]::IsNullOrWhiteSpace($IsoPath) -or -not (Test-Path $IsoPath)) {
    throw "Informe -IsoPath com o caminho de um ISO do Windows para criar a VM."
}

Info "Criando a VM '$VMName' (Gen 2, ${MemoriaGB}GB RAM, disco ${DiscoGB}GB)..."
New-Item -ItemType Directory -Force (Split-Path $VhdPath) | Out-Null

# switch de rede: usa o informado se existir; senao tenta o "Default Switch"; se nenhum
# existir, cria a VM SEM rede (o teste E2E nao precisa de internet).
$switch = Get-VMSwitch -Name $SwitchName -ErrorAction SilentlyContinue
if (-not $switch) { $switch = Get-VMSwitch -ErrorAction SilentlyContinue | Select-Object -First 1 }

if ($switch) {
    New-VM -Name $VMName -Generation 2 -MemoryStartupBytes ($MemoriaGB * 1GB) `
        -NewVHDPath $VhdPath -NewVHDSizeBytes ($DiscoGB * 1GB) -SwitchName $switch.Name | Out-Null
    Ok "VM com rede ($($switch.Name))."
} else {
    Info "Nenhum switch de rede encontrado; criando a VM SEM rede (nao e necessaria para o teste)."
    New-VM -Name $VMName -Generation 2 -MemoryStartupBytes ($MemoriaGB * 1GB) `
        -NewVHDPath $VhdPath -NewVHDSizeBytes ($DiscoGB * 1GB) | Out-Null
}

Set-VM -Name $VMName -DynamicMemory -MemoryMinimumBytes 2GB -MemoryMaximumBytes ($MemoriaGB * 1GB)
Set-VMProcessor -VMName $VMName -Count 2

# Gen2 + Windows exige Secure Boot com o template da Microsoft (senao nao boota o instalador).
Set-VMFirmware -VMName $VMName -EnableSecureBoot On -SecureBootTemplate "MicrosoftWindows"

# Windows 11 exige TPM 2.0: habilita o vTPM (precisa do Key Protector local).
try {
    if (-not (Get-HgsGuardian -Name "PDV-Guardian" -ErrorAction SilentlyContinue)) {
        New-HgsGuardian -Name "PDV-Guardian" -GenerateCertificates | Out-Null
    }
    $guardian = Get-HgsGuardian -Name "PDV-Guardian"
    $kp = New-HgsKeyProtector -Owner $guardian -AllowUntrustedRoot
    Set-VMKeyProtector -VMName $VMName -KeyProtector $kp.RawData
    Enable-VMTPM -VMName $VMName
    Ok "vTPM habilitado (requisito do Windows 11)."
} catch {
    Info "Nao consegui habilitar o vTPM ($($_.Exception.Message.Split('.')[0])). Se o instalador do Win11 reclamar de TPM, use o bypass (Shift+F10 no setup)."
}

Add-VMDvdDrive -VMName $VMName -Path $IsoPath
# boot pelo DVD para instalar o Windows
$dvd = Get-VMDvdDrive -VMName $VMName
Set-VMFirmware -VMName $VMName -FirstBootDevice $dvd
Ok "VM criada."

Info "Ligando a VM. Instale o Windows manualmente (crie o usuario '$GuestUser')."
Start-VM -Name $VMName
Write-Host ""
Write-Host "PROXIMOS PASSOS (uma unica vez):" -ForegroundColor Yellow
Write-Host "  1. Abra o Hyper-V Manager e conecte na '$VMName'." -ForegroundColor Gray
Write-Host "  2. Instale o Windows; crie um usuario LOCAL '$GuestUser' com senha '$GuestPass'." -ForegroundColor Gray
Write-Host "  3. Deixe a VM logada nesse usuario." -ForegroundColor Gray
Write-Host "  4. No host, rode: pwsh qa\New-PDVTestVM.ps1 -Finalizar -GuestUser $GuestUser -GuestPass $GuestPass" -ForegroundColor Gray
