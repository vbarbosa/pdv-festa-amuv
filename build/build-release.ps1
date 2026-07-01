<#
    build-release.ps1 - Engenharia de Release do PDV Festa Junina

    O que faz (em ordem):
      1. dotnet publish do app WinForms como .exe UNICO, self-contained (win-x64).
      2. Copia cardapio.json e o pacote do driver da impressora para a pasta de release.
      3. (Opcional) Compila o instalador Inno Setup -> Setup_PDVFestaJunina.exe.
      4. Zipa tudo -> FestaJuninaPDV_Release_vX.zip (pronto p/ WhatsApp / GitHub Releases).

    Uso:
      pwsh build\build-release.ps1                 # build completo
      pwsh build\build-release.ps1 -Versao 1.2     # define a versao no nome do zip
      pwsh build\build-release.ps1 -SemInstalador  # pula o Inno Setup (so o zip portable)

    Requisitos: .NET SDK 8+; Inno Setup 6 (para o Setup.exe) - opcional.
#>

param(
    [string]$Versao = "1.0",
    [switch]$SemInstalador
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$AppProj  = Join-Path $RepoRoot "src\PdvFesta.App\PdvFesta.App.csproj"
$OutDir   = Join-Path $RepoRoot "release"
$PubDir   = Join-Path $OutDir "PDV-Festa"      # conteudo que vai no zip
$DriverSrc = Join-Path $RepoRoot "assets\driver-impressora"  # pacote do driver (opcional)

function Info($m) { Write-Host "[build] $m" -ForegroundColor Cyan }
function Ok($m)   { Write-Host "[ ok  ] $m" -ForegroundColor Green }
function Warn($m) { Write-Host "[warn ] $m" -ForegroundColor Yellow }

# --- Limpeza ---
if (Test-Path $OutDir) { Remove-Item $OutDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $PubDir | Out-Null

# --- 1) Publish single-file self-contained ---
Info "Publicando o app (single-file, self-contained, win-x64)..."
dotnet publish $AppProj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $PubDir --nologo | Out-Null

$exe = Get-ChildItem $PubDir -Filter "*.exe" | Select-Object -First 1
if (-not $exe) { throw "Publish falhou: .exe nao encontrado em $PubDir" }
Ok "Executavel gerado: $($exe.Name) ($([math]::Round($exe.Length/1MB,1)) MB)"

# --- 2) Recursos que acompanham o app ---
Copy-Item (Join-Path $RepoRoot "src\PdvFesta.App\cardapio.json") $PubDir -Force
Ok "cardapio.json copiado."

# Pacote do driver da impressora (se existir) -> vai junto no zip
if (Test-Path $DriverSrc) {
    $driverDst = Join-Path $PubDir "driver-impressora"
    Copy-Item $DriverSrc $driverDst -Recurse -Force
    Ok "Pacote do driver da impressora incluido."
} else {
    Warn "Pasta do driver nao encontrada ($DriverSrc). O zip sai sem o driver."
}

# Manual do operador (se existir)
$manual = Join-Path $RepoRoot "docs\MANUAL-OPERADOR.md"
if (Test-Path $manual) { Copy-Item $manual (Join-Path $PubDir "MANUAL-OPERADOR.txt") -Force }

# --- 3) Instalador Inno Setup (opcional) ---
if (-not $SemInstalador) {
    $iscc = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1

    if ($iscc) {
        Info "Compilando o instalador com Inno Setup..."
        $iss = Join-Path $RepoRoot "installer\PDVFesta.iss"
        & $iscc "/DMyAppVersion=$Versao" "$iss" | Out-Null
        $setup = Join-Path $OutDir "Setup_PDVFestaJunina.exe"
        if (Test-Path $setup) { Ok "Instalador gerado: Setup_PDVFestaJunina.exe" }
        else { Warn "Inno Setup rodou mas nao achei o Setup.exe (verifique OutputDir no .iss)." }
    } else {
        Warn "Inno Setup nao encontrado; pulei o Setup.exe. (Instale ou use -SemInstalador)"
    }
}

# --- 4) Zip portable final ---
$zip = Join-Path $OutDir "FestaJuninaPDV_Release_v$Versao.zip"
Info "Compactando o pacote portable..."
Compress-Archive -Path (Join-Path $PubDir "*") -DestinationPath $zip -Force
Ok "ZIP pronto: $zip"

Write-Host ""
Write-Host "======================================================" -ForegroundColor White
Write-Host " RELEASE v$Versao CONCLUIDO" -ForegroundColor Green
Write-Host "  - Portable (zip): $zip" -ForegroundColor Gray
if (-not $SemInstalador) { Write-Host "  - Instalador:     $OutDir\Setup_PDVFestaJunina.exe" -ForegroundColor Gray }
Write-Host "======================================================" -ForegroundColor White
