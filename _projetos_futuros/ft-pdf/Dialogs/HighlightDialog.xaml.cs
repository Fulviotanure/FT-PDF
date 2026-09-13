using System;
using System.Windows;
using PdfSharp.Drawing;
using MessageBox = System.Windows.MessageBox;

namespace FtPdf.Dialogs
{
    public partial class HighlightDialog : Window
    {
        public int PageNumber { get; private set; } = 1;
        public double PosX { get; private set; } = 50;
        public double PosY { get; private set; } = 40;
        public double RectWidth { get; private set; } = 500;
        public double RectHeight { get; private set; } = 25;
        public XColor HighlightColor { get; private set; } = XColors.Yellow;

        public HighlightDialog(int maxPages, int currentPage)
        {
            InitializeComponent();
            TxtPageNumber.Text = Math.Clamp(currentPage, 1, Math.Max(1, maxPages)).ToString();
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(TxtPageNumber.Text, out int page) || page < 1)
            {
                MessageBox.Show(this, "Número de página inválido.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            PageNumber = page;

            switch (ComboRegion.SelectedIndex)
            {
                case 0: // Header
                    PosX = 40; PosY = 40; RectWidth = 515; RectHeight = 35; break;
                case 1: // Central paragraph
                    PosX = 40; PosY = 300; RectWidth = 515; RectHeight = 50; break;
                case 2: // Footer
                    PosX = 40; PosY = 750; RectWidth = 515; RectHeight = 30; break;
            }

            HighlightColor = ComboColor.SelectedIndex switch
            {
                0 => XColor.FromArgb(255, 255, 0),    // Yellow
                1 => XColor.FromArgb(74, 222, 128),  // Green
                2 => XColor.FromArgb(244, 114, 182), // Pink
                3 => XColor.FromArgb(96, 165, 250),  // Blue
                _ => XColor.FromArgb(255, 255, 0)
            };

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
