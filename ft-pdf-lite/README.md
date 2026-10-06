# FT PDF Lite - Leitor e Validador de Documentos PDF

Versão ultraleve, rápida e minimalista do **FT PDF** focada exclusivamente em **leitura vetorial nativa e validação de integridade de arquivos PDF**.

---

## 🚀 Funcionalidades

- **📄 Leitor Vetorial Nativo Puro**:
  - Renderização ultranítida por GPU/DirectX sem conversão para imagens.
  - Rolagem fluida e zoom vetorial infinito sem perda de qualidade.
  - Seleção nativa de texto com clique e arraste (`Ctrl+C`, `Ctrl+F`, `Ctrl+P`).
- **📑 Abas Integradas**:
  - Abra múltiplos documentos simultaneamente na barra de título integrada.
- **🔍 Validação de Integridade e Diagnósticos**:
  - Classificação do arquivo (Nativo, Imagem/Scan, Criptografado/Quebrado).
  - Score de integridade (0% a 100%).
  - Veredito claro de importação do arquivo.
  - Detecção de sinais estranhos, caracteres corrompidos e densidade de texto.
  - Metadados e propriedades completas do PDF (Autor, Versão, Dimensões, Segurança).
- **📝 Bloco de Notas Integrado**:
  - Extração com layout preservado ou texto cru (*raw*).
  - Cópia com 1 clique e exportação para `.txt`.

---

## 📦 Versão 2.5.1
- **Alerta de Arquivo Convertido**: Identificação clara de PDFs escaneados com camada de texto por baixo, orientando envio para a retaguarda em caso de inconsistência de valores.
- **Feedback Visual de Abertura Imediato**: Overlay moderno com cálculo dinâmico do tamanho em MB, detecção de arquivo grande e barra de progresso.
- **Abertura Fluida via Duplo Clique / Programa Padrão**: Renderização antecipada da interface gráfica no Windows, evitando a impressão de aplicativo travado ao abrir PDFs diretamente do explorer.
- **Lazy Loading de Miniaturas de Páginas**: Carregamento instantâneo da primeira página em arquivos volumosos com painel lateral de miniaturas recolhido.
- **OCR Otimizado para Arquivos Grandes**: Orçamento inteligente de OCR e barra de progresso em tempo real durante a análise.

---

## 📦 Versão 2.5.0
- **Mini Inteligência OCR Nativa (Windows Media OCR)**: Inspeção inteligente de imagens embutidas em alta performance sem dependências externas (0 MB de peso extra).
- **Classificação Precisa de Elementos Visuais**: Diferenciação automática entre valores monetários, históricos transacionais, cabeçalhos de tabela e logotipos institucionais.
- **Detecção de Valores em Figuras com Citação Real**: Amostragem de valores capturados diretamente nas imagens (ex: `-R$ 0,11`, `-R$ 4,47`) com linha e página correspondentes.
- **Regras Contábeis de Encaminhamento**: Documentos não importáveis ou híbridos com perda de dados alertam claramente *"Atenção: documento não importável. Informe o cliente."*. O encaminhamento para a retaguarda é reservado estritamente a documentos sem perda de dados.
- **Contadores Separados de Diagnóstico**: Contagem independente de Total de Imagens e Páginas Escaneadas em qualquer documento.
- **Layout Anti-Sobreposição no Painel**: Porcentagem no topo com badge destacada, classificação com quebra automática de texto e balão de retaguarda independente.
- **Performance Otimizada**: Processamento rápido mesmo em extratos densos com centenas de páginas e milhares de elementos gráficos.
