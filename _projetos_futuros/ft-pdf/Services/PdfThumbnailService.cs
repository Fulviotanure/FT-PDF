using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using PdfiumViewer;

namespace FtPdf.Services
{
    public static class PdfThumbnailService
    {
        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        public static BitmapSource? RenderPageThumbnail(PdfDocument doc, int pageIndex, int dpi = 96)
        {
            try
            {
                if (doc == null || pageIndex < 0 || pageIndex >= doc.PageCount) return null;

                using var img = doc.Render(pageIndex, dpi, dpi, true);
                using var bmp = new System.Drawing.Bitmap(img);
                var hBitmap = bmp.GetHbitmap();
                try
                {
                    var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap,
                        IntPtr.Zero,
                        System.Windows.Int32Rect.Empty,
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
    }
}
