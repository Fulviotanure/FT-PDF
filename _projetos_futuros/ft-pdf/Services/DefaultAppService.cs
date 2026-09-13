using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace FtPdf.Services
{
    public static class DefaultAppService
    {
        [DllImport("Shlwapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern uint AssocQueryString(uint flags, uint str, string pszAssoc, string? pszExtra, [Out] StringBuilder? pszOut, [In, Out] ref uint pcchOut);

        [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        private const uint SHCNE_ASSOCCHANGED = 0x08000000;
        private const uint SHCNF_IDLIST = 0x0000;

        public static bool IsDefaultPdfReader(bool isLite = false)
        {
            try
            {
                string targetProgId = isLite ? "FtPdfLite.Document" : "FtPdf.Document";
                string targetExeName = isLite ? "FtPdfLite.exe" : "FtPdf.exe";

                // 1. Checa chave UserChoice do Explorer
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.pdf\UserChoice");
                if (key != null)
                {
                    string progId = key.GetValue("ProgId") as string ?? "";
                    if (progId.Equals(targetProgId, StringComparison.OrdinalIgnoreCase) ||
                        progId.Contains(isLite ? "FtPdfLite" : "FtPdf", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                // 2. Checa via AssocQueryString
                uint length = 0;
                AssocQueryString(0, 2 /* ASSOCSTR_EXECUTABLE */, ".pdf", null, null, ref length);
                if (length > 0)
                {
                    var sb = new StringBuilder((int)length);
                    AssocQueryString(0, 2, ".pdf", null, sb, ref length);
                    string currentExe = sb.ToString();
                    if (!string.IsNullOrEmpty(currentExe) && currentExe.Contains(targetExeName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch {}

            return false;
        }

        public static bool IsDismissed(bool isLite = false)
        {
            try
            {
                string appFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    isLite ? "FtPdfLite" : "FtPdf");
                string dismissFile = Path.Combine(appFolder, "default_prompt_dismissed.flag");
                return File.Exists(dismissFile);
            }
            catch
            {
                return false;
            }
        }

        public static void DismissPrompt(bool isLite = false)
        {
            try
            {
                string appFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    isLite ? "FtPdfLite" : "FtPdf");
                Directory.CreateDirectory(appFolder);
                File.WriteAllText(Path.Combine(appFolder, "default_prompt_dismissed.flag"), DateTime.UtcNow.ToString("o"));
            }
            catch {}
        }

        public static void ResetDismissed(bool isLite = false)
        {
            try
            {
                string appFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    isLite ? "FtPdfLite" : "FtPdf");
                string dismissFile = Path.Combine(appFolder, "default_prompt_dismissed.flag");
                if (File.Exists(dismissFile))
                {
                    File.Delete(dismissFile);
                }
            }
            catch {}
        }

        public static void RegisterAndSetDefault(bool isLite = false)
        {
            try
            {
                string progId = isLite ? "FtPdfLite.Document" : "FtPdf.Document";
                string appName = isLite ? "FT PDF Lite" : "FT PDF";
                string exeName = isLite ? "FtPdfLite.exe" : "FtPdf.exe";

                string currentExePath = Environment.ProcessPath ?? "";
                if (string.IsNullOrEmpty(currentExePath) || currentExePath.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase))
                {
                    string localTarget = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, exeName);
                    if (File.Exists(localTarget))
                    {
                        currentExePath = localTarget;
                    }
                    else
                    {
                        currentExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, exeName);
                    }
                }

                // 1. Registra ProgID em HKCU\Software\Classes
                using (var progKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{progId}"))
                {
                    progKey.SetValue("", $"{appName} Document");
                    using var iconKey = progKey.CreateSubKey("DefaultIcon");
                    iconKey.SetValue("", $"\"{currentExePath}\",0");

                    using var cmdKey = progKey.CreateSubKey(@"shell\open\command");
                    cmdKey.SetValue("", $"\"{currentExePath}\" \"%1\"");
                }

                // 2. Registra associação na extensão .pdf
                using (var pdfKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.pdf"))
                {
                    pdfKey.SetValue("", progId);
                    using var openWithKey = pdfKey.CreateSubKey("OpenWithProgids");
                    openWithKey.SetValue(progId, string.Empty);
                }

                // 3. Registra em Applications
                using (var appKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\Applications\{exeName}"))
                {
                    using var typesKey = appKey.CreateSubKey("SupportedTypes");
                    typesKey.SetValue(".pdf", string.Empty);

                    using var cmdKey = appKey.CreateSubKey(@"shell\open\command");
                    cmdKey.SetValue("", $"\"{currentExePath}\" \"%1\"");
                }

                // 4. Registra Windows Capabilities para tela de Aplicativos Padrão do Windows
                string regAppPath = isLite ? @"Software\FtPdfLite" : @"Software\FtPdf";
                using (var capKey = Registry.CurrentUser.CreateSubKey($@"{regAppPath}\Capabilities"))
                {
                    capKey.SetValue("ApplicationName", appName);
                    capKey.SetValue("ApplicationDescription", $"{appName} - Visualizador e Editor de Documentos PDF");
                    using var faKey = capKey.CreateSubKey("FileAssociations");
                    faKey.SetValue(".pdf", progId);
                }
                using (var regAppsKey = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications"))
                {
                    regAppsKey.SetValue(isLite ? "FtPdfLite" : "FtPdf", $@"{regAppPath}\Capabilities");
                }

                // 5. Notifica o Windows sobre a mudança nas associações
                SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);

                // 6. Abre a tela de aplicativos padrão do Windows
                Process.Start(new ProcessStartInfo
                {
                    FileName = "ms-settings:defaultapps",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Erro ao registrar programa padrão: {ex.Message}");
            }
        }
    }
}
