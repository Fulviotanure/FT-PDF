using System;
using System.Windows;
using PdfSharp.Drawing;
using MessageBox = System.Windows.MessageBox;

namespace FtPdf.Dialogs
{
    public partial class InsertTextDialog : Window
    {
        public string TextToInsert { get; private set; } = string.Empty;
        public int PageNumber { get; private set; } = 1;
        public double FontSizeValue { get; private set; } = 14;
        public XColor TextColor { get; private set; } = XColors.Black;
        public double PosX { get; private set; } = 50;
        public double PosY { get; private set; } = 50;

        public InsertTextDialog(int maxPages, int currentPage)
        {
            InitializeComponent();
            TxtPageNumber.Text = Math.Clamp(currentPage, 1, Math.Max(1, maxPages)).ToString();
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtInputText.Text))
            {
                MessageBox.Show(this, "Por favor, digite o texto a ser inserido.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(TxtPageNumber.Text, out int page) || page < 1)
            {
                MessageBox.Show(this, "Número de página inválido.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TextToInsert = TxtInputText.Text;
            PageNumber = page;

            FontSizeValue = ComboFontSize.SelectedIndex switch
            {
                0 => 10,
                1 => 14,
                2 => 18,
                3 => 24,
                4 => 32,
                _ => 14
            };

            TextColor = ComboColor.SelectedIndex switch
            {
                0 => XColors.Black,
                1 => XColors.Navy,
                2 => XColors.DarkRed,
                3 => XColors.DarkGreen,
                _ => XColors.Black
            };

            // Calculate approximate coordinates on page (A4 ~ 595 x 842 pt)
            switch (ComboPosition.SelectedIndex)
            {
                case 0: // Header Top
                    PosX = 50; PosY = 40; break;
                case 1: // Footer Bottom
                    PosX = 50; PosY = 800; break;
                case 2: // Center
                    PosX = 150; PosY = 400; break;
                case 3: // Bottom Right
                    PosX = 380; PosY = 780; break;
                default:
                    PosX = 50; PosY = 50; break;
            }

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
