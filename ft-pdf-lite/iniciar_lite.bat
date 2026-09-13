@echo off
title FT PDF Lite - Executando em Modo de Teste (.NET 10 C#)
cd /d "%~dp0"
echo ========================================================
echo       Iniciando FT PDF Lite - Leitor Ultraleve (.NET 10)
echo ========================================================
echo.
dotnet run
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Ocorreu um erro ou o .NET SDK 10 precisa ser instalado.
    echo Baixe gratuitamente em: https://dotnet.microsoft.com/download/dotnet/10.0
    pause
)
