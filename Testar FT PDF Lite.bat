@echo off
title FT PDF Lite - Teste dos Arquivos Brutos (.NET 10)
set "PATH=C:\Users\Fulvio\AppData\Local\Microsoft\dotnet;%PATH%"
cd /d "C:\Users\fulvi\Desktop\ft-pdf\ft-pdf-lite"
echo ================================================================
echo       FT PDF Lite (Edicao Ultraleve) - Teste Local dos Arquivos Brutos
echo ================================================================
echo.
dotnet run
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Ocorreu uma interrupcao na execucao.
    pause
)
