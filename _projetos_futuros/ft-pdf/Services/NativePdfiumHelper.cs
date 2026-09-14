using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace FtPdf.Services
{
    public static class NativePdfiumHelper
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpLibFileName);

        private static bool _initialized = false;
        private static readonly object _lock = new();

        public static void Initialize()
        {
            if (_initialized) return;

            lock (_lock)
            {
                if (_initialized) return;

                try
                {
                    string extractedPath = EnsurePdfiumExtracted();
                    if (!string.IsNullOrEmpty(extractedPath) && File.Exists(extractedPath))
                    {
                        string dir = Path.GetDirectoryName(extractedPath)!;
                        SetDllDirectory(dir);

                        string currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                        if (!currentPath.Contains(dir, StringComparison.OrdinalIgnoreCase))
                        {
                            Environment.SetEnvironmentVariable("PATH", $"{dir};{currentPath}");
                        }

                        IntPtr handle = LoadLibrary(extractedPath);
                        if (handle == IntPtr.Zero)
                        {
                            int error = Marshal.GetLastWin32Error();
                            LogWarning($"LoadLibrary falhou para '{extractedPath}'. Código Win32: {error}. Possível falta do Visual C++ Redistributable.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogWarning($"Erro ao inicializar NativePdfiumHelper: {ex.Message}");
                }
                finally
                {
                    _initialized = true;
                }
            }
        }

        private static string EnsurePdfiumExtracted()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string localX64 = Path.Combine(baseDir, "x64", "pdfium.dll");
            if (File.Exists(localX64)) return localX64;

            string localRoot = Path.Combine(baseDir, "pdfium.dll");
            if (File.Exists(localRoot)) return localRoot;

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string targetDir = Path.Combine(localAppData, "FtPdf", "Native", "x64");
            string targetFile = Path.Combine(targetDir, "pdfium.dll");

            try
            {
                var asm = Assembly.GetExecutingAssembly();
                string? resourceName = null;
                foreach (var name in asm.GetManifestResourceNames())
                {
                    if (name.EndsWith("pdfium.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        resourceName = name;
                        break;
                    }
                }

                if (resourceName != null)
                {
                    using var stream = asm.GetManifestResourceStream(resourceName);
                    if (stream != null)
                    {
                        if (!File.Exists(targetFile) || new FileInfo(targetFile).Length != stream.Length)
                        {
                            Directory.CreateDirectory(targetDir);
                            string tempFile = targetFile + ".tmp";
                            using (var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                stream.CopyTo(fs);
                            }
                            if (File.Exists(targetFile))
                            {
                                try { File.Delete(targetFile); } catch { }
                            }
                            File.Move(tempFile, targetFile, true);
                        }

                        try
                        {
                            string appX64Dir = Path.Combine(baseDir, "x64");
                            Directory.CreateDirectory(appX64Dir);
                            string appX64File = Path.Combine(appX64Dir, "pdfium.dll");
                            if (!File.Exists(appX64File))
                            {
                                File.Copy(targetFile, appX64File, true);
                            }
                        }
                        catch { }

                        return targetFile;
                    }
                }
            }
            catch (Exception ex)
            {
                LogWarning($"Falha na extração de recurso pdfium.dll: {ex.Message}");
            }

            return targetFile;
        }

        private static void LogWarning(string message)
        {
            try
            {
                string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FtPdf");
                Directory.CreateDirectory(logDir);
                string logFile = Path.Combine(logDir, "pdfium_native.log");
                File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
            }
            catch { }
        }
    }
}
