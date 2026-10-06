using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;

namespace FtPdfLite.Services
{
    public static class WindowsOcrHelper
    {
        private static readonly Lazy<OcrEngine?> _ocrEngine = new(() =>
        {
            try
            {
                var engine = OcrEngine.TryCreateFromUserProfileLanguages();
                if (engine != null) return engine;

                if (OcrEngine.AvailableRecognizerLanguages.Count > 0)
                {
                    return OcrEngine.TryCreateFromLanguage(OcrEngine.AvailableRecognizerLanguages[0]);
                }
            }
            catch
            {
                // Windows.Media.Ocr indisponível ou não suportado
            }
            return null;
        });

        public static bool IsAvailable => _ocrEngine.Value != null;

        public static string? RecognizeText(byte[]? imageBytes)
        {
            if (!IsAvailable || imageBytes == null || imageBytes.Length == 0)
                return null;

            try
            {
                return Task.Run(() => RecognizeTextInternalAsync(imageBytes)).GetAwaiter().GetResult();
            }
            catch
            {
                return null;
            }
        }

        private static async Task<string?> RecognizeTextInternalAsync(byte[] imageBytes)
        {
            var engine = _ocrEngine.Value;
            if (engine == null) return null;

            try
            {
                using var stream = new InMemoryRandomAccessStream();
                await stream.WriteAsync(imageBytes.AsBuffer());
                stream.Seek(0);

                var decoder = await BitmapDecoder.CreateAsync(stream);
                var softwareBitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

                // Windows.Media.Ocr requer largura e altura entre 40 e 10.000 pixels.
                // Redimensionamos imagens muito pequenas (< 40px) ou excessivamente pesadas (> 2600px) para máxima performance e precisão.
                if (softwareBitmap.PixelWidth < 40 || softwareBitmap.PixelHeight < 40 ||
                    softwareBitmap.PixelWidth > 2600 || softwareBitmap.PixelHeight > 2600)
                {
                    using var srcMs = new MemoryStream(imageBytes);
                    using var originalBmp = new System.Drawing.Bitmap(srcMs);

                    int targetW = originalBmp.Width;
                    int targetH = originalBmp.Height;

                    if (targetW < 40 || targetH < 40)
                    {
                        targetW = Math.Max(targetW * 4, 80);
                        targetH = Math.Max(targetH * 4, 80);
                    }
                    else if (targetW > 2400 || targetH > 2400)
                    {
                        double scale = Math.Min(2400.0 / targetW, 2400.0 / targetH);
                        targetW = Math.Max(40, (int)(targetW * scale));
                        targetH = Math.Max(40, (int)(targetH * scale));
                    }

                    using var scaledBmp = new System.Drawing.Bitmap(targetW, targetH);
                    using (var g = System.Drawing.Graphics.FromImage(scaledBmp))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.Clear(System.Drawing.Color.White);
                        g.DrawImage(originalBmp, 0, 0, targetW, targetH);
                    }

                    using var scaledMs = new MemoryStream();
                    scaledBmp.Save(scaledMs, System.Drawing.Imaging.ImageFormat.Png);
                    scaledMs.Position = 0;

                    using var scaledStream = new InMemoryRandomAccessStream();
                    await scaledStream.WriteAsync(scaledMs.ToArray().AsBuffer());
                    scaledStream.Seek(0);

                    var scaledDecoder = await BitmapDecoder.CreateAsync(scaledStream);
                    softwareBitmap = await scaledDecoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                }

                var result = await engine.RecognizeAsync(softwareBitmap);
                return result?.Text?.Trim();
            }
            catch
            {
                return null;
            }
        }

        public static bool ContainsFinancialOrTabularData(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;

            // Contém dígitos numéricos formatados como moeda ou valores contábeis
            if (Regex.IsMatch(text, @"\b\d{1,3}(?:\.\d{3})*,\d{2}\b|\b\d+,\d{2}\b|R\$\s*\d|[-–—]\s*\d"))
                return true;

            // Palavras-chave típicas de dados financeiros que não deveriam estar em imagem
            string[] financialTokens = { "debito", "débito", "credito", "crédito", "tarifa", "saldo", "pago", "icms", "total", "iss" };
            foreach (var token in financialTokens)
            {
                if (text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        public static OcrClassificationResult ClassifyOcrText(string? text, double width, double height)
        {
            var res = new OcrClassificationResult();
            if (string.IsNullOrWhiteSpace(text))
            {
                return res;
            }

            string raw = text.Trim();
            res.RawText = raw;

            // Limpeza de ruídos comuns de OCR em fontes pequenas/rasterizadas
            string clean = raw;
            clean = Regex.Replace(clean, @"[Ññ]\$", "R$");
            clean = Regex.Replace(clean, @"¯\s*R\$", "-R$");
            clean = Regex.Replace(clean, @"¯\s*([0-9])", "-$1");
            clean = Regex.Replace(clean, @"—\s*o\.", "-0.");
            clean = Regex.Replace(clean, @"—\s*([0-9])", "-$1");
            clean = Regex.Replace(clean, @"-R\$fi", "-R$ 6");
            res.CleanText = clean;

            // 1. Valores monetários / Cifras / Débitos / Tarifas
            var currencyRegex = new Regex(@"(?:[-–—]\s*)?(?:R\$\s*|[$€£]\s*)\d+(?:[.,]\d+)*|[-–—]\s*\d+[.,]\d{2}|\b\d{1,3}(?:\.\d{3})*,\d{2}\b|\b\d+,\d{2}\b");
            if (currencyRegex.IsMatch(clean) || clean.StartsWith("-R$") || clean.StartsWith("R$"))
            {
                res.Category = OcrContentCategory.MonetaryValue;
                res.CategoryDisplayName = "Valor Monetário / Débito / Tarifa";
                return res;
            }

            // 2. Cabeçalhos de coluna e rótulos de campos de tabela
            var headerTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            { 
                "tipo", "saldo", "descrição", "descricao", "contraparte", "documento", 
                "instituição", "instituicao", "agencia", "agência", "conta", "data",
                "lançamento", "lancamento", "histórico", "historico", "valor" 
            };
            if (headerTokens.Contains(clean))
            {
                res.Category = OcrContentCategory.ColumnHeaderOrLabel;
                res.CategoryDisplayName = "Cabeçalho de Coluna / Rótulo de Campo";
                return res;
            }

            // 3. Logotipos institucionais e nomes de bancos/empresas
            var brandTokens = new[] { "stone", "itau", "itaú", "bradesco", "santander", "caixa", "orion", "nubank", "inter", "sicoob", "sicredi", "safra" };
            if (brandTokens.Any(b => clean.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0) && !clean.Any(char.IsDigit))
            {
                res.Category = OcrContentCategory.InstitutionalLogo;
                res.CategoryDisplayName = "Logotipo / Identificação Institucional";
                return res;
            }

            // 4. Códigos / Datas / Referências
            if (Regex.IsMatch(clean, @"\b\d{2}/\d{2}/\d{2,4}\b|\b\d{11,}\b"))
            {
                res.Category = OcrContentCategory.CodeOrDateOrRef;
                res.CategoryDisplayName = "Código / Data / Referência";
                return res;
            }

            // 5. Histórico e descrição de transações
            string[] historyTokens = { "saída", "saida", "entrada", "pix", "ted", "doc", "tarifa", "taxa", "pagamento", "transferência", "transferencia", "reserva", "depósito", "deposito", "estorno", "maquininha", "fatura", "compra" };
            if (historyTokens.Any(h => clean.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                res.Category = OcrContentCategory.TransactionalHistory;
                res.CategoryDisplayName = "Histórico / Descrição Transacional";
                return res;
            }

            // Fallback: se tiver letras e for razoavelmente legível
            if (clean.Any(char.IsLetter))
            {
                res.Category = OcrContentCategory.TransactionalHistory;
                res.CategoryDisplayName = "Histórico / Descrição";
                return res;
            }

            return res;
        }
    }

    public enum OcrContentCategory
    {
        None,                   // Sem texto legível (ícone, marcador, separador gráfico)
        InstitutionalLogo,      // Logotipo / Marca (ex: "Stone", "Itaú", "Orion Máquinas")
        ColumnHeaderOrLabel,    // Cabeçalho de coluna / Rótulo de tabela (ex: "Documento", "Instituição", "Conta", "DESCRIÇÃO", "TIPO", "SALDO")
        MonetaryValue,          // Valor monetário / Cifra / Taxa / Saldo (ex: "-R$ 0,11", "-R$ 4,47", "R$ 40,09")
        TransactionalHistory,   // Histórico / Descrição de transação (ex: "Saída Dinheiro Guardado", "Pix | Maquininha")
        CodeOrDateOrRef         // Data, código de barras, autenticação, CNPJ/CPF
    }

    public class OcrClassificationResult
    {
        public OcrContentCategory Category { get; set; } = OcrContentCategory.None;
        public string CategoryDisplayName { get; set; } = "Nenhum";
        public string RawText { get; set; } = string.Empty;
        public string CleanText { get; set; } = string.Empty;
    }
}
