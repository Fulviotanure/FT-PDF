using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media.Imaging;
using FtPdf.Services;
using PdfiumViewer;

namespace FtPdf.Models
{
    public class PdfDocumentTab : IDisposable
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FilePath { get; set; } = string.Empty;
        public string FileName => Path.GetFileName(FilePath);
        public int TotalPages { get; set; } = 1;
        public ExtractionResult? Extraction { get; set; }
        public Dictionary<int, BitmapSource> Thumbnails { get; } = new();

        // Viewer state per tab — preserved when switching tabs
        public double ZoomLevel { get; set; } = 1.0;
        public double ScrollOffset { get; set; } = 0.0;
        public Dictionary<int, double> PageOffsets { get; } = new(); // vertical offset per page in the viewer

        // Native Pdfium document — kept open while tab is active for fast re-rendering
        public PdfDocument? PdfDoc { get; set; }

        public void Dispose()
        {
            Thumbnails.Clear();
            PageOffsets.Clear();
            try { PdfDoc?.Dispose(); } catch { }
            PdfDoc = null;
        }
    }
}
