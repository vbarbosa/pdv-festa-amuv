@echo off
setlocal
cd /d "%~dp0"

net session >nul 2>&1
if %errorLevel% neq 0 goto naoadmin

echo Finalizando a VM (Guest Services + checkpoint base)...
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0New-PDVTestVM.ps1" -Finalizar -GuestUser pdv -GuestPass pdv
echo.
echo Se apareceu "Checkpoint Base-Limpa criado", a VM esta pronta.
echo Volte ao chat: eu rodo a bateria E2E na sandbox.
goto fim

:naoadmin
echo [ERRO] Rode como ADMINISTRADOR: botao direito, Executar como administrador.

:fim
echo.
pause
