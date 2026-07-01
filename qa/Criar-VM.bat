@echo off
REM ============================================================================
REM  Criar-VM.bat - Cria a VM base de testes do PDV (roda ELEVADO).
REM
REM  Clique com o BOTAO DIREITO -> "Executar como administrador".
REM  Ele chama o New-PDVTestVM.ps1 apontando para a ISO x64 ja baixada em qa\isos.
REM
REM  Ao final, a VM "PDV-Test-VM" e criada e LIGADA para voce instalar o Windows.
REM ============================================================================

net session >nul 2>&1
if %errorLevel% neq 0 (
    echo.
    echo [ERRO] Rode como ADMINISTRADOR (botao direito -^> Executar como administrador).
    echo.
    pause
    exit /b 1
)

set "SCRIPT=%~dp0New-PDVTestVM.ps1"
set "ISO=%~dp0isos\Win11_25H2_BrazilianPortuguese_x64_v2.iso"

if not exist "%ISO%" (
    echo [ERRO] ISO nao encontrada em: %ISO%
    pause
    exit /b 1
)

echo === Criando a VM base "PDV-Test-VM" a partir da ISO x64 ===
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT%" -IsoPath "%ISO%"

echo.
echo ============================================================================
echo  PROXIMO PASSO: a janela do Hyper-V vai abrir a VM. Instale o Windows,
echo  crie o usuario LOCAL "pdv" com senha "pdv", e deixe a VM logada nesse usuario.
echo  Depois, rode: qa\Finalizar-VM.bat  (tambem como administrador).
echo ============================================================================
pause
