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
        public string ImportVerdict { get; set; } = "O arquivo importa";
        public string ImportVerdictColor { get; set; } = "#10B981"; // Green
        public int TotalCharacters { get; set; }
        public int TotalWords { get; set; }
        public int TotalPages { get; set; }
        public int StrangeCharactersCount { get; set; }
        public List<string> StrangeCharactersSamples { get; set; } = new();
        public int ScannedPagesCount { get; set; }
        public int TotalImagesFound { get; set; }
        public string FormattingQuality { get; set; } = "Bem Formatado";
        public List<string> DiagnosticWarnings { get; set; } = new();
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
                report.ImportVerdict = "Arquivo não importável";
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
                    var letters = page.Letters.ToList();
                    var images = page.GetImages().ToList();
                    totalImages += images.Count;

                    // Get dimensions of first page
                    if (i == 1)
                    {
                        double widthMm = page.Width * 0.352778;
                        double heightMm = page.Height * 0.352778;
                        props.PageDimensions = $"{widthMm:0.0} x {heightMm:0.0} mm ({page.Width:0} x {page.Height:0} pt)";
                        props.PageOrientation = page.Width > page.Height ? "Paisagem (Horizontal)" : "Retrato (Vertical)";
                    }

                    // Detect scanned page: minimal/no vector letters, but contains images
                    if (letters.Count < 20 && images.Count > 0)
                    {
                        scannedPages++;
                    }

                    // Raw text extraction for this page
                    var pageRawText = string.Concat(letters.Select(l => l.Value));
                    rawBuilder.AppendLine($"--- [PÁGINA {i}] ---");
                    rawBuilder.AppendLine(pageRawText);
                    rawBuilder.AppendLine();

                    // Formatted layout text extraction
                    var pageFormattedText = ExtractFormattedPageText(page);
                    formattedBuilder.AppendLine($"--- [PÁGINA {i}] ---");
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
                    var words = page.GetWords().ToList();
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
                    }
                }

                report.TotalCharacters = totalLetters;
                report.TotalWords = totalWords;
                report.TotalImagesFound = totalImages;
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

                // Evaluate Score & Verdict
                if (isScannedDocument)
                {
                    report.IntegrityScore = 0.0;
                    report.IntegrityStatus = "0% - Não Legível";
                    report.DocumentType = "Documento Escaneado (Imagem)";
                    report.FormattingQuality = "Sem Texto Vetorial (Imagem)";
                    report.ImportVerdict = "Arquivo não importável";
                    report.ImportVerdictColor = "#EF4444"; // Red
                    report.DiagnosticWarnings.Add("Documento composto por imagens escaneadas sem camada de texto digital (necessita OCR).");
                }
                else if (isScrambledOrEncrypted)
                {
                    report.IntegrityScore = 0.0;
                    report.IntegrityStatus = "0% - Ilegível";
                    report.DocumentType = "Documento Criptografado ou Codificação Quebrada";
                    report.FormattingQuality = "Texto Quebrado / Embaralhado";
                    report.ImportVerdict = "Arquivo não importável";
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
                    report.ImportVerdict = "Arquivo não importável";
                    report.ImportVerdictColor = "#EF4444";
                    report.DiagnosticWarnings.Add("Nenhum caractere de texto legível foi encontrado no arquivo.");
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
                        report.ImportVerdict = "O arquivo importa";
                        report.ImportVerdictColor = "#10B981"; // Green
                    }
                    else if (score >= 70.0)
                    {
                        report.IntegrityStatus = "Atenção - Abaixo de 100%";
                        report.DocumentType = "Texto com Pequenas Discrepâncias";
                        report.ImportVerdict = "Atenção: o arquivo pode importar com erros";
                        report.ImportVerdictColor = "#F59E0B"; // Yellow/Orange
                        report.DiagnosticWarnings.Insert(0, "Atenção: integridade abaixo de 100% - o documento pode importar com erros ou falhas.");
                    }
                    else if (score >= 35.0)
                    {
                        report.IntegrityStatus = "Baixa Integridade";
                        report.DocumentType = "Texto com Ruído / Itens Faltantes";
                        report.ImportVerdict = "Atenção: grandes chances de erro na importação";
                        report.ImportVerdictColor = "#F97316"; // Orange
                        report.DiagnosticWarnings.Insert(0, "Atenção: baixa integridade estrutural - o arquivo pode importar com erros ou partes truncadas.");
                    }
                    else
                    {
                        report.IntegrityStatus = "Integridade Crítica";
                        report.DocumentType = "Texto Severamente Danificado";
                        report.ImportVerdict = "Arquivo não importável";
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
                report.ImportVerdict = !string.IsNullOrEmpty(password) ? "Visualização ativa via Pdfium" : "Arquivo não importável";
                report.ImportVerdictColor = !string.IsNullOrEmpty(password) ? "#3B82F6" : "#EF4444";
                report.DiagnosticWarnings.Add($"Aviso de extração de texto via PdfPig: {ex.Message}");
                result.FormattedText = !string.IsNullOrEmpty(password) 
                    ? $"[Documento protegido por senha desbloqueado no visualizador nativo. Extração avançada de texto via PdfPig: {ex.Message}]"
                    : $"[Erro ao extrair texto do documento: {ex.Message}]";
                result.RawText = result.FormattedText;
            }

            return result;
        }

        private static string ExtractFormattedPageText(Page page)
        {
            var words = page.GetWords().ToList();
            if (words.Count == 0)
            {
                return page.Text;
            }

            var lines = new List<List<Word>>();
            var sortedWords = words.OrderByDescending(w => w.BoundingBox.Bottom).ThenBy(w => w.BoundingBox.Left).ToList();

            foreach (var word in sortedWords)
            {
                bool added = false;
                foreach (var line in lines)
                {
                    var firstWordInLine = line[0];
                    if (Math.Abs(firstWordInLine.BoundingBox.Bottom - word.BoundingBox.Bottom) < 4.5)
                    {
                        line.Add(word);
                        added = true;
                        break;
                    }
                }

                if (!added)
                {
                    lines.Add(new List<Word> { word });
                }
            }

            lines = lines.OrderByDescending(l => l[0].BoundingBox.Bottom).ToList();

            var sb = new StringBuilder();
            double lastY = -1;

            foreach (var line in lines)
            {
                var orderedLine = line.OrderBy(w => w.BoundingBox.Left).ToList();

                if (lastY > 0)
                {
                    double lineGap = lastY - orderedLine[0].BoundingBox.Bottom;
                    if (lineGap > (orderedLine[0].BoundingBox.Height * 1.8))
                    {
                        sb.AppendLine();
                    }
                }

                lastY = orderedLine[0].BoundingBox.Bottom;

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
