@echo off
REM ============================================================================
REM  Finalizar-VM.bat - Configura a VM DEPOIS de voce instalar o Windows nela.
REM
REM  Pre-requisito: a VM "PDV-Test-VM" ja tem Windows instalado, com um usuario
REM  LOCAL "pdv" (senha "pdv") criado e LOGADO.
REM
REM  Clique com o BOTAO DIREITO -> "Executar como administrador".
REM  Habilita Guest Services, testa o PowerShell Direct e cria o checkpoint base.
REM ============================================================================

net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [ERRO] Rode como ADMINISTRADOR.
    pause
    exit /b 1
)

set "SCRIPT=%~dp0New-PDVTestVM.ps1"
powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT%" -Finalizar -GuestUser pdv -GuestPass pdv

echo.
echo ============================================================================
echo  Se apareceu "Checkpoint 'Base-Limpa' criado", a VM esta pronta.
echo  Agora volte ao chat: eu rodo a bateria E2E na sandbox.
echo ============================================================================
pause
