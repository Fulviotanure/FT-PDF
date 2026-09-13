using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using FtPdf.Models;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace FtPdf.Services
{
    public class PdfEditingService
    {
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        /// <summary>
        /// Renders a PDF page to a high-resolution 300 DPI BitmapSource for crisp, print-quality display.
        /// </summary>
        public static System.Windows.Media.Imaging.BitmapSource RenderPageToBitmapSource(PdfiumViewer.PdfDocument doc, int pageIndex, int dpi = 300)
        {
            using var img = doc.Render(pageIndex, dpi, dpi, true);
            using var bmp = new System.Drawing.Bitmap(img);
            var hBitmap = bmp.GetHbitmap();
            try
            {
                var bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    System.Windows.Int32Rect.Empty,
                    System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                bitmapSource.Freeze();
                return bitmapSource;
            }
            finally
            {
                DeleteObject(hBitmap);
            }
        }

        /// <summary>
        /// Extracts word-level coordinates and text for interactive on-screen text selection.
        /// </summary>
        public List<PageTextData> ExtractTextLayers(string filePath, double displayWidth)
        {
            var result = new List<PageTextData>();
            if (!File.Exists(filePath)) return result;

            try
            {
                using var document = UglyToad.PdfPig.PdfDocument.Open(filePath);
                for (int i = 1; i <= document.NumberOfPages; i++)
                {
                    var page = document.GetPage(i);
                    double scale = displayWidth / page.Width;

                    var pageData = new PageTextData
                    {
                        PageNumber = i,
                        PageWidth = page.Width,
                        PageHeight = page.Height
                    };

                    foreach (var word in page.GetWords())
                    {
                        // Convert PDF bottom-left coordinates to top-left display coordinates
                        double wpfX = word.BoundingBox.Left * scale;
                        double wpfY = (page.Height - word.BoundingBox.Top) * scale;
                        double wpfW = Math.Max(2, word.BoundingBox.Width * scale);
                        double wpfH = Math.Max(4, word.BoundingBox.Height * scale);

                        pageData.Words.Add(new PageWordItem
                        {
                            Text = word.Text,
                            DisplayBounds = new Rect(wpfX, wpfY, wpfW, wpfH),
                            PdfBounds = new Rect(word.BoundingBox.Left, page.Height - word.BoundingBox.Top, word.BoundingBox.Width, word.BoundingBox.Height)
                        });
                    }

                    result.Add(pageData);
                }
            }
            catch
            {
                // Fallback gracefully if font encoding or stream error occurs
            }

            return result;
        }

        public void InsertText(string sourcePath, string outputPath, int pageNumber, string text, double x, double y, double fontSize, XColor color)
        {
            InsertFormattedTextBox(sourcePath, outputPath, pageNumber, text, x, y, 400, fontSize, color, false, false);
        }

        public void InsertFormattedTextBox(
            string sourcePath, 
            string outputPath, 
            int pageNumber, 
            string text, 
            double x, 
            double y, 
            double width, 
            double fontSize, 
            XColor color, 
            bool isBold, 
            bool isItalic)
        {
            using var document = PdfReader.Open(sourcePath, PdfDocumentOpenMode.Modify);
            if (pageNumber < 1 || pageNumber > document.PageCount)
                throw new ArgumentOutOfRangeException(nameof(pageNumber), "Número de página inválido.");

            var page = document.Pages[pageNumber - 1];
            using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);

            var style = XFontStyleEx.Regular;
            if (isBold && isItalic) style = XFontStyleEx.BoldItalic;
            else if (isBold) style = XFontStyleEx.Bold;
            else if (isItalic) style = XFontStyleEx.Italic;

            var font = new XFont("Arial", fontSize, style);
            var brush = new XSolidBrush(color);

            var rawLines = (text ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            double lineHeight = fontSize * 1.35;
            double currentY = y;
            double effectiveWidth = Math.Max(width, 100);

            foreach (var rawLine in rawLines)
            {
                if (string.IsNullOrEmpty(rawLine))
                {
                    currentY += lineHeight;
                    continue;
                }

                var words = rawLine.Split(' ');
                string currentLine = "";

                foreach (var word in words)
                {
                    string testLine = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
                    var size = gfx.MeasureString(testLine, font);

                    if (size.Width > effectiveWidth && !string.IsNullOrEmpty(currentLine))
                    {
                        gfx.DrawString(currentLine, font, brush, new XPoint(x, currentY), XStringFormats.TopLeft);
                        currentY += lineHeight;
                        currentLine = word;
                    }
                    else
                    {
                        currentLine = testLine;
                    }
                }

                if (!string.IsNullOrEmpty(currentLine))
                {
                    gfx.DrawString(currentLine, font, brush, new XPoint(x, currentY), XStringFormats.TopLeft);
                    currentY += lineHeight;
                }
            }

            document.Save(outputPath);
        }

        public void InsertSignature(string sourcePath, string outputPath, int pageNumber, byte[] imageBytes, double x, double y, double width, double height)
        {
            using var document = PdfReader.Open(sourcePath, PdfDocumentOpenMode.Modify);
            if (pageNumber < 1 || pageNumber > document.PageCount)
                throw new ArgumentOutOfRangeException(nameof(pageNumber), "Número de página inválido.");

            var page = document.Pages[pageNumber - 1];
            using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
            using var stream = new MemoryStream(imageBytes);
            using var image = XImage.FromStream(stream);

            gfx.DrawImage(image, x, y, width, height);
            document.Save(outputPath);
        }

        public void AddHighlight(string sourcePath, string outputPath, int pageNumber, double x, double y, double width, double height, XColor highlightColor)
        {
            AddHighlightRectangles(sourcePath, outputPath, pageNumber, new[] { new Rect(x, y, width, height) }, highlightColor);
        }

        public void AddHighlightRectangles(string sourcePath, string outputPath, int pageNumber, IEnumerable<Rect> rects, XColor highlightColor)
        {
            using var document = PdfReader.Open(sourcePath, PdfDocumentOpenMode.Modify);
            if (pageNumber < 1 || pageNumber > document.PageCount)
                throw new ArgumentOutOfRangeException(nameof(pageNumber), "Número de página inválido.");

            var page = document.Pages[pageNumber - 1];
            using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
            
            var colorWithAlpha = XColor.FromArgb(115, highlightColor.R, highlightColor.G, highlightColor.B);
            var brush = new XSolidBrush(colorWithAlpha);

            foreach (var r in rects)
            {
                gfx.DrawRectangle(brush, r.X, r.Y, r.Width, r.Height);
            }

            document.Save(outputPath);
        }

        public void MergePdfs(IEnumerable<string> filePaths, string outputPath)
        {
            using var outputDocument = new PdfDocument();

            foreach (var file in filePaths)
            {
                if (!File.Exists(file)) continue;

                using var inputDocument = PdfReader.Open(file, PdfDocumentOpenMode.Import);
                for (int i = 0; i < inputDocument.PageCount; i++)
                {
                    outputDocument.AddPage(inputDocument.Pages[i]);
                }
            }

            outputDocument.Save(outputPath);
        }

        public void ExtractPages(string sourcePath, string outputPath, IEnumerable<int> pageNumbers)
        {
            using var inputDocument = PdfReader.Open(sourcePath, PdfDocumentOpenMode.Import);
            using var outputDocument = new PdfDocument();

            var uniquePages = pageNumbers.Distinct().OrderBy(p => p);
            foreach (var pageNumber in uniquePages)
            {
                if (pageNumber >= 1 && pageNumber <= inputDocument.PageCount)
                {
                    outputDocument.AddPage(inputDocument.Pages[pageNumber - 1]);
                }
            }

            outputDocument.Save(outputPath);
        }

        public void RotatePages(string sourcePath, string outputPath, IEnumerable<int> pageNumbers, int angleDegrees)
        {
            using var document = PdfReader.Open(sourcePath, PdfDocumentOpenMode.Modify);
            var targetPages = new HashSet<int>(pageNumbers);

            for (int i = 0; i < document.PageCount; i++)
            {
                int pageNum = i + 1;
                if (targetPages.Contains(pageNum) || !targetPages.Any())
                {
                    var page = document.Pages[i];
                    page.Rotate = (page.Rotate + angleDegrees) % 360;
                }
            }

            document.Save(outputPath);
        }
    }
}
