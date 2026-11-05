# CCPDF (Arquivo Único Standalone)

Leitor, Validador de Integridade e Extrator de Documentos PDF ultraleve desenvolvido em **C# (.NET 10 WPF)**, distribuído como **executável único standalone (Single-File Self-Contained)** sem necessidade de instalação prévia do .NET.

> *Nota: O CCPDF é a evolução direta do projeto anteriormente denominado FT PDF Lite.*

---

## ⚡ Principais Características

- **Executável Único (Standalone):** Roda direto com dois cliques (`CCPDF.exe`), sem necessidade de instalar runtimes do .NET.
- **Motor PDFium Integrado:** Renderização nativa vetorial com aceleração de hardware e renderização cristalina.
- **🌿 Tema Claro Verde Pastel Suave & Modo Escuro Moderno:** Interface projetada para conforto visual prolongado em tarefas fiscais e contábeis.
- **☀️🌙 Ícones Vetoriais:** Barra de título moderna com alternância visual elegante de tema.
- **🔍 Diagnóstico de Integridade e Validação:**
  - Classificação visual padronizada:
    - 🟢 `DOCUMENTO IMPORTÁVEL` (PDF com texto vetorial íntegro e legível).
    - 🟡 `DOCUMENTO IMPORTÁVEL, PORÉM PODE CONTER FALHAS` (Alerta para microimagens de valores negativos ou cabeçalhos em curvas).
    - 🔴 `DOCUMENTO NÃO IMPORTÁVEL` (Documento digitalizado/rasterizado sem camada de texto).
  - Banner informativo padrão com diretrizes de retaguarda.
- **Visualizador Moderno Multiabas:** Abas simultâneas, zoom suave, arrastar e soltar (drag & drop) e PDFs com senha.
- **📝 Bloco de Notas Integrado:** Extração instantânea de texto preservando layout ou texto puro com busca rápida (`Ctrl+F`).

---

## 📁 Estrutura do Repositório

```text
CCPDF/
├── CCPDF.slnx                       # Solution (.NET 10)
├── ccpdf/                           # Código-fonte do CCPDF
│   ├── CCPDF.csproj
│   ├── App.xaml / App.xaml.cs
│   ├── MainWindow.xaml / .cs
│   ├── SettingsWindow.xaml / .cs
│   ├── Services/                    # Extração, diagnóstico e integração PDFium
│   ├── Models/                      # Modelos de dados e diagnóstico
│   ├── Native/                      # pdfium.dll embutida como recurso
│   └── Assets/                      # Ícones e logotipos
├── Testar CCPDF.bat                 # Executa direto os arquivos brutos para testes
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
Testar CCPDF.bat
```
Ele executa via `dotnet run` diretamente o código fonte atual, permitindo validações imediatas sem compilar.

### 2. Compilar Executável Localmente
Execute:
```cmd
compilar_local.bat
```
Informe a versão desejada (ex: `2.6.0`). O executável standalone será criado em:
`compilacoes/v2.6.0/CCPDF.exe`

### 3. Lançamento Oficial (GitHub Actions)
Execute:
```cmd
publicar_versao.bat
```
Após confirmação, a tag Git será criada e enviada ao GitHub, disparando o workflow do GitHub Actions que compilará e anexará os executáveis (`CCPDF.exe`, `FtPdfLite.exe` para compatibilidade retroativa) e arquivos ZIP na aba **Releases**.
