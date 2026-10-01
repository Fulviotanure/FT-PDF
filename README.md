# FT PDF Lite (Arquivo Único)

Leitor, Validador de Integridade e Extrator de Documentos PDF ultraleve desenvolvido em **C# (.NET 10 WPF)**, distribuído como **executável único standalone (Single-File Self-Contained)** sem necessidade de instalação prévia do .NET.

---

## ⚡ Principais Características

- **Executável Único (Standalone):** Roda direto com dois cliques (`FtPdfLite.exe`), sem necessidade de instalar runtimes do .NET.
- **Motor PDFium Integrado:** Renderização nativa de alta fidelidade e aceleração gráfica.
- **🔍 Diagnóstico de Integridade e Validação:**
  - Classificação visual padronizada:
    - 🟢 `DOCUMENTO IMPORTÁVEL` (PDF com texto vetorial íntegro e legível).
    - 🟡 `DOCUMENTO IMPORTÁVEL, PORÉM PODE CONTER FALHAS` (Alerta para microimagens de valores negativos ou possíveis falhas de leitura).
    - 🔴 `DOCUMENTO NÃO IMPORTÁVEL` (Documento digitalizado/rasterizado sem camada de texto).
  - Banner informativo padrão: *"Atenção: caso mesmo como importável ele não importe, envie para a retaguarda."*
- **Visualizador Moderno Multiabas:** Suporte a abas simultâneas, zoom suave, arrastar e soltar (drag & drop) e PDFs protegidos por senha.
- **Bloco de Notas Integrado:** Extração instantânea de texto preservando layout ou texto puro.

---

## 📁 Estrutura do Repositório

```text
FT-PDF/
├── FtPdf.slnx                       # Solution (.NET 10)
├── ft-pdf-lite/                     # Código-fonte do FT PDF Lite
│   ├── FtPdfLite.csproj
│   ├── App.xaml / App.xaml.cs
│   ├── MainWindow.xaml / .cs
│   ├── SettingsWindow.xaml / .cs
│   ├── Services/                    # Extração, diagnóstico e integração PDFium
│   ├── Models/                      # Modelos de dados e diagnóstico
│   ├── Native/                      # pdfium.dll embutida como recurso
│   └── Assets/                      # Ícones e logotipos
├── Testar FT PDF Lite.bat           # Executa direto os arquivos brutos para testes
├── compilar_local.bat               # Compilação local em Single-File na pasta compilacoes/
├── publicar_versao.bat              # Script interativo para gerar tag e acionar GitHub Actions
└── .github/workflows/
    └── release-ft-pdf-lite.yml      # CI/CD automatizado no GitHub Actions (Single-File)
```

---

## 🛠️ Como Testar e Compilar

### 1. Testar Arquivos Brutos (Desenvolvimento)
Execute o script:
```cmd
Testar FT PDF Lite.bat
```
Ele executa via `dotnet run` diretamente o código fonte atual, permitindo validações imediatas sem compilar.

### 2. Compilar Executável Localmente
Execute:
```cmd
compilar_local.bat
```
Informe a versão desejada (ex: `2.4.2`). O executável standalone será criado em:
`compilacoes/v2.4.2/FtPdfLite.exe`

### 3. Lançamento Oficial (GitHub Actions)
Execute:
```cmd
publicar_versao.bat
```
Após confirmação, a tag Git será criada e enviada ao GitHub, disparando o workflow do GitHub Actions que compilará e anexará o executável único (`FtPdfLite.exe`) e o arquivo ZIP na aba **Releases**.
