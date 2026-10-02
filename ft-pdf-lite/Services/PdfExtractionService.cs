using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace FtPdfLite.Services
{
    public class PdfDocumentProperties
    {
        public string FileName { get; set; } = string.Empty;
        public string FileSize { get; set; } = string.Empty;
        public string PdfVersion { get; set; } = string.Empty;
        public string Title { get; set; } = "Não informado";
        public string Author { get; set; } = "Não informado";
        public string Producer { get; set; } = "Não informado";
        public string Creator { get; set; } = "Não informado";
        public string CreationDate { get; set; } = "Não informado";
        public string ModificationDate { get; set; } = "Não informado";
        public string PageDimensions { get; set; } = string.Empty;
        public string PageOrientation { get; set; } = "Retrato";
        public string Security { get; set; } = "Sem restrições";
    }

    public class IntegrityReport
    {
        public double IntegrityScore { get; set; } = 100.0;
        public string IntegrityStatus { get; set; } = "Alta Integridade";
        public string DocumentType { get; set; } = "Texto Vetorial Nativo";
        public string ImportVerdict { get; set; } = "DOCUMENTO IMPORTÁVEL";
        public string ImportVerdictColor { get; set; } = "#10B981"; // Green
        public int TotalCharacters { get; set; }
        public int TotalWords { get; set; }
        public int TotalPages { get; set; }
        public int StrangeCharactersCount { get; set; }
        public List<string> StrangeCharactersSamples { get; set; } = new();
        public int ScannedPagesCount { get; set; }
        public int TotalImagesFound { get; set; }
        public int SmallInlineImagesCount { get; set; }
        public int AnomalousInlineImagesCount { get; set; }
        public bool HasAnomalousImages { get; set; }
        public bool HasMissingNegativeAmountsInImages
        {
            get => HasAnomalousImages;
            set => HasAnomalousImages = value;
        }
        public int MissingDebitLinesCount { get; set; }
        public List<string> MissingDebitLineSamples { get; set; } = new();
        public List<string> DiscrepancySamples { get; set; } = new();
        public string FormattingQuality { get; set; } = "Bem Formatado";
        public List<string> DiagnosticWarnings { get; set; } = new();
        public List<string> OcrDiscoveredValues { get; set; } = new();
        public List<string> OcrDiscoveredHistories { get; set; } = new();
        public List<string> OcrDiscoveredHeaders { get; set; } = new();
        public List<string> OcrDiscoveredCodes { get; set; } = new();
    }

    public class ExtractionResult
    {
        public string FormattedText { get; set; } = string.Empty;
        public string RawText { get; set; } = string.Empty;
        public IntegrityReport Report { get; set; } = new();
        public PdfDocumentProperties Properties { get; set; } = new();
    }

    public class PdfExtractionService
    {
        private static readonly HashSet<string> CommonRecognizedWords = new(StringComparer.OrdinalIgnoreCase)
        {
            // Brazilian Banking & Financial Terms
            "banco", "comprovante", "pagamento", "pagamentos", "extrato", "saldo", "fatura", "cartao", "cartão",
            "cliente", "agencia", "agência", "conta", "valor", "data", "total", "segunda", "via", "atendimento",
            "descricao", "descrição", "referencia", "referência", "documento", "transferencia", "transferência",
            "beneficiario", "beneficiário", "pagador", "autenticacao", "autenticação", "real", "reais", "emissao",
            "emissão", "vencimento", "codigo", "código", "barras", "linha", "digitavel", "digitável", "debito",
            "débito", "credito", "crédito", "dinheiro", "cheque", "deposito", "depósito", "pix", "ted", "doc",
            "titulos", "títulos", "movimentacao", "movimentação", "posicao", "posição", "carteira", "folha",
            "financeiro", "empresa", "cnpj", "cpf", "sacado", "cedente", "operacao", "operação", "recibo",
            "juros", "multa", "desconto", "abatimento",
            // Portuguese Stop Words & Common Terms
            "de", "da", "do", "das", "dos", "para", "com", "em", "por", "um", "uma", "uns", "umas",
            "no", "na", "nos", "nas", "ao", "aos", "ou", "se", "que", "este", "esta", "seu", "sua",
            "pelo", "pela", "pelos", "pelas", "sobre", "entre", "mais", "como", "nao", "não",
            "mes", "mês", "ano", "dia", "hora", "numero", "número", "nome", "periodo", "período",
            // Common English Document Words
            "bank", "payment", "statement", "invoice", "receipt", "account", "date", "amount", "total",
            "credit", "debit", "balance", "customer", "client", "number", "the", "and", "for", "with", "from"
        };

        private static readonly Regex CurrencyRegex = new(@"R\$\s*\d{1,3}(?:\.\d{3})*,\d{2}|\b\d{1,3}(?:\.\d{3})*,\d{2}\b", RegexOptions.Compiled);
        private static readonly Regex NegativeCurrencyRegex = new(@"(?:-\s*R\$|R\$\s*[-–—]|[-–—]\s*R\$\s*\d|\(\s*R\$\s*\d|\(\s*\d{1,3}(?:\.\d{3})*,\d{2}\s*\)|-\s*\d{1,3}(?:\.\d{3})*,\d{2}\b)", RegexOptions.Compiled);

        private static readonly string[] FinancialStatementKeywords = new[]
        {
            "extrato", "saldo", "banco", "tarifa", "cobrança", "cobranca", "saída", "saida", "entrada",
            "pix", "maquininha", "stone", "pagamento", "transferência", "transferencia", "ted", "doc",
            "débito", "debito", "crédito", "credito", "instituição de pagamento", "instituicao de pagamento",
            "conta corrente", "conta de pagamento", "reserva stone"
        };

        private static readonly string[] DebitOrFeeKeywords = new[]
        {
            "tarifa", "cobrança de tarifa", "cobranca de tarifa", "tarifa de", "taxa pix", "taxa maquininha",
            "taxa", "débito", "debito", "saída", "saida", "estorno", "iof", "anuidade", "encargos"
        };

        public ExtractionResult ExtractAndAnalyze(string filePath, string? password = null)
        {
            var result = new ExtractionResult();
            var report = result.Report;
            var props = result.Properties;

            if (!File.Exists(filePath))
            {
                report.IntegrityScore = 0;
                report.IntegrityStatus = "Arquivo não encontrado";
                report.DocumentType = "Arquivo Inacessível";
                report.ImportVerdict = "DOCUMENTO NÃO IMPORTÁVEL";
                report.ImportVerdictColor = "#EF4444";
                report.DiagnosticWarnings.Add("O arquivo PDF especificado não existe.");
                return result;
            }

            var fileInfo = new FileInfo(filePath);
            props.FileName = fileInfo.Name;
            props.FileSize = FormatBytes(fileInfo.Length);

            var formattedBuilder = new StringBuilder();
            var rawBuilder = new StringBuilder();

            var strangeCharSet = new HashSet<char>();
            int totalLetters = 0;
            int alphaCount = 0;
            int digitCount = 0;
            int symbolCount = 0;
            int strangeChars = 0;
            int totalWords = 0;
            int validWordCount = 0;
            int recognizedWordHits = 0;
            int scannedPages = 0;
            int totalImages = 0;
            int totalSmallInlineImages = 0;
            int anomalousInlineImagesCount = 0;
            var sampleDiscrepancies = new List<string>();
            int ocrChecksPerformed = 0;
            const int MaxOcrChecksPerDoc = 30;
            int debitLinesWithSingleAmountCount = 0;
            int normalTransactionLinesWithTwoAmountsCount = 0;
            int negativeAmountsInTextCount = 0;
            int financialTermHits = 0;
            var sampleMissingDebitLines = new List<string>();
            int brokenLineCount = 0;
            int totalLines = 0;

            try
            {
                var parsingOptions = new ParsingOptions();
                if (!string.IsNullOrEmpty(password))
                {
                    parsingOptions.Password = password;
                }

                using var document = PdfDocument.Open(filePath, parsingOptions);
                report.TotalPages = document.NumberOfPages;
                props.PdfVersion = $"PDF {document.Version:0.0}";
                props.Security = document.IsEncrypted 
                    ? (!string.IsNullOrEmpty(password) ? "Criptografado / Protegido (Desbloqueado com Senha)" : "Criptografado / Protegido") 
                    : "Sem restrições (Livre)";

                // Extract Document Metadata
                var info = document.Information;
                if (!string.IsNullOrWhiteSpace(info.Title)) props.Title = info.Title;
                if (!string.IsNullOrWhiteSpace(info.Author)) props.Author = info.Author;
                if (!string.IsNullOrWhiteSpace(info.Producer)) props.Producer = info.Producer;
                if (!string.IsNullOrWhiteSpace(info.Creator)) props.Creator = info.Creator;
                if (!string.IsNullOrWhiteSpace(info.CreationDate)) props.CreationDate = FormatPdfDate(info.CreationDate);
                if (!string.IsNullOrWhiteSpace(info.ModifiedDate)) props.ModificationDate = FormatPdfDate(info.ModifiedDate);

                for (int i = 1; i <= document.NumberOfPages; i++)
                {
                    var page = document.GetPage(i);
                    var rawLetters = page.Letters.ToList();
                    var letters = DeduplicateOverprintedLetters(rawLetters);
                    var words = ExtractWordsFromLetters(letters);
                    var images = page.GetImages().ToList();
                    totalImages += images.Count;
                    int pageOcrChecks = 0;

                    var pageLines = ExtractPageLines(words);

                    // Análise espacial e dimensional de imagens na página
                    foreach (var img in images)
                    {
                        var box = img.Bounds;
                        double w = box.Width;
                        double h = box.Height;

                        // Verifica se é imagem de fundo/escaneada que ocupa a página toda
                        bool isPageScan = (w >= page.Width * 0.75 && h >= page.Height * 0.75);
                        if (isPageScan)
                            continue;

                        // Verifica se a página está em orientação paisagem (horizontal) ou retrato (vertical)
                        bool isLandscape = page.Width > page.Height;
                        double headerRatio = isLandscape ? 0.74 : 0.82;
                        double footerRatio = 0.09;

                        // Verifica se é cabeçalho (topo institucional/emitente) ou rodapé
                        bool isHeaderOrFooter = (box.Bottom > page.Height * headerRatio || box.Top < page.Height * footerRatio);
                        if (isHeaderOrFooter)
                            continue;

                        // Banners largos, faixas divisórias horizontais ou contêineres de gráficos (não são valores de dados)
                        bool isWideBannerOrChart = (w > page.Width * 0.50 || (w > 200 && h > 28));
                        if (isWideBannerOrChart)
                            continue;

                        // Calhas das margens externas (logos de banco na margem esquerda/direita, carimbos laterais)
                        bool isOuterMargin = (box.Right <= 48 || box.Left >= page.Width - 48);
                        if (isOuterMargin)
                            continue;

                        // Logotipos, emblemas ou carimbos de dimensões amplas (linhas de tabelas medem 8 a 16pt de altura)
                        // Uma imagem com altura >= 30pt e área >= 1400pt² é uma marca/logo institucional, não uma célula de tabela
                        bool isLogoOrEmblem = (h >= 30 && w >= 35 && (w * h) >= 1400);
                        if (isLogoOrEmblem)
                            continue;

                        // Códigos de barras (Chave de Acesso da NF-e, boletos ou guias)
                        bool isBarcode = (w >= 120 && h <= 45 && (w / Math.Max(1, h)) >= 3.5);
                        if (isBarcode)
                            continue;

                        // Dimensões típicas de elementos de conteúdo intercalados (valores, status, códigos, ícones)
                        bool isContentDimension = (h >= 2 && h <= 90 && w >= 2 && w <= 250);

                        if (isContentDimension)
                        {
                            totalSmallInlineImages++;

                            if (pageLines.Count > 0)
                            {
                                // Verifica se a imagem compartilha alinhamento vertical com alguma linha de texto
                                // e está contida horizontalmente no fluxo de texto da coluna
                                var alignedLine = pageLines.FirstOrDefault(l =>
                                    box.Bottom <= l.Top + 4 && box.Top >= l.Bottom - 4 &&
                                    ((box.Left >= l.Left - 15 && box.Left <= l.Right + 15) ||
                                     (box.Right >= l.Left - 15 && box.Right <= l.Right + 15)));

                                if (alignedLine != null)
                                {
                                    // Mini inteligência: Verificação profunda via OCR Nativo do Windows
                                    string? ocrText = null;
                                    bool ranOcr = false;
                                    if (WindowsOcrHelper.IsAvailable && ocrChecksPerformed < MaxOcrChecksPerDoc && pageOcrChecks < 5)
                                    {
                                        byte[]? rawImgBytes = null;
                                        if (img.TryGetPng(out byte[] pngData))
                                        {
                                            rawImgBytes = pngData;
                                        }
                                        else
                                        {
                                            try { rawImgBytes = img.RawBytes.ToArray(); } catch { }
                                        }

                                        if (rawImgBytes != null && rawImgBytes.Length > 0)
                                        {
                                            ocrChecksPerformed++;
                                            pageOcrChecks++;
                                            ranOcr = true;
                                            ocrText = WindowsOcrHelper.RecognizeText(rawImgBytes);
                                        }
                                    }

                                    if (ranOcr)
                                    {
                                        var ocrResult = WindowsOcrHelper.ClassifyOcrText(ocrText, w, h);

                                        // Se o OCR confirmou que é logotipo/marca institucional: descartar como anomalia
                                        if (ocrResult.Category == OcrContentCategory.InstitutionalLogo)
                                        {
                                            continue;
                                        }

                                        // Se a imagem não possui nenhum texto/dado legível: é ícone, bullet gráfico ou traço decorativo
                                        if (ocrResult.Category == OcrContentCategory.None)
                                        {
                                            continue;
                                        }

                                        // Se for cabeçalho de coluna/tabela (ex: "DESCRIÇÃO", "TIPO", "SALDO")
                                        if (ocrResult.Category == OcrContentCategory.ColumnHeaderOrLabel)
                                        {
                                            if (!report.OcrDiscoveredHeaders.Contains(ocrResult.CleanText) && report.OcrDiscoveredHeaders.Count < 8)
                                            {
                                                report.OcrDiscoveredHeaders.Add(ocrResult.CleanText);
                                            }
                                            continue;
                                        }

                                        // Se for código / data / referência
                                        if (ocrResult.Category == OcrContentCategory.CodeOrDateOrRef)
                                        {
                                            if (!report.OcrDiscoveredCodes.Contains(ocrResult.CleanText) && report.OcrDiscoveredCodes.Count < 6)
                                            {
                                                report.OcrDiscoveredCodes.Add(ocrResult.CleanText);
                                            }
                                        }

                                        // Se for valor monetário ou histórico transacional: discrepância real confirmada!
                                        anomalousInlineImagesCount++;

                                        if (ocrResult.Category == OcrContentCategory.MonetaryValue)
                                        {
                                            if (!report.OcrDiscoveredValues.Contains(ocrResult.CleanText) && report.OcrDiscoveredValues.Count < 10)
                                            {
                                                report.OcrDiscoveredValues.Add(ocrResult.CleanText);
                                            }
                                        }
                                        else if (ocrResult.Category == OcrContentCategory.TransactionalHistory)
                                        {
                                            if (!report.OcrDiscoveredHistories.Contains(ocrResult.CleanText) && report.OcrDiscoveredHistories.Count < 8)
                                            {
                                                report.OcrDiscoveredHistories.Add(ocrResult.CleanText);
                                            }
                                        }

                                        if (sampleDiscrepancies.Count < 4)
                                        {
                                            string preview = alignedLine.Text.Trim();
                                            if (preview.Length > 50) preview = preview.Substring(0, 50) + "...";

                                            string positionDesc = (box.Left >= alignedLine.Right - 8) ? "à direita" : ((box.Right <= alignedLine.Left + 8) ? "à esquerda" : "intercalada no corpo do texto");
                                            string natureDesc = $"[{ocrResult.CategoryDisplayName}]: \"{ocrResult.CleanText}\"";
                                            sampleDiscrepancies.Add($"Pág. {i}: Linha \"{preview}\" possui imagem {positionDesc} ({natureDesc}).");
                                        }
                                    }
                                    else
                                    {
                                        // Sem execução de OCR (budget excedido): se já confirmamos valores no documento e dimensões batem com valores
                                        if (report.OcrDiscoveredValues.Count > 0 && h >= 4 && h <= 35 && w >= 15 && w <= 220)
                                        {
                                            anomalousInlineImagesCount++;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // Get dimensions of first page
                    if (i == 1)
                    {
                        double widthMm = page.Width * 0.352778;
                        double heightMm = page.Height * 0.352778;
                        props.PageDimensions = $"{widthMm:0.0} x {heightMm:0.0} mm ({page.Width:0} x {page.Height:0} pt)";
                        props.PageOrientation = page.Width > page.Height ? "Paisagem (Horizontal)" : "Retrato (Vertical)";
                    }

                    // Detect scanned page: minimal/no vector letters, but contains images, or image covers full page
                    bool isFullPageScan = images.Any(img => img.Bounds.Width >= page.Width * 0.75 && img.Bounds.Height >= page.Height * 0.75);
                    if (isFullPageScan || (letters.Count < 20 && images.Count > 0))
                    {
                        scannedPages++;
                    }

                    // Raw text extraction for this page
                    var pageRawText = string.Concat(letters.Select(l => l.Value));
                    rawBuilder.AppendLine($"--- [PÁGINA {i} de {report.TotalPages}] ---");
                    rawBuilder.AppendLine(pageRawText);
                    rawBuilder.AppendLine();

                    // Formatted layout text extraction
                    var pageFormattedText = ExtractFormattedPageText(words);
                    formattedBuilder.AppendLine($"--- [PÁGINA {i} de {report.TotalPages}] ---");
                    formattedBuilder.AppendLine(pageFormattedText);
                    formattedBuilder.AppendLine();

                    // Analyze characters on page
                    foreach (var letter in letters)
                    {
                        string val = letter.Value;
                        foreach (char c in val)
                        {
                            totalLetters++;

                            if (char.IsLetter(c))
                            {
                                alphaCount++;
                            }
                            else if (char.IsDigit(c))
                            {
                                digitCount++;
                            }
                            else if (!char.IsWhiteSpace(c))
                            {
                                symbolCount++;
                            }

                            if (IsStrangeOrCorruptCharacter(c))
                            {
                                strangeChars++;
                                if (strangeCharSet.Count < 10 && !char.IsWhiteSpace(c))
                                {
                                    strangeCharSet.Add(c);
                                }
                            }
                        }
                    }

                    // Analyze words and linguistic validity
                    totalWords += words.Count;

                    foreach (var word in words)
                    {
                        string cleanWord = Regex.Replace(word.Text, @"[^\w]", "");
                        if (cleanWord.Length >= 2 && CommonRecognizedWords.Contains(cleanWord))
                        {
                            recognizedWordHits++;
                        }

                        if (IsValidLinguisticWord(word.Text))
                        {
                            validWordCount++;
                        }
                    }

                    var lines = pageFormattedText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    totalLines += lines.Length;
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (trimmed.Length > 0 && trimmed.Length < 15 && !trimmed.EndsWith(".") && !trimmed.EndsWith(":") && !trimmed.EndsWith(";"))
                        {
                            brokenLineCount++;
                        }

                        if (trimmed.Length == 0) continue;

                        // Check financial keywords
                        for (int k = 0; k < FinancialStatementKeywords.Length; k++)
                        {
                            if (trimmed.IndexOf(FinancialStatementKeywords[k], StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                financialTermHits++;
                            }
                        }

                        // Check negative amounts
                        if (NegativeCurrencyRegex.IsMatch(trimmed))
                        {
                            negativeAmountsInTextCount++;
                        }

                        // Check transaction line amounts
                        var currencyMatches = CurrencyRegex.Matches(trimmed);
                        if (currencyMatches.Count >= 2)
                        {
                            normalTransactionLinesWithTwoAmountsCount++;
                        }
                        else if (currencyMatches.Count == 1)
                        {
                            bool hasDebitWord = false;
                            for (int d = 0; d < DebitOrFeeKeywords.Length; d++)
                            {
                                if (trimmed.IndexOf(DebitOrFeeKeywords[d], StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    hasDebitWord = true;
                                    break;
                                }
                            }

                            if (hasDebitWord)
                            {
                                debitLinesWithSingleAmountCount++;
                                if (sampleMissingDebitLines.Count < 3)
                                {
                                    sampleMissingDebitLines.Add(trimmed);
                                }
                            }
                        }
                    }
                }

                report.TotalCharacters = totalLetters;
                report.TotalWords = totalWords;
                report.TotalImagesFound = totalImages;
                report.SmallInlineImagesCount = totalSmallInlineImages;
                report.AnomalousInlineImagesCount = anomalousInlineImagesCount;
                report.DiscrepancySamples = sampleDiscrepancies;
                report.MissingDebitLinesCount = debitLinesWithSingleAmountCount;
                report.MissingDebitLineSamples = sampleMissingDebitLines;
                report.ScannedPagesCount = scannedPages;
                report.StrangeCharactersCount = strangeChars;
                report.StrangeCharactersSamples = strangeCharSet.Select(c => $"'{c}' (U+{(int)c:X4})").ToList();

                result.FormattedText = formattedBuilder.ToString().TrimEnd();
                result.RawText = rawBuilder.ToString().TrimEnd();

                // Compute Ratios for Intelligent Scramble/Cipher/Image Detection
                double alphaRatio = totalLetters > 0 ? (double)alphaCount / totalLetters : 0.0;
                double symbolRatio = totalLetters > 0 ? (double)symbolCount / totalLetters : 0.0;
                double validWordRatio = totalWords > 0 ? (double)validWordCount / totalWords : 0.0;
                double avgCharsPerWord = totalWords > 0 ? (double)totalLetters / totalWords : 0.0;

                bool isScannedDocument = false;
                bool isScrambledOrEncrypted = false;

                // 1. Check Scanned / Image PDF
                if (report.TotalPages > 0 && scannedPages >= report.TotalPages)
                {
                    isScannedDocument = true;
                }
                else if (totalLetters < 30 && totalImages > 0)
                {
                    isScannedDocument = true;
                }

                // 2. Check Scrambled / Obfuscated / Missing Font Encoding
                // Genuine scramble or missing ToUnicode occurs when:
                // - High density of truly corrupt/unmapped characters exist (strangeChars like U+FFFD or control codes), OR
                // - The document has plenty of text, but lacks coherent linguistic words, has ZERO recognized dictionary words, and displays gibberish / extreme symbol noise.
                if (!isScannedDocument && totalLetters > 40)
                {
                    double strangeRatio = (double)strangeChars / totalLetters;
                    bool hasSevereStrangeChars = strangeRatio > 0.15 && strangeChars > 15;
                    bool isGibberishWithoutRecognizedWords = recognizedWordHits == 0 && totalWords > 15 && validWordRatio < 0.15;
                    bool isExtremelyCorruptSymbols = recognizedWordHits == 0 && symbolRatio > 0.60 && alphaRatio < 0.10;

                    if (hasSevereStrangeChars || isGibberishWithoutRecognizedWords || isExtremelyCorruptSymbols)
                    {
                        isScrambledOrEncrypted = true;
                    }
                }

                // 3. Verificação de Imagens Anômalas embutidas no fluxo de texto/tabelas
                // Documentos com discrepâncias estruturais reais possuem imagens inline substituindo dados/valores/ícones
                string rawTextSample = rawBuilder.Length > 8000 ? rawBuilder.ToString(0, 8000) : rawBuilder.ToString();
                bool isFinancialDoc = financialTermHits >= 3 || 
                                     rawTextSample.Contains("extrato", StringComparison.OrdinalIgnoreCase) ||
                                     rawTextSample.Contains("saldo", StringComparison.OrdinalIgnoreCase) ||
                                     rawTextSample.Contains("Stone", StringComparison.OrdinalIgnoreCase);

                bool hasDetectedDiscrepancies = !isScannedDocument && !isScrambledOrEncrypted && totalLetters > 80 && (
                    anomalousInlineImagesCount >= 2 ||
                    (anomalousInlineImagesCount >= 1 && (sampleDiscrepancies.Count > 0 || (isFinancialDoc && debitLinesWithSingleAmountCount >= 1)))
                );

                // Evaluate Score & Verdict
                if (isScannedDocument)
                {
                    report.IntegrityScore = 0.0;
                    report.IntegrityStatus = "0% - Não Legível";
                    report.DocumentType = "Documento Escaneado (Imagem)";
                    report.FormattingQuality = "Sem Texto Vetorial (Imagem)";
                    report.ImportVerdict = "DOCUMENTO NÃO IMPORTÁVEL";
                    report.ImportVerdictColor = "#EF4444"; // Red
                    report.DiagnosticWarnings.Add("Documento composto por imagens escaneadas sem camada de texto digital (necessita OCR).");
                }
                else if (isScrambledOrEncrypted)
                {
                    report.IntegrityScore = 0.0;
                    report.IntegrityStatus = "0% - Ilegível";
                    report.DocumentType = "Documento Criptografado ou Codificação Quebrada";
                    report.FormattingQuality = "Texto Quebrado / Embaralhado";
                    report.ImportVerdict = "DOCUMENTO NÃO IMPORTÁVEL";
                    report.ImportVerdictColor = "#EF4444"; // Red
                    report.DiagnosticWarnings.Add("Fontes com codificação embutida sem mapeamento ToUnicode (letras/sinais embaralhados e não decodificáveis).");
                    if (strangeChars > 0)
                    {
                        report.DiagnosticWarnings.Add($"Detectada alta densidade de caracteres anômalos ou ilegíveis ({strangeChars} ocorrência(s)).");
                    }
                    else
                    {
                        report.DiagnosticWarnings.Add("Texto extraído sem coerência linguística ou palavras decodificáveis.");
                    }
                }
                else if (totalLetters == 0)
                {
                    report.IntegrityScore = 0.0;
                    report.IntegrityStatus = "0% - Vazio";
                    report.DocumentType = "PDF Sem Informações de Texto";
                    report.FormattingQuality = "Vazio";
                    report.ImportVerdict = "DOCUMENTO NÃO IMPORTÁVEL";
                    report.ImportVerdictColor = "#EF4444";
                    report.DiagnosticWarnings.Add("Nenhum caractere de texto legível foi encontrado no arquivo.");
                }
                else if (hasDetectedDiscrepancies)
                {
                    double penalty = Math.Max(anomalousInlineImagesCount * 4.0, totalSmallInlineImages * 1.5);
                    double score = Math.Clamp(Math.Round(100.0 - penalty, 1), 30.0, 55.0);

                    report.IntegrityScore = score;
                    report.HasAnomalousImages = true;
                    report.IntegrityStatus = $"{score:0.0}% - Discrepâncias Visuais (Imagens no Texto)";
                    report.DocumentType = "Documento Híbrido (Texto com Elementos em Imagem)";
                    report.FormattingQuality = "Imagens Intercaladas no Conteúdo";
                    report.ImportVerdict = "DOCUMENTO NÃO IMPORTÁVEL (DADOS EM FIGURA)";
                    report.ImportVerdictColor = "#EF4444"; // Red

                    double avgPerPg = report.TotalPages > 0 ? (double)totalImages / report.TotalPages : 0;
                    
                    if (anomalousInlineImagesCount > 0)
                    {
                        report.DiagnosticWarnings.Add($"⚠️ Discrepância Estrutural Detectada: Foram encontradas {anomalousInlineImagesCount} imagens com dados no fluxo de texto/tabelas ({totalImages} imagens no total do documento, média de {avgPerPg:0.0} por página).");
                    }
                    else
                    {
                        report.DiagnosticWarnings.Add($"⚠️ Alta Concentração de Imagens: Foram detectadas {totalImages} figuras/imagens no documento ({avgPerPg:0.0} por página) intercaladas com texto vetorial.");
                    }

                    if (report.OcrDiscoveredValues.Count > 0)
                    {
                        string samples = string.Join(", ", report.OcrDiscoveredValues.Take(6).Select(v => $"\"{v}\""));
                        report.DiagnosticWarnings.Add($"⚠️ OCR Identificou Valores Monetários em Figuras: Foram detectadas ocorrências de valores contábeis gravados como imagem (Exemplos: {samples}). Esses valores NÃO constam na camada de texto vetorial e podem ser omitidos na importação contábil.");
                    }

                    if (report.OcrDiscoveredHistories.Count > 0)
                    {
                        string samples = string.Join(", ", report.OcrDiscoveredHistories.Take(5).Select(h => $"\"{h}\""));
                        report.DiagnosticWarnings.Add($"⚠️ OCR Identificou Históricos em Figuras: Foram detectadas descrições transacionais gravadas como imagem (Exemplos: {samples}).");
                    }

                    if (report.OcrDiscoveredHeaders.Count > 0)
                    {
                        string samples = string.Join(", ", report.OcrDiscoveredHeaders.Take(6).Select(h => $"\"{h}\""));
                        report.DiagnosticWarnings.Add($"ℹ️ Rótulos de Cabeçalho em Figura: Rótulos/colunas detectados como imagem pelo OCR: {samples}.");
                    }

                    report.DiagnosticWarnings.Add("Risco de Dados Faltantes: Elementos visuais (como valores, tarifas, status, códigos, assinaturas ou ícones) foram gerados como figura e NÃO constam na camada de texto. Esses campos não serão extraídos e podem causar lançamentos faltantes na importação.");

                    if (sampleDiscrepancies.Count > 0)
                    {
                        foreach (var sample in sampleDiscrepancies)
                        {
                            report.DiagnosticWarnings.Add($"Discrepância visual localizada: {sample}");
                        }
                    }

                    if (isFinancialDoc && sampleMissingDebitLines.Count > 0)
                    {
                        report.DiagnosticWarnings.Add($"Exemplo de lançamento com valor ausente no texto: \"{sampleMissingDebitLines[0]}\"");
                    }

                    report.DiagnosticWarnings.Add("Orientação: Compare a visualização do PDF com o texto extraído no Bloco de Notas ao lado. Caso ocorram divergências na importação, solicite o documento em formato de dados (OFX, Excel/XLSX, CSV) ou PDF vetorial gerado diretamente pelo sistema emissor.");
                }
                else
                {
                    // Calculate real text score
                    double score = 100.0;

                    // Mixed scanned pages
                    if (scannedPages > 0)
                    {
                        double scannedRatio = (double)scannedPages / report.TotalPages;
                        score -= (scannedRatio * 50.0);
                        report.DocumentType = "Misto (Texto + Páginas Escaneadas)";
                        report.DiagnosticWarnings.Add($"{scannedPages} de {report.TotalPages} página(s) são imagens sem texto direto.");
                    }

                    // Strange character penalties
                    if (strangeChars > 0)
                    {
                        double strangeRatio = (double)strangeChars / totalLetters;
                        score -= Math.Min(35.0, strangeRatio * 350.0);
                        report.DiagnosticWarnings.Add($"Detectados {strangeChars} caractere(s) estranho(s) ou discrepantes.");
                    }

                    // Broken line / fragmentation penalties
                    if (totalLines > 5)
                    {
                        double brokenRatio = (double)brokenLineCount / totalLines;
                        if (brokenRatio > 0.45)
                        {
                            score -= 15.0;
                            report.FormattingQuality = "Texto Quebrado / Fragmentado";
                            report.DiagnosticWarnings.Add("Muitas linhas com quebras irregulares ou palavras truncadas.");
                        }
                        else if (brokenRatio > 0.25)
                        {
                            score -= 8.0;
                            report.FormattingQuality = "Moderadamente Quebrado";
                            report.DiagnosticWarnings.Add("Alguns parágrafos possuem quebras de linha irregulares.");
                        }
                        else
                        {
                            report.FormattingQuality = "Bem Formatado";
                        }
                    }

                    // Discrepant words/letters ratio
                    if (avgCharsPerWord > 18.0) // Extremely long glued tokens
                    {
                        score -= 15.0;
                        report.FormattingQuality = "Texto com Palavras Coladas";
                        report.DiagnosticWarnings.Add("Muitos caracteres com pouca separação de palavras (tokens anormalmente longos).");
                    }

                    score = Math.Clamp(Math.Round(score, 1), 0.0, 100.0);
                    report.IntegrityScore = score;

                    // Set Import Verdict based on Score: strictly < 100% triggers attention/warning
                    if (score >= 100.0)
                    {
                        report.IntegrityStatus = "100% - Integridade Perfeita";
                        report.DocumentType = "Texto Vetorial Nativo";
                        report.ImportVerdict = "DOCUMENTO IMPORTÁVEL";
                        report.ImportVerdictColor = "#10B981"; // Green
                    }
                    else if (score >= 70.0)
                    {
                        report.IntegrityStatus = "Atenção - Abaixo de 100%";
                        report.DocumentType = "Texto com Pequenas Discrepâncias";
                        report.ImportVerdict = "DOCUMENTO IMPORTÁVEL, PORÉM PODE CONTER FALHAS";
                        report.ImportVerdictColor = "#F59E0B"; // Yellow/Orange
                        report.DiagnosticWarnings.Insert(0, "Atenção: integridade abaixo de 100% - o documento pode importar com erros ou falhas.");
                    }
                    else if (score >= 35.0)
                    {
                        report.IntegrityStatus = "Baixa Integridade";
                        report.DocumentType = "Texto com Ruído / Itens Faltantes";
                        report.ImportVerdict = "DOCUMENTO IMPORTÁVEL, PORÉM PODE CONTER FALHAS";
                        report.ImportVerdictColor = "#F97316"; // Orange
                        report.DiagnosticWarnings.Insert(0, "Atenção: baixa integridade estrutural - o arquivo pode importar com erros ou partes truncadas.");
                    }
                    else
                    {
                        report.IntegrityStatus = "Integridade Crítica";
                        report.DocumentType = "Texto Severamente Danificado";
                        report.ImportVerdict = "DOCUMENTO NÃO IMPORTÁVEL";
                        report.ImportVerdictColor = "#EF4444"; // Red
                    }
                }

                if (report.DiagnosticWarnings.Count == 0)
                {
                    report.DiagnosticWarnings.Add("Texto vetorial nativo bem estruturado, limpo e legível.");
                }
            }
            catch (Exception ex)
            {
                report.IntegrityScore = 0.0;
                report.IntegrityStatus = "0% - Protegido / Erro";
                report.DocumentType = !string.IsNullOrEmpty(password) ? "Documento Protegido por Senha" : "Arquivo Corrompido / Ilegível";
                report.ImportVerdict = !string.IsNullOrEmpty(password) ? "DOCUMENTO IMPORTÁVEL, PORÉM PODE CONTER FALHAS" : "DOCUMENTO NÃO IMPORTÁVEL";
                report.ImportVerdictColor = !string.IsNullOrEmpty(password) ? "#3B82F6" : "#EF4444";
                report.DiagnosticWarnings.Add($"Aviso de extração de texto via PdfPig: {ex.Message}");
                result.FormattedText = !string.IsNullOrEmpty(password) 
                    ? $"[Documento protegido por senha desbloqueado no visualizador nativo. Extração avançada de texto via PdfPig: {ex.Message}]"
                    : $"[Erro ao extrair texto do documento: {ex.Message}]";
                result.RawText = result.FormattedText;
            }

            return result;
        }

        private class ExtractedWord
        {
            public string Text { get; set; } = string.Empty;
            public double Left { get; set; }
            public double Right { get; set; }
            public double Bottom { get; set; }
            public double Top { get; set; }
            public double Height => Top - Bottom;
        }

        private static List<ExtractedWord> ExtractWordsFromLetters(IReadOnlyList<Letter> letters)
        {
            var result = new List<ExtractedWord>();
            if (letters == null || letters.Count == 0) return result;

            var lineGroups = new List<List<Letter>>();
            var sorted = letters.OrderByDescending(l => l.GlyphRectangle.Bottom).ThenBy(l => l.GlyphRectangle.Left).ToList();

            foreach (var l in sorted)
            {
                bool added = false;
                foreach (var lg in lineGroups)
                {
                    if (Math.Abs(lg[0].GlyphRectangle.Bottom - l.GlyphRectangle.Bottom) < 4.0)
                    {
                        lg.Add(l);
                        added = true;
                        break;
                    }
                }
                if (!added)
                {
                    lineGroups.Add(new List<Letter> { l });
                }
            }

            foreach (var lg in lineGroups.OrderByDescending(g => g[0].GlyphRectangle.Bottom))
            {
                var ordered = lg.OrderBy(l => l.GlyphRectangle.Left).ToList();
                ExtractedWord? currentWord = null;

                for (int i = 0; i < ordered.Count; i++)
                {
                    var l = ordered[i];
                    if (string.IsNullOrWhiteSpace(l.Value))
                    {
                        if (currentWord != null)
                        {
                            result.Add(currentWord);
                            currentWord = null;
                        }
                        continue;
                    }

                    if (currentWord != null)
                    {
                        double gap = l.GlyphRectangle.Left - currentWord.Right;
                        double spaceThreshold = Math.Max(2.0, l.PointSize * 0.22);
                        if (gap > spaceThreshold)
                        {
                            result.Add(currentWord);
                            currentWord = null;
                        }
                    }

                    if (currentWord == null)
                    {
                        currentWord = new ExtractedWord
                        {
                            Text = l.Value,
                            Left = l.GlyphRectangle.Left,
                            Right = l.GlyphRectangle.Right,
                            Bottom = l.GlyphRectangle.Bottom,
                            Top = l.GlyphRectangle.Top
                        };
                    }
                    else
                    {
                        currentWord.Text += l.Value;
                        currentWord.Right = Math.Max(currentWord.Right, l.GlyphRectangle.Right);
                        currentWord.Top = Math.Max(currentWord.Top, l.GlyphRectangle.Top);
                        currentWord.Bottom = Math.Min(currentWord.Bottom, l.GlyphRectangle.Bottom);
                    }
                }

                if (currentWord != null)
                {
                    result.Add(currentWord);
                }
            }

            return result;
        }

        private class PageLineInfo
        {
            public double Bottom { get; set; }
            public double Top { get; set; }
            public double Left { get; set; }
            public double Right { get; set; }
            public string Text { get; set; } = string.Empty;
        }

        private static List<PageLineInfo> ExtractPageLines(IReadOnlyList<ExtractedWord> words)
        {
            if (words == null || words.Count == 0) return new List<PageLineInfo>();

            var lineGroups = new List<List<ExtractedWord>>();
            var sortedWords = words.OrderByDescending(w => w.Bottom).ThenBy(w => w.Left).ToList();

            foreach (var word in sortedWords)
            {
                bool added = false;
                foreach (var line in lineGroups)
                {
                    var firstWord = line[0];
                    if (Math.Abs(firstWord.Bottom - word.Bottom) < 4.5)
                    {
                        line.Add(word);
                        added = true;
                        break;
                    }
                }

                if (!added)
                {
                    lineGroups.Add(new List<ExtractedWord> { word });
                }
            }

            var result = new List<PageLineInfo>();
            foreach (var line in lineGroups)
            {
                var ordered = line.OrderBy(w => w.Left).ToList();
                result.Add(new PageLineInfo
                {
                    Bottom = ordered.Min(w => w.Bottom),
                    Top = ordered.Max(w => w.Top),
                    Left = ordered.Min(w => w.Left),
                    Right = ordered.Max(w => w.Right),
                    Text = string.Join(" ", ordered.Select(w => w.Text))
                });
            }

            return result;
        }

        private static string ExtractFormattedPageText(IReadOnlyList<ExtractedWord> words)
        {
            if (words == null || words.Count == 0) return string.Empty;

            var lineGroups = new List<List<ExtractedWord>>();
            var sortedWords = words.OrderByDescending(w => w.Bottom).ThenBy(w => w.Left).ToList();

            foreach (var word in sortedWords)
            {
                bool added = false;
                foreach (var line in lineGroups)
                {
                    var firstWordInLine = line[0];
                    if (Math.Abs(firstWordInLine.Bottom - word.Bottom) < 4.5)
                    {
                        line.Add(word);
                        added = true;
                        break;
                    }
                }

                if (!added)
                {
                    lineGroups.Add(new List<ExtractedWord> { word });
                }
            }

            lineGroups = lineGroups.OrderByDescending(l => l[0].Bottom).ToList();

            var sb = new StringBuilder();
            double lastY = -1;

            foreach (var line in lineGroups)
            {
                var orderedLine = line.OrderBy(w => w.Left).ToList();

                if (lastY > 0)
                {
                    double lineGap = lastY - orderedLine[0].Bottom;
                    if (lineGap > (orderedLine[0].Height * 1.8))
                    {
                        sb.AppendLine();
                    }
                }

                lastY = orderedLine[0].Bottom;

                for (int w = 0; w < orderedLine.Count; w++)
                {
                    sb.Append(orderedLine[w].Text);
                    if (w < orderedLine.Count - 1)
                    {
                        sb.Append(" ");
                    }
                }
                sb.AppendLine();
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Deduplica caracteres gerados com falso negrito ("fake bold" / double-strike / overprinting).
        /// Muitos geradores de PDF e drivers de impressão que não possuem a variante em negrito da fonte
        /// desenham o mesmo glifo 2 ou mais vezes com deslocamento horizontal de ~0.3pt.
        /// </summary>
        private static List<Letter> DeduplicateOverprintedLetters(IReadOnlyList<Letter> letters)
        {
            if (letters == null || letters.Count <= 1) return letters?.ToList() ?? new List<Letter>();

            var result = new List<Letter>(letters.Count);
            var buckets = new Dictionary<int, List<Letter>>();

            for (int i = 0; i < letters.Count; i++)
            {
                var current = letters[i];
                if (string.IsNullOrEmpty(current.Value) || char.IsWhiteSpace(current.Value[0]))
                {
                    result.Add(current);
                    continue;
                }

                double curBottom = current.GlyphRectangle.Bottom;
                double curLeft = current.GlyphRectangle.Left;
                int yKey = (int)Math.Round(curBottom);

                bool isDuplicate = false;

                // Checa buckets Y vizinhos (mesma linha ou borda próxima)
                for (int k = yKey - 1; k <= yKey + 1; k++)
                {
                    if (buckets.TryGetValue(k, out var candidates))
                    {
                        for (int j = 0; j < candidates.Count; j++)
                        {
                            var prev = candidates[j];
                            if (prev.Value == current.Value &&
                                Math.Abs(prev.GlyphRectangle.Bottom - curBottom) <= 0.6 &&
                                Math.Abs(prev.GlyphRectangle.Left - curLeft) <= 0.8)
                            {
                                isDuplicate = true;
                                break;
                            }
                        }
                        if (isDuplicate) break;
                    }
                }

                if (!isDuplicate)
                {
                    result.Add(current);
                    if (!buckets.TryGetValue(yKey, out var list))
                    {
                        list = new List<Letter>();
                        buckets[yKey] = list;
                    }
                    list.Add(current);
                }
            }

            return result;
        }

        private static bool IsValidLinguisticWord(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = text.Trim();

            // Single vowels (a, e, o, à) are valid words in Portuguese
            if (text.Length == 1)
            {
                return "aeiouyáéíóúàâêô".Contains(char.ToLower(text[0]));
            }

            // Pure digits or dates/currency tokens (e.g. 12.848,05 or 26/06/2026 or 14:22:04)
            if (Regex.IsMatch(text, @"^[\d\.,\/\-\:]+$")) return true;

            // Check for at least one vowel in alphabetic words
            bool hasVowel = Regex.IsMatch(text, @"[aeiouyáéíóúâêîôûãõàäëïöü]", RegexOptions.IgnoreCase);
            bool hasLetters = text.Any(char.IsLetter);

            if (hasLetters && hasVowel)
            {
                int symbolCount = text.Count(c => !char.IsLetterOrDigit(c));
                return symbolCount <= (text.Length / 2);
            }

            // Common Brazilian business/financial acronyms (without vowels or state codes)
            if (Regex.IsMatch(text, @"^(CPF|CNPJ|TED|DOC|STR|CIP|DDA|PIX|BB|BRL|S\/A|S\.A\.|LTDA|EIRELI|ME|EPP|[A-Z]{2})$", RegexOptions.IgnoreCase))
            {
                return true;
            }

            // Account / agency codes with check digit (e.g. 2290-X, 74.784-X)
            if (Regex.IsMatch(text, @"^\d+[\.\-][\dX]$", RegexOptions.IgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static bool IsStrangeOrCorruptCharacter(char c)
        {
            if (c == '\uFFFD' || c == '\u0000') return true;
            if (char.IsControl(c) && c != '\t' && c != '\n' && c != '\r') return true;
            if (c >= 0xE000 && c <= 0xF8FF) return true;
            return false;
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{(bytes / 1024.0):0.0} KB";
            return $"{(bytes / (1024.0 * 1024.0)):0.00} MB";
        }

        private static string FormatPdfDate(string rawDate)
        {
            if (string.IsNullOrWhiteSpace(rawDate)) return "Não informado";
            if (rawDate.StartsWith("D:") && rawDate.Length >= 10)
            {
                string year = rawDate.Substring(2, 4);
                string month = rawDate.Substring(6, 2);
                string day = rawDate.Substring(8, 2);
                return $"{day}/{month}/{year}";
            }
            return rawDate;
        }
    }
}
