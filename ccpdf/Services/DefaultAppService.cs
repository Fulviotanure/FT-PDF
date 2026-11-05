using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace CCPDF.Services
{
    public static class DefaultAppService
    {
        [DllImport("Shlwapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern uint AssocQueryString(uint flags, uint str, string pszAssoc, string? pszExtra, [Out] StringBuilder? pszOut, [In, Out] ref uint pcchOut);

        [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        private const uint SHCNE_ASSOCCHANGED = 0x08000000;
        private const uint SHCNF_IDLIST = 0x0000;

        public static bool IsDefaultPdfReader(bool isLite = true)
        {
            try
            {
                // Helper para verificar se a string identifica nosso aplicativo (CCPDF ou legado FT-PDF)
                static bool IsOurApp(string? val)
                {
                    if (string.IsNullOrWhiteSpace(val)) return false;
                    return val.Contains("CCPDF", StringComparison.OrdinalIgnoreCase) ||
                           val.Contains("FtPdf", StringComparison.OrdinalIgnoreCase);
                }

                // 1. Checa chave UserChoice do Explorer (Windows 10/11 per-user explicit choice)
                using (var userChoiceKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.pdf\UserChoice"))
                {
                    if (userChoiceKey != null)
                    {
                        string progId = userChoiceKey.GetValue("ProgId") as string ?? "";
                        if (IsOurApp(progId)) return true;
                    }
                }

                // 2. Checa via AssocQueryString - ASSOCSTR_PROGID (20)
                string assocProgId = QueryAssoc(20 /* ASSOCSTR_PROGID */, ".pdf");
                if (IsOurApp(assocProgId)) return true;

                // 3. Checa via AssocQueryString - ASSOCSTR_EXECUTABLE (2)
                string assocExe = QueryAssoc(2 /* ASSOCSTR_EXECUTABLE */, ".pdf");
                if (IsOurApp(assocExe)) return true;

                // 4. Checa via AssocQueryString - ASSOCSTR_COMMAND (1)
                string assocCmd = QueryAssoc(1 /* ASSOCSTR_COMMAND */, ".pdf");
                if (IsOurApp(assocCmd)) return true;

                // 5. Checa HKCU\Software\Classes\.pdf (associação direta do usuário)
                using (var hkcuPdfKey = Registry.CurrentUser.OpenSubKey(@"Software\Classes\.pdf"))
                {
                    if (hkcuPdfKey != null)
                    {
                        string defVal = hkcuPdfKey.GetValue("") as string ?? "";
                        if (IsOurApp(defVal)) return true;
                    }
                }

                // 6. Checa HKCR\.pdf (visão combinada do sistema / ClassesRoot)
                using (var hkcrPdfKey = Registry.ClassesRoot.OpenSubKey(@".pdf"))
                {
                    if (hkcrPdfKey != null)
                    {
                        string defVal = hkcrPdfKey.GetValue("") as string ?? "";
                        if (IsOurApp(defVal)) return true;
                    }
                }
            }
            catch {}

            return false;
        }

        private static string QueryAssoc(uint strType, string extension)
        {
            try
            {
                uint length = 0;
                AssocQueryString(0, strType, extension, null, null, ref length);
                if (length > 0)
                {
                    var sb = new StringBuilder((int)length);
                    uint res = AssocQueryString(0, strType, extension, null, sb, ref length);
                    if (res == 0 || sb.Length > 0)
                    {
                        return sb.ToString();
                    }
                }
            }
            catch { }
            return string.Empty;
        }

        public static bool IsDismissed(bool isLite = true)
        {
            try
            {
                string appFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    isLite ? "CCPDF" : "CCPDF");
                string dismissFile = Path.Combine(appFolder, "default_prompt_dismissed.flag");
                return File.Exists(dismissFile);
            }
            catch
            {
                return false;
            }
        }

        public static void DismissPrompt(bool isLite = true)
        {
            try
            {
                string appFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    isLite ? "CCPDF" : "CCPDF");
                Directory.CreateDirectory(appFolder);
                File.WriteAllText(Path.Combine(appFolder, "default_prompt_dismissed.flag"), DateTime.UtcNow.ToString("o"));
            }
            catch {}
        }

        public static void ResetDismissed(bool isLite = true)
        {
            try
            {
                string appFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    isLite ? "CCPDF" : "CCPDF");
                string dismissFile = Path.Combine(appFolder, "default_prompt_dismissed.flag");
                if (File.Exists(dismissFile))
                {
                    File.Delete(dismissFile);
                }
            }
            catch {}
        }

        public static void RegisterAndSetDefault(bool isLite = true)
        {
            try
            {
                DismissPrompt(isLite);

                string progId = isLite ? "CCPDF.Document" : "CCPDF.Document";
                string appName = isLite ? "CCPDF" : "CCPDF";
                string exeName = isLite ? "CCPDF.exe" : "CCPDF.exe";

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

                // Atualiza também chaves legadas para apontar para o executável atual
                try
                {
                    using var legacyProgKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\FtPdf.Document\shell\open\command");
                    legacyProgKey.SetValue("", $"\"{currentExePath}\" \"%1\"");
                }
                catch { }
                try
                {
                    using var legacyAppKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\Applications\FtPdfLite.exe\shell\open\command");
                    legacyAppKey.SetValue("", $"\"{currentExePath}\" \"%1\"");
                }
                catch { }

                // 4. Registra Windows Capabilities para tela de Aplicativos Padrão do Windows
                string regAppPath = isLite ? @"Software\CCPDF" : @"Software\CCPDF";
                using (var capKey = Registry.CurrentUser.CreateSubKey($@"{regAppPath}\Capabilities"))
                {
                    capKey.SetValue("ApplicationName", appName);
                    capKey.SetValue("ApplicationDescription", $"{appName} - Visualizador e Editor de Documentos PDF");
                    using var faKey = capKey.CreateSubKey("FileAssociations");
                    faKey.SetValue(".pdf", progId);
                }
                using (var regAppsKey = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications"))
                {
                    regAppsKey.SetValue(isLite ? "CCPDF" : "CCPDF", $@"{regAppPath}\Capabilities");
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
