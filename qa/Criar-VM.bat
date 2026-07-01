@echo off
setlocal
cd /d "%~dp0"

net session >nul 2>&1
if %errorLevel% neq 0 goto naoadmin

set "ISO=%~dp0isos\Win11_25H2_BrazilianPortuguese_x64_v2.iso"
if not exist "%ISO%" goto semiso

echo Criando a VM base PDV-Test-VM a partir da ISO x64...
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0New-PDVTestVM.ps1" -IsoPath "%ISO%"
echo.
echo Proximo passo: instale o Windows na VM pelo Hyper-V Manager,
echo crie o usuario local pdv com senha pdv, e deixe a VM logada.
echo Depois rode Finalizar-VM.bat como administrador.
goto fim

:naoadmin
echo [ERRO] Rode como ADMINISTRADOR: botao direito, Executar como administrador.
goto fim

:semiso
echo [ERRO] ISO nao encontrada: %ISO%
goto fim

:fim
echo.
pause
