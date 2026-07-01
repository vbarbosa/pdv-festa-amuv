@echo off
REM ============================================================================
REM  Habilitar-HyperV-no-Home.bat
REM  Instala o Hyper-V no Windows 11 HOME (metodo NAO-OFICIAL da comunidade).
REM
REM  O Home nao expoe o Hyper-V no "Ativar recursos do Windows", mas os pacotes
REM  existem no sistema. Este .bat os instala via DISM.
REM
REM  COMO USAR:
REM    1. Clique com o BOTAO DIREITO neste arquivo -> "Executar como administrador".
REM    2. Aguarde a instalacao (varios pacotes; pode demorar alguns minutos).
REM    3. Reinicie o PC quando pedir (digite S).
REM
REM  NAO SUPORTADO pela Microsoft. Use por sua conta. Se algo der errado, o
REM  Hyper-V pode ser removido depois em "Ativar/desativar recursos do Windows".
REM ============================================================================

REM --- exige elevacao ---
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo.
    echo [ERRO] Este script precisa rodar como ADMINISTRADOR.
    echo        Feche esta janela, clique com o botao direito no .bat e escolha
    echo        "Executar como administrador".
    echo.
    pause
    exit /b 1
)

echo.
echo === Habilitando Hyper-V no Windows Home (metodo nao-oficial) ===
echo.

REM 1) Lista e instala TODOS os pacotes Hyper-V presentes no componente store.
pushd "%~dp0"
echo Coletando a lista de pacotes do Hyper-V...
dir /b %SystemRoot%\servicing\Packages\*Hyper-V*.mum > "%TEMP%\hyperv-pkgs.txt" 2>nul

echo Instalando os pacotes (isso pode demorar)...
for /f "usebackq tokens=*" %%p in ("%TEMP%\hyperv-pkgs.txt") do (
    echo   + %%p
    dism /online /norestart /add-package:"%SystemRoot%\servicing\Packages\%%p" >nul 2>&1
)

REM 2) Habilita o recurso opcional principal (agora que os pacotes estao presentes).
echo Habilitando o recurso Microsoft-Hyper-V-All...
dism /online /enable-feature /featurename:Microsoft-Hyper-V-All /limitaccess /all

echo.
echo === Concluido. E NECESSARIO REINICIAR o Windows para o Hyper-V funcionar. ===
echo.
choice /c SN /m "Reiniciar agora"
if errorlevel 2 (
    echo Reinicie manualmente depois. Apos o reboot, valide com:
    echo    Get-Command Get-VM   ^(no PowerShell^)
    popd
    pause
    exit /b 0
)
shutdown /r /t 5 /c "Reiniciando para ativar o Hyper-V"
popd
