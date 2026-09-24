@echo off
title FT PDF - Teste dos Arquivos Brutos (.NET 10)
set "PATH=C:\Users\Fulvio\AppData\Local\Microsoft\dotnet;%PATH%"

taskkill /F /IM FtPdf.exe 2>nul

if exist "%~dp0_projetos_futuros\ft-pdf" (
    cd /d "%~dp0_projetos_futuros\ft-pdf"
) else if exist "%USERPROFILE%\Documents\FT-PDF\_projetos_futuros\ft-pdf" (
    cd /d "%USERPROFILE%\Documents\FT-PDF\_projetos_futuros\ft-pdf"
) else (
    cd /d "E:\PROJETOS\FT-PDF\_projetos_futuros\ft-pdf"
)

echo ================================================================
echo       FT PDF (Edicao Completa) - Teste Local dos Arquivos Brutos
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
