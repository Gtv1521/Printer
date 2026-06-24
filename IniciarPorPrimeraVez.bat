@echo off
:: Certifica que se ejecute como Administrador
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Por favor, ejecuta este archivo como ADMINISTRADOR (Clic derecho -> Ejecutar como administrador).
    pause
    exit
)

echo Configurando entorno SSL Local...
:: Genera el certificado usando el propio motor de tu ejecutable publicado
MiPrinterApp.exe --urls=https://localhost:5181
pause