using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PdfiumViewer;

namespace FtPdf.Services
{
    /// <summary>
    /// Renders PDF pages as WPF BitmapSources using the native Pdfium engine.
    /// Thread-safe: rendering happens on background threads, results dispatched to UI.
    /// </summary>
    public static class PdfViewerService
    {
        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        /// <summary>
        /// Renders a single PDF page at the given DPI and zoom level.
        /// Returns a frozen BitmapSource ready for WPF Image binding, or null on failure.
        /// </summary>
        public static BitmapSource? RenderPage(PdfDocument doc, int pageIndex, double dpi = 120.0, double zoom = 1.0)
        {
            try
            {
                if (doc == null || pageIndex < 0 || pageIndex >= doc.PageCount)
                    return null;

                int effectiveDpi = (int)Math.Round(dpi * zoom);
                effectiveDpi = Math.Clamp(effectiveDpi, 48, 600);

                using var image = doc.Render(pageIndex, effectiveDpi, effectiveDpi, PdfRenderFlags.CorrectFromDpi);
                using var bmp = new System.Drawing.Bitmap(image);

                var hBitmap = bmp.GetHbitmap();
                try
                {
                    var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap,
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    source.Freeze();
                    return source;
                }
                finally
                {
                    DeleteObject(hBitmap);
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Opens a PdfDocument from a file path. Caller is responsible for Dispose().
        /// </summary>
        public static PdfDocument? OpenDocument(string filePath)
        {
            try
            {
                return PdfDocument.Load(filePath);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the rendered pixel size of a page at the given DPI and zoom.
        /// </summary>
        public static System.Windows.Size GetPageSize(PdfDocument doc, int pageIndex, double dpi = 120.0, double zoom = 1.0)
        {
            try
            {
                var size = doc.PageSizes[pageIndex];
                double scale = (dpi * zoom) / 72.0; // PDF points are 72 dpi
                return new System.Windows.Size(size.Width * scale, size.Height * scale);
            }
            catch
            {
                return new System.Windows.Size(794, 1123); // A4 fallback
            }
        }
    }
}