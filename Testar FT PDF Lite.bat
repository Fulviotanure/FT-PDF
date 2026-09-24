@echo off
title FT PDF Lite - Teste dos Arquivos Brutos (.NET 10)
set "PATH=C:\Users\Fulvio\AppData\Local\Microsoft\dotnet;%PATH%"

taskkill /F /IM FtPdfLite.exe 2>nul

if exist "%~dp0ft-pdf-lite" (
    cd /d "%~dp0ft-pdf-lite"
) else if exist "%USERPROFILE%\Documents\FT-PDF\ft-pdf-lite" (
    cd /d "%USERPROFILE%\Documents\FT-PDF\ft-pdf-lite"
) else (
    cd /d "E:\PROJETOS\FT-PDF\ft-pdf-lite"
)

echo ================================================================
echo       FT PDF Lite (Edicao Ultraleve) - Teste Local dos Arquivos Brutos
echo ================================================================
echo.
dotnet run
echo.
if %ERRORLEVEL% NEQ 0 (
    echo Ocorreu uma interrupcao na execucao com codigo %ERRORLEVEL%.
) else (
    echo Execucao concluida.
)
pause
