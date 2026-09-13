using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using MessageBox = System.Windows.MessageBox;
using FtPdf.Services;

namespace FtPdf
{
    public partial class SettingsWindow : Window
    {
        private UpdateCheckResult? _cachedResult;
        private bool _isDownloading = false;

        public SettingsWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            TxtCurrentVersion.Text = $"v{UpdateService.CurrentVersionString}";
            TxtBadgeVersion.Text = $"Release {UpdateService.CurrentVersionString}";

            CheckDefaultAppStatus();
            await CheckUpdatesAsync();
        }

        private void CheckDefaultAppStatus()
        {
            bool isDefault = DefaultAppService.IsDefaultPdfReader(isLite: false);

            if (isDefault)
            {
                BdDefaultStatus.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E3A2F"));
                BdDefaultStatus.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                TxtDefaultStatusBadge.Text = "Padrão Ativo";
                TxtDefaultStatusBadge.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#34D399"));
                IconDefaultStatus.Data = Geometry.Parse("M9 16.17L4.83 12L3.41 13.41L9 19L21 7L19.59 5.59L9 16.17Z");
                IconDefaultStatus.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#34D399"));
                TxtDefaultStatusDescription.Text = "O FT PDF já é o seu aplicativo padrão para leitura de documentos PDF no Windows.";
                TxtBtnSetDefaultApp.Text = "Reconfigurar no Windows";
            }
            else
            {
                BdDefaultStatus.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3F2E18"));
                BdDefaultStatus.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
                TxtDefaultStatusBadge.Text = "Não é Padrão";
                TxtDefaultStatusBadge.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FBBF24"));
                IconDefaultStatus.Data = Geometry.Parse("M1 21H23L12 2L1 21ZM13 18H11V16H13V18ZM13 14H11V10H13V14Z");
                IconDefaultStatus.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FBBF24"));
                TxtDefaultStatusDescription.Text = "Outro aplicativo está configurado para abrir PDFs. Clique no botão abaixo para definir o FT PDF como leitor padrão.";
                TxtBtnSetDefaultApp.Text = "Definir como Leitor Padrão";
            }
        }

        private void BtnSetDefaultApp_Click(object sender, RoutedEventArgs e)
        {
            DefaultAppService.RegisterAndSetDefault(isLite: false);
            CheckDefaultAppStatus();
        }

        private async Task CheckUpdatesAsync()
        {
            TxtStatus.Text = "Verificando atualizações...";
            TxtLatestVersion.Text = "Verificando...";
            PbStatus.Visibility = Visibility.Visible;
            BtnAction.IsEnabled = false;

            var result = await UpdateService.CheckForUpdatesAsync(isLite: false);
            _cachedResult = result;

            PbStatus.Visibility = Visibility.Collapsed;
            BtnAction.IsEnabled = true;

            if (result.HasUpdate)
            {
                TxtLatestVersion.Text = $"v{result.LatestVersion}";
                TxtStatus.Text = $"Nova versão v{result.LatestVersion} disponível para instalação!";
                TxtBtnAction.Text = "Atualizar Agora";
                IconBtnAction.Data = Geometry.Parse("M19.35 10.04C18.67 6.59 15.64 4 12 4C9.11 4 6.6 5.64 5.35 8.04C2.34 8.36 0 10.91 0 14C0 17.31 2.69 20 6 20H19C21.76 20 24 17.76 24 15C24 12.36 21.95 10.22 19.35 10.04ZM13 13V17H11V13H8L12 9L16 13H13Z");
                BtnAction.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
            }
            else if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                TxtLatestVersion.Text = "Indisponível";
                TxtStatus.Text = "Não foi possível conectar ao canal de atualizações.";
                TxtBtnAction.Text = "Tentar Novamente";
                IconBtnAction.Data = Geometry.Parse("M17.65 6.35C16.2 4.9 14.21 4 12 4C7.58 4 4.01 7.58 4.01 12C4.01 16.42 7.58 20 12 20C15.73 20 18.84 17.45 19.73 14H17.65C16.83 16.33 14.61 18 12 18C8.69 18 6 15.31 6 12C6 8.69 8.69 6 12 6C13.66 6 15.14 6.69 16.22 7.78L13 11H20V4L17.65 6.35Z");
                BtnAction.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155"));
            }
            else
            {
                TxtLatestVersion.Text = $"v{UpdateService.CurrentVersionString}";
                TxtStatus.Text = "Você já está utilizando a versão mais recente.";
                TxtBtnAction.Text = "Verificar Novamente";
                IconBtnAction.Data = Geometry.Parse("M17.65 6.35C16.2 4.9 14.21 4 12 4C7.58 4 4.01 7.58 4.01 12C4.01 16.42 7.58 20 12 20C15.73 20 18.84 17.45 19.73 14H17.65C16.83 16.33 14.61 18 12 18C8.69 18 6 15.31 6 12C6 8.69 8.69 6 12 6C13.66 6 15.14 6.69 16.22 7.78L13 11H20V4L17.65 6.35Z");
                BtnAction.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155"));
            }
        }

        private async void BtnAction_Click(object sender, RoutedEventArgs e)
        {
            if (_isDownloading) return;

            if (_cachedResult != null && _cachedResult.HasUpdate && !string.IsNullOrEmpty(_cachedResult.DownloadUrl))
            {
                _isDownloading = true;
                BtnAction.IsEnabled = false;
                PbStatus.Visibility = Visibility.Visible;
                TxtStatus.Text = "Baixando nova versão e substituindo executável antigo...";

                await UpdateService.DownloadAndApplyUpdateAsync(_cachedResult.DownloadUrl, "FtPdf.exe");

                _isDownloading = false;
                BtnAction.IsEnabled = true;
                PbStatus.Visibility = Visibility.Collapsed;
            }
            else
            {
                await CheckUpdatesAsync();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
