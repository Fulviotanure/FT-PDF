@echo off
title FT PDF - Teste dos Arquivos Brutos (.NET 10)
set "PATH=C:\Users\Fulvio\AppData\Local\Microsoft\dotnet;%PATH%"
cd /d "E:\PROJETOS\FT-PDF\_projetos_futuros\ft-pdf"
echo ================================================================
echo       FT PDF (Edicao Completa) - Teste Local dos Arquivos Brutos
echo ================================================================
echo.
dotnet run
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Ocorreu uma interrupcao na execucao.
    pause
)
