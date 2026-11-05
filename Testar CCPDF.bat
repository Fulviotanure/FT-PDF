@echo off
title CCPDF - Teste dos Arquivos Brutos (.NET 10)
set "PATH=C:\Program Files\dotnet;C:\Users\Fulvio\AppData\Local\Microsoft\dotnet;%PATH%"

taskkill /F /IM CCPDF.exe 2>nul

if exist "%~dp0ccpdf" (
    cd /d "%~dp0ccpdf"
) else if exist "C:\Users\fulvi\Desktop\PROJETOS\CCPDF\ccpdf" (
    cd /d "C:\Users\fulvi\Desktop\PROJETOS\CCPDF\ccpdf"
) else (
    cd /d "%USERPROFILE%\Desktop\PROJETOS\CCPDF\ccpdf"
)

echo ================================================================
echo       CCPDF - Teste Local dos Arquivos Brutos
echo ================================================================
echo.
dotnet run -- %*
echo.
if %ERRORLEVEL% NEQ 0 (
    echo Ocorreu uma interrupcao na execucao com codigo %ERRORLEVEL%.
) else (
    echo Execucao concluida.
)
pause

