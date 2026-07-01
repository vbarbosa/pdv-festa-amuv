@echo off
setlocal
cd /d "%~dp0"

net session >nul 2>&1
if %errorLevel% neq 0 goto naoadmin

echo Corrigindo boot da VM PDV-Test-VM (remonta a ISO e forca boot pelo DVD)...
echo.
powershell -NoProfile -ExecutionPolicy Bypass -Command "$vm='PDV-Test-VM'; $iso='%~dp0isos\Win11_25H2_BrazilianPortuguese_x64_v2.iso'; if((Get-VM $vm).State -ne 'Off'){Stop-VM $vm -Force; Start-Sleep 4}; Get-VMDvdDrive $vm | Remove-VMDvdDrive; Add-VMDvdDrive -VMName $vm -Path $iso; $dvd=Get-VMDvdDrive $vm; Set-VMFirmware $vm -FirstBootDevice $dvd; Write-Host 'DVD remontado e definido como 1o boot. Ligando...'; Start-VM $vm; Write-Host 'VM ligada. VA RAPIDO ao Hyper-V Manager e aperte uma tecla no Press any key.'"
goto fim

:naoadmin
echo [ERRO] Rode como ADMINISTRADOR.

:fim
echo.
pause
