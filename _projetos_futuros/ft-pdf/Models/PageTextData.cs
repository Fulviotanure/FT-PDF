using System.Collections.Generic;
using System.Windows;

namespace FtPdf.Models
{
    public class PageWordItem
    {
        public string Text { get; set; } = string.Empty;
        public Rect DisplayBounds { get; set; }
        public Rect PdfBounds { get; set; }
        public bool IsSelected { get; set; }
    }

    public class PageTextData
    {
        public int PageNumber { get; set; }
        public double PageWidth { get; set; }
        public double PageHeight { get; set; }
        public List<PageWordItem> Words { get; set; } = new();
    }
}
