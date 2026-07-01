@echo off
setlocal
cd /d "%~dp0"

net session >nul 2>&1
if %errorLevel% neq 0 goto naoadmin

echo Este passo LIGA a VM, instala o .NET SDK dentro dela e RECONGELA o checkpoint.
echo IMPORTANTE: quando a VM ligar, faca login como o usuario "pdv" (senha "pdv")
echo e deixe na area de trabalho. O script espera a VM responder.
echo.
pause

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0New-PDVTestVM.ps1" -Finalizar -GuestUser pdv -GuestPass pdv
goto fim

:naoadmin
echo [ERRO] Rode como ADMINISTRADOR.

:fim
echo.
echo Se apareceu ".NET SDK na VM: instalado" e "Checkpoint Base-Limpa criado", esta pronto.
pause
