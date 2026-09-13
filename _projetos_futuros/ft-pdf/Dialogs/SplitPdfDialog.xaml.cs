using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using MessageBox = System.Windows.MessageBox;

namespace FtPdf.Dialogs
{
    public partial class SplitPdfDialog : Window
    {
        public List<int> SelectedPages { get; } = new();
        public string OutputFilePath { get; private set; } = string.Empty;
        private readonly int _totalPages;

        public SplitPdfDialog(int totalPages, int currentPage)
        {
            InitializeComponent();
            _totalPages = totalPages;
            TxtDocumentInfo.Text = $"Total de Páginas do Documento: {totalPages}";
            TxtPageRange.Text = totalPages > 1 ? $"1-{Math.Min(totalPages, 3)}" : "1";
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            var parsedPages = ParsePageRange(TxtPageRange.Text, _totalPages);
            if (parsedPages.Count == 0)
            {
                MessageBox.Show(this, "Nenhuma página válida informada no intervalo.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "Arquivo PDF (*.pdf)|*.pdf",
                DefaultExt = "pdf",
                FileName = "Paginas_Extraidas.pdf",
                Title = "Salvar Páginas Extraídas"
            };

            if (dialog.ShowDialog(this) == true)
            {
                SelectedPages.Clear();
                SelectedPages.AddRange(parsedPages);
                OutputFilePath = dialog.FileName;
                DialogResult = true;
                Close();
            }
        }

        private static List<int> ParsePageRange(string input, int maxPage)
        {
            var result = new HashSet<int>();
            var parts = input.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (trimmed.Contains('-'))
                {
                    var rangeParts = trimmed.Split('-');
                    if (rangeParts.Length == 2 &&
                        int.TryParse(rangeParts[0].Trim(), out int start) &&
                        int.TryParse(rangeParts[1].Trim(), out int end))
                    {
                        for (int i = Math.Min(start, end); i <= Math.Max(start, end); i++)
                        {
                            if (i >= 1 && i <= maxPage) result.Add(i);
                        }
                    }
                }
                else if (int.TryParse(trimmed, out int singlePage))
                {
                    if (singlePage >= 1 && singlePage <= maxPage) result.Add(singlePage);
                }
            }

            return result.OrderBy(p => p).ToList();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
