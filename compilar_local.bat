@echo off
chcp 65001 > nul
setlocal enabledelayedexpansion

echo =============================================================
echo             COMPILADOR LOCAL CCPDF (Zero Dependência .NET)
echo =============================================================
echo.
set /p VER="Digite a versão para compilar (ex: v2.6.0 ou 2.6.0): "
if "%VER%"=="" (
    echo [ERRO] Versão não informada.
    pause
    exit /b 1
)

:: Garantir prefixo v
if not "%VER:~0,1%"=="v" (
    set "VER=v%VER%"
)

set "OUTDIR=compilacoes\%VER%"
echo.
echo [1/2] Criando pasta de destino: %OUTDIR%
if not exist "%OUTDIR%" mkdir "%OUTDIR%"

echo.
echo [2/2] Compilando CCPDF (Arquivo Único - Standalone Self-Contained)...
dotnet publish ccpdf/CCPDF.csproj -c Release -r win-x64 -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true --self-contained true -o "%OUTDIR%"
if errorlevel 1 (
    echo [ERRO] Falha ao compilar CCPDF.
    pause
    exit /b 1
)

echo.
echo =============================================================
echo 🎉 SUCESSO! Executável único gerado com sucesso em:
echo    %OUTDIR%\CCPDF.exe
echo.
echo Agora você pode testar diretamente na pasta 'compilacoes\%VER%\'!
echo Quando estiver pronto para lançar ao público, use 'publicar_versao.bat'.
echo =============================================================
echo.
pause
