using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using PdfiumViewer;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Key = System.Windows.Input.Key;
using Brush = System.Windows.Media.Brush;
using BrushConverter = System.Windows.Media.BrushConverter;
using Geometry = System.Windows.Media.Geometry;

namespace FtPdfLite
{
    public partial class PasswordPromptDialog : Window
    {
        private readonly string _filePath;
        private bool _isPasswordShown = false;

        public string? EnteredPassword { get; private set; }
        public PdfDocument? LoadedDocument { get; private set; }

        public PasswordPromptDialog(string filePath)
        {
            InitializeComponent();
            _filePath = filePath;
            TxtFileName.Text = Path.GetFileName(filePath);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            TxtPassword.Focus();
        }

        private void BtnToggleEye_Click(object sender, RoutedEventArgs e)
        {
            _isPasswordShown = !_isPasswordShown;
            if (_isPasswordShown)
            {
                TxtPasswordVisible.Text = TxtPassword.Password;
                TxtPassword.Visibility = Visibility.Collapsed;
                TxtPasswordVisible.Visibility = Visibility.Visible;
                TxtPasswordVisible.Focus();
                TxtPasswordVisible.CaretIndex = TxtPasswordVisible.Text.Length;
                IconEye.Data = Geometry.Parse("M11.83 9L15 12.16C15 12.11 15 12.05 15 12C15 10.34 13.66 9 12 9C11.95 9 11.89 9 11.83 9ZM7.53 9.8L9.08 11.35C9.03 11.56 9 11.77 9 12C9 13.66 10.34 15 12 15C12.22 15 12.44 14.97 12.65 14.92L14.2 16.47C13.53 16.8 12.79 17 12 17C9.24 17 7 14.76 7 12C7 11.21 7.2 10.47 7.53 9.8ZM2 4.27L4.28 6.55L4.73 7.01C3.08 8.3 1.78 10 1 12C2.73 16.39 7 19.5 12 19.5C13.55 19.5 15.03 19.2 16.38 18.66L16.8 19.08L19.73 22L21 20.73L3.27 3L2 4.27ZM12 7C12.05 7 12.11 7 12.17 7.01L10.02 4.86C10.66 4.63 11.32 4.5 12 4.5C17 4.5 21.27 7.61 23 12C22.18 14.08 20.79 15.84 19 17.09L17.56 15.65C18.89 14.76 19.95 13.5 20.57 12C19.05 8.32 15.77 6.5 12 6.5V7Z");
                IconEye.Fill = (Brush)new BrushConverter().ConvertFromString("#38BDF8")!;
            }
            else
            {
                TxtPassword.Password = TxtPasswordVisible.Text;
                TxtPasswordVisible.Visibility = Visibility.Collapsed;
                TxtPassword.Visibility = Visibility.Visible;
                TxtPassword.Focus();
                IconEye.Data = Geometry.Parse("M12 4.5C7 4.5 2.73 7.61 1 12C2.73 16.39 7 19.5 12 19.5C17 19.5 21.27 16.39 23 12C21.27 7.61 17 4.5 12 4.5ZM12 17C9.24 17 7 14.76 7 12C7 9.24 9.24 7 12 7C14.76 7 17 9.24 17 12C17 14.76 14.76 17 12 17ZM12 9C10.34 9 9 10.34 9 12C9 13.66 10.34 15 12 15C13.66 15 15 13.66 15 12C15 10.34 13.66 9 12 9Z");
                IconEye.Fill = (Brush)new BrushConverter().ConvertFromString("#94A3B8")!;
            }
        }

        private void Input_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                BtnUnlock_Click(sender, e);
            }
        }

        private void BtnUnlock_Click(object sender, RoutedEventArgs e)
        {
            string password = _isPasswordShown ? TxtPasswordVisible.Text : TxtPassword.Password;

            if (string.IsNullOrEmpty(password))
            {
                ShowError("Por favor, digite a senha do documento.");
                return;
            }

            try
            {
                var doc = PdfDocument.Load(_filePath, password);
                if (doc != null)
                {
                    LoadedDocument = doc;
                    EnteredPassword = password;
                    DialogResult = true;
                    Close();
                }
                else
                {
                    ShowError("Não foi possível carregar o documento com a senha fornecida.");
                }
            }
            catch (PdfException ex) when (ex.Error == PdfError.PasswordProtected || ex.Message.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                ShowError("Senha incorreta. Verifique e tente novamente.");
            }
            catch (Exception ex)
            {
                ShowError($"Erro ao abrir o arquivo: {ex.Message}");
            }
        }

        private void ShowError(string message)
        {
            TxtErrorMessage.Text = message;
            PanelError.Visibility = Visibility.Visible;
            if (_isPasswordShown)
            {
                TxtPasswordVisible.SelectAll();
                TxtPasswordVisible.Focus();
            }
            else
            {
                TxtPassword.SelectAll();
                TxtPassword.Focus();
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
