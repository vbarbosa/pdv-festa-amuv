@echo off
setlocal
cd /d "%~dp0"

net session >nul 2>&1
if %errorLevel% neq 0 goto naoadmin

echo === Rodando a bateria E2E na SANDBOX (Hyper-V) - HEADLESS ===
echo A VM roda em BACKGROUND (sem janela) para nao atrapalhar seu PC.
echo O ciclo: restaura Base-Limpa, injeta o app+testes, instala, roda FlaUI,
echo extrai screenshots para test-reports, e REVERTE a VM ao estado limpo.
echo NAO abra o Hyper-V Manager / conexao da VM enquanto roda.
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Run-HyperVTests.ps1"
goto fim

:naoadmin
echo [ERRO] Rode como ADMINISTRADOR.

:fim
echo.
echo Relatorio (screenshots + logs) em: ..\test-reports\
pause
