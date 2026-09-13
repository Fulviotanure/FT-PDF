using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using MessageBox = System.Windows.MessageBox;

namespace FtPdf.Dialogs
{
    public partial class MergePdfDialog : Window
    {
        public ObservableCollection<string> FilesToMerge { get; } = new();
        public string OutputFilePath { get; private set; } = string.Empty;

        public MergePdfDialog(string? initialFile)
        {
            InitializeComponent();
            ListPdfs.ItemsSource = FilesToMerge;

            if (!string.IsNullOrEmpty(initialFile) && File.Exists(initialFile))
            {
                FilesToMerge.Add(initialFile);
            }
        }

        private void BtnAddFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Arquivos PDF (*.pdf)|*.pdf",
                Multiselect = true,
                Title = "Selecionar Arquivos PDF para Mesclar"
            };

            if (dialog.ShowDialog(this) == true)
            {
                foreach (var file in dialog.FileNames)
                {
                    if (!FilesToMerge.Contains(file))
                    {
                        FilesToMerge.Add(file);
                    }
                }
            }
        }

        private void BtnRemove_Click(object sender, RoutedEventArgs e)
        {
            if (ListPdfs.SelectedIndex >= 0)
            {
                FilesToMerge.RemoveAt(ListPdfs.SelectedIndex);
            }
        }

        private void BtnMoveUp_Click(object sender, RoutedEventArgs e)
        {
            int index = ListPdfs.SelectedIndex;
            if (index > 0)
            {
                var item = FilesToMerge[index];
                FilesToMerge.RemoveAt(index);
                FilesToMerge.Insert(index - 1, item);
                ListPdfs.SelectedIndex = index - 1;
            }
        }

        private void BtnMoveDown_Click(object sender, RoutedEventArgs e)
        {
            int index = ListPdfs.SelectedIndex;
            if (index >= 0 && index < FilesToMerge.Count - 1)
            {
                var item = FilesToMerge[index];
                FilesToMerge.RemoveAt(index);
                FilesToMerge.Insert(index + 1, item);
                ListPdfs.SelectedIndex = index + 1;
            }
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            if (FilesToMerge.Count < 2)
            {
                MessageBox.Show(this, "Adicione pelo menos 2 arquivos PDF para realizar a mesclagem.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "Arquivo PDF (*.pdf)|*.pdf",
                DefaultExt = "pdf",
                FileName = "Documento_Mesclado.pdf",
                Title = "Salvar Arquivo PDF Mesclado"
            };

            if (dialog.ShowDialog(this) == true)
            {
                OutputFilePath = dialog.FileName;
                DialogResult = true;
                Close();
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
