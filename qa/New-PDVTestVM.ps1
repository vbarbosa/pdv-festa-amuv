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

    # instala o .NET 8 Desktop Runtime dentro da VM (via winget) - o app e self-contained,
    # mas isso garante que o FlaUI/UIA3 e dependencias de UI funcionem.
    try {
        Invoke-Command -VMName $VMName -Credential $cred -ScriptBlock {
            New-Item -ItemType Directory -Force "C:\TempPDV" | Out-Null
            # habilita execucao de scripts e o UI Automation (ja vem no Windows).
            Set-ExecutionPolicy -Scope LocalMachine Bypass -Force -ErrorAction SilentlyContinue
        } -ErrorAction Stop
        Ok "PowerShell Direct funcionando (guest respondeu)."
    } catch {
        throw "Nao consegui falar com o guest via PowerShell Direct. Confira o usuario/senha e se o Windows da VM esta logado. Detalhe: $($_.Exception.Message)"
    }

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
New-VM -Name $VMName -Generation 2 -MemoryStartupBytes ($MemoriaGB * 1GB) `
    -NewVHDPath $VhdPath -NewVHDSizeBytes ($DiscoGB * 1GB) -SwitchName $SwitchName | Out-Null
Set-VM -Name $VMName -DynamicMemory -MemoryMinimumBytes 2GB -MemoryMaximumBytes ($MemoriaGB * 1GB)
Set-VMProcessor -VMName $VMName -Count 2
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
