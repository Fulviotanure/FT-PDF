using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using MessageBox = System.Windows.MessageBox;

namespace FtPdf.Dialogs
{
    public partial class SignatureDialog : Window
    {
        public byte[]? SignatureImageBytes { get; private set; }
        public int PageNumber { get; private set; } = 1;
        public double PosX { get; private set; } = 380;
        public double PosY { get; private set; } = 720;
        public double SigWidth { get; private set; } = 150;
        public double SigHeight { get; private set; } = 60;

        private byte[]? _uploadedImageBytes;

        public SignatureDialog(int maxPages, int currentPage)
        {
            InitializeComponent();
            TxtPageNumber.Text = Math.Clamp(currentPage, 1, Math.Max(1, maxPages)).ToString();
        }

        private void BtnClearCanvas_Click(object sender, RoutedEventArgs e)
        {
            CanvasSignature.Strokes.Clear();
        }

        private void BtnUploadImage_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Imagens (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|Todos os Arquivos (*.*)|*.*",
                Title = "Selecionar Imagem de Assinatura"
            };

            if (dialog.ShowDialog(this) == true)
            {
                _uploadedImageBytes = File.ReadAllBytes(dialog.FileName);
                TxtSelectedImage.Text = $"Arquivo: {Path.GetFileName(dialog.FileName)}";
            }
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(TxtPageNumber.Text, out int page) || page < 1)
            {
                MessageBox.Show(this, "Número de página inválido.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            PageNumber = page;

            // Check if user uploaded image or drew on canvas
            if (_uploadedImageBytes != null && _uploadedImageBytes.Length > 0)
            {
                SignatureImageBytes = _uploadedImageBytes;
            }
            else if (CanvasSignature.Strokes.Count > 0)
            {
                SignatureImageBytes = RenderInkCanvasToPng(CanvasSignature);
            }
            else
            {
                MessageBox.Show(this, "Por favor, desenhe uma assinatura ou selecione uma imagem.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Position calculations (A4: ~ 595 x 842 pt)
            switch (ComboPosition.SelectedIndex)
            {
                case 0: // Bottom Right
                    PosX = 380; PosY = 720; break;
                case 1: // Bottom Left
                    PosX = 60; PosY = 720; break;
                case 2: // Center
                    PosX = 220; PosY = 400; break;
                case 3: // Footer Center
                    PosX = 220; PosY = 740; break;
                default:
                    PosX = 380; PosY = 720; break;
            }

            DialogResult = true;
            Close();
        }

        private static byte[] RenderInkCanvasToPng(System.Windows.Controls.InkCanvas canvas)
        {
            int width = (int)Math.Max(100, canvas.ActualWidth);
            int height = (int)Math.Max(50, canvas.ActualHeight);

            var rtb = new RenderTargetBitmap(width, height, 96d, 96d, PixelFormats.Pbgra32);
            rtb.Render(canvas);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));

            using var ms = new MemoryStream();
            encoder.Save(ms);
            return ms.ToArray();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
