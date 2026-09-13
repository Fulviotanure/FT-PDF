@echo off
chcp 65001 > nul
setlocal enabledelayedexpansion

echo =============================================================
echo        COMPILADOR LOCAL FT PDF (Zero Dependência .NET)
echo =============================================================
echo.
set /p VER="Digite a versão para compilar (ex: v2.1.0 ou 2.1.0): "
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
echo [1/3] Criando pasta de destino: %OUTDIR%
if not exist "%OUTDIR%" mkdir "%OUTDIR%"

echo.
echo [2/3] Compilando FT PDF (Edição Completa) - SingleFile Self-Contained...
dotnet publish _projetos_futuros/ft-pdf/FtPdf.csproj -c Release -r win-x64 -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true --self-contained true -o "%OUTDIR%"
if errorlevel 1 (
    echo [ERRO] Falha ao compilar FT PDF.
    pause
    exit /b 1
)

echo.
echo [3/3] Compilando FT PDF Lite (Edição Ultraleve) - SingleFile Self-Contained...
dotnet publish ft-pdf-lite/FtPdfLite.csproj -c Release -r win-x64 -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true --self-contained true -o "%OUTDIR%"
if errorlevel 1 (
    echo [ERRO] Falha ao compilar FT PDF Lite.
    pause
    exit /b 1
)

echo.
echo =============================================================
echo 🎉 SUCESSO! Executáveis gerados com sucesso em:
echo    %OUTDIR%\FtPdf.exe
echo    %OUTDIR%\FtPdfLite.exe
echo.
echo Agora você pode testar diretamente na pasta 'compilacoes\%VER%\'!
echo Quando estiver pronto para lançar ao público, use 'publicar_versao.bat'.
echo =============================================================
echo.
pause
