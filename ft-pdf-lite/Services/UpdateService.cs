using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using MessageBox = System.Windows.MessageBox;
using Application = System.Windows.Application;

namespace FtPdfLite.Services
{
    public class UpdateCheckResult
    {
        public bool HasUpdate { get; set; }
        public string LatestVersion { get; set; } = "";
        public string CurrentVersion { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public string ReleaseNotes { get; set; } = "";
        public string ErrorMessage { get; set; } = "";
    }

    public static class UpdateService
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        static UpdateService()
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("FtPdfLiteUpdater", "2.0"));
            _httpClient.Timeout = TimeSpan.FromSeconds(15);
        }

        public static Version CurrentVersion =>
            Assembly.GetExecutingAssembly().GetName().Version ?? new Version(2, 0, 0, 0);

        public static string CurrentVersionString =>
            $"{CurrentVersion.Major}.{CurrentVersion.Minor}.{CurrentVersion.Build}";

        public static async Task<UpdateCheckResult> CheckForUpdatesAsync(bool isLite)
        {
            var result = new UpdateCheckResult
            {
                CurrentVersion = CurrentVersionString
            };

            try
            {
                var response = await _httpClient.GetStringAsync("https://api.github.com/repos/Fulviotanure/ft-pdf/releases");
                using var doc = JsonDocument.Parse(response);

                foreach (var release in doc.RootElement.EnumerateArray())
                {
                    string tagName = release.TryGetProperty("tag_name", out var tagProp) ? (tagProp.GetString() ?? "") : "";

                    bool matchesMode = isLite
                        ? (tagName.StartsWith("lite-v", StringComparison.OrdinalIgnoreCase) || tagName.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                        : (tagName.StartsWith("v", StringComparison.OrdinalIgnoreCase) && !tagName.StartsWith("lite-", StringComparison.OrdinalIgnoreCase));

                    if (!matchesMode) continue;

                    string versionStr = tagName.StartsWith("lite-v", StringComparison.OrdinalIgnoreCase)
                        ? tagName.Substring("lite-v".Length)
                        : tagName.TrimStart('v', 'V');

                    if (Version.TryParse(versionStr, out var releaseVer))
                    {
                        if (releaseVer > CurrentVersion)
                        {
                            result.HasUpdate = true;
                            result.LatestVersion = versionStr;

                            if (release.TryGetProperty("body", out var bodyProp))
                                result.ReleaseNotes = bodyProp.GetString() ?? "";

                            string targetExeName = isLite ? "FtPdfLite.exe" : "FtPdf.exe";
                            if (release.TryGetProperty("assets", out var assets))
                            {
                                foreach (var asset in assets.EnumerateArray())
                                {
                                    string name = asset.TryGetProperty("name", out var nProp) ? (nProp.GetString() ?? "") : "";
                                    if (name.Equals(targetExeName, StringComparison.OrdinalIgnoreCase))
                                    {
                                        result.DownloadUrl = asset.TryGetProperty("browser_download_url", out var urlProp) ? (urlProp.GetString() ?? "") : "";
                                        break;
                                    }
                                }
                            }
                            return result;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                Debug.WriteLine($"Erro ao checar atualizações: {ex.Message}");
            }

            return result;
        }

        public static async Task<bool> DownloadAndApplyUpdateAsync(string downloadUrl, string targetExeName)
        {
            try
            {
                string tempDir = Path.GetTempPath();
                string tempFile = Path.Combine(tempDir, $"{Path.GetFileNameWithoutExtension(targetExeName)}_vNew.exe");
                using (var downloadClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) })
                {
                    downloadClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("FtPdfLiteUpdater", "2.1"));
                    using var response = await downloadClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    using var contentStream = await response.Content.ReadAsStreamAsync();
                    using var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
                    await contentStream.CopyToAsync(fileStream);
                }

                string currentExePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";

                // Se estiver em modo de teste local via 'dotnet run', avisa amigavelmente
                if (string.IsNullOrEmpty(currentExePath) ||
                    currentExePath.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase) ||
                    currentExePath.Contains(@"\bin\Debug\") ||
                    currentExePath.Contains(@"\bin\Release\"))
                {
                    MessageBox.Show(
                        $"Nova versão baixada em:\n{tempFile}\n\n(Como você está executando em modo de teste local dos arquivos brutos, o código-fonte em desenvolvimento foi mantido intacto).",
                        "Download Concluído", MessageBoxButton.OK, MessageBoxImage.Information);
                    return true;
                }

                // Substituição segura do executável antigo pelo novo:
                // Usamos o PowerShell que suporta nativamente caminhos Unicode com acentos (ex: C:\Users\Luccas Octávio\Desktop)
                // e faz a troca em segundo plano sem abrir telas pretas de terminal nem sofrer com conflitos de codificação do cmd.exe.
                string escCurrent = currentExePath.Replace("'", "''");
                string escTemp = tempFile.Replace("'", "''");

                string psCommand = $"Start-Sleep -Seconds 1; " +
                    $"for ($i = 0; $i -lt 30; $i++) {{ " +
                    $"  try {{ Remove-Item -LiteralPath '{escCurrent}' -Force -ErrorAction Stop; break; }} catch {{ Start-Sleep -Milliseconds 500; }} " +
                    $"}} " +
                    $"Move-Item -LiteralPath '{escTemp}' -Destination '{escCurrent}' -Force; " +
                    $"Start-Process -FilePath '{escCurrent}'";

                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -Command \"{psCommand}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                }
                catch
                {
                    // Fallback caso powershell.exe esteja inacessível: gera script .bat com suporte a UTF-8 (chcp 65001)
                    string batPath = Path.Combine(tempDir, "ft_pdf_lite_replace_update.bat");
                    string batContent = $@"@echo off
chcp 65001 >nul
timeout /t 1 /nobreak >nul
:wait_loop
del /f /q ""{currentExePath}"" 2>nul
if exist ""{currentExePath}"" (
    timeout /t 1 /nobreak >nul
    goto wait_loop
)
move /y ""{tempFile}"" ""{currentExePath}""
start """" ""{currentExePath}""
del ""%~f0""
";
                    await File.WriteAllTextAsync(batPath, batContent, new System.Text.UTF8Encoding(false));

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = batPath,
                        CreateNoWindow = true,
                        UseShellExecute = true
                    });
                }

                Application.Current.Shutdown();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Falha ao atualizar executável:\n{ex.Message}", "Erro de Atualização", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private static int _isChecking = 0;
        private static bool _hasPromptedThisSession = false;

        public static async Task CheckForUpdatesAndPromptAsync(bool isLite, Window? owner, bool isStartup = false)
        {
            // Evita disparar verificações simultâneas
            if (Interlocked.CompareExchange(ref _isChecking, 1, 0) != 0)
                return;

            try
            {
                // Se já exibiu o pop-up de atualização nesta sessão do aplicativo e o usuário optou por não atualizar agora,
                // não incomoda novamente a cada novo arquivo aberto na mesma sessão.
                if (_hasPromptedThisSession)
                    return;

                // Breve pausa para dar tempo da interface renderizar a janela ou a primeira página do documento
                await Task.Delay(isStartup ? 800 : 500);

                var result = await CheckForUpdatesAsync(isLite);
                if (result.HasUpdate && !string.IsNullOrEmpty(result.DownloadUrl))
                {
                    _hasPromptedThisSession = true;

                    if (owner != null)
                    {
                        await owner.Dispatcher.InvokeAsync(async () =>
                        {
                            string appName = isLite ? "FT PDF Lite" : "FT PDF";
                            var resp = MessageBox.Show(owner,
                                $"🚀 Uma nova versão do {appName} (v{result.LatestVersion}) está disponível!\n\n" +
                                $"Versão atual: v{result.CurrentVersion}\n" +
                                $"Nova versão: v{result.LatestVersion}\n\n" +
                                "Deseja atualizar agora? O aplicativo fará o download e atualizará automaticamente.",
                                $"Atualização Disponível - {appName}",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Information);

                            if (resp == MessageBoxResult.Yes)
                            {
                                await DownloadAndApplyUpdateAsync(result.DownloadUrl, isLite ? "FtPdfLite.exe" : "FtPdf.exe");
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UpdateService] Erro na checagem automática: {ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref _isChecking, 0);
            }
        }

        public static Task AutoCheckOnStartupAsync(bool isLite, Window? owner) =>
            CheckForUpdatesAndPromptAsync(isLite, owner, isStartup: true);
    }
}

