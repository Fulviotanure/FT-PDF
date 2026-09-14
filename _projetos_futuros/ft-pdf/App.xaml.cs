using System;
using System.IO;
using System.Threading;
using System.Windows;
using Application = System.Windows.Application;
using FtPdf.Services;

namespace FtPdf
{
    public partial class App : Application
    {
        private const string MutexName = "FtPdf_SingleInstance_Mutex_v2";
        private const string PipeName = "FtPdf_SingleInstance_Pipe_v2";
        private Mutex? _mutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            SetupExceptionHandling();
            NativePdfiumHelper.Initialize();

            base.OnStartup(e);

            bool isNewInstance;
            try
            {
                _mutex = new Mutex(true, MutexName, out isNewInstance);
                if (!isNewInstance)
                {
                    try
                    {
                        if (_mutex.WaitOne(0, false))
                        {
                            isNewInstance = true;
                        }
                    }
                    catch (AbandonedMutexException)
                    {
                        isNewInstance = true;
                    }
                }
            }
            catch
            {
                isNewInstance = true;
            }

            if (!isNewInstance)
            {
                // Secondary instance: allow existing window to take focus
                SingleInstanceService.AllowSetForegroundWindow(SingleInstanceService.ASFW_ANY);

                // Pass command-line arguments to existing instance
                if (SingleInstanceService.TrySendArgs(PipeName, e.Args))
                {
                    Shutdown(0);
                    return;
                }
            }

            // Primary instance: create and show MainWindow
            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();

            // Start listening for secondary instances
            SingleInstanceService.StartServer(PipeName, files =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (MainWindow is MainWindow win)
                    {
                        SingleInstanceService.BringToForeground(win);
                        foreach (var file in files)
                        {
                            string path = file.Trim('"', ' ');
                            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                            {
                                win.OpenTab(path);
                            }
                        }
                    }
                });
            });
        }

        private void SetupExceptionHandling()
        {
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                LogCrash(args.ExceptionObject as Exception, "AppDomain.UnhandledException");
            };

            DispatcherUnhandledException += (s, args) =>
            {
                LogCrash(args.Exception, "DispatcherUnhandledException");
                args.Handled = true;

                string msg = args.Exception.Message;
                if (args.Exception is DllNotFoundException || msg.Contains("pdfium", StringComparison.OrdinalIgnoreCase))
                {
                    System.Windows.MessageBox.Show(
                        "O FT PDF requer a biblioteca de tempo de execução do Microsoft Visual C++ 2015-2022 (x64) para renderizar PDFs com alta performance.\n\n" +
                        "Caso este computador seja novo ou recém-formatado, baixe e instale gratuitamente o 'Visual C++ Redistributable x64' no site oficial da Microsoft.\n\n" +
                        $"Detalhes técnicos: {msg}",
                        "Componente do Sistema Necessário - FT PDF",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                else
                {
                    System.Windows.MessageBox.Show(
                        $"Ocorreu um erro no FT PDF:\n{msg}\n\nO aplicativo continuará em execução.",
                        "Aviso FT PDF",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            };

            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, args) =>
            {
                LogCrash(args.Exception, "TaskScheduler.UnobservedTaskException");
                args.SetObserved();
            };
        }

        private static void LogCrash(Exception? ex, string source)
        {
            if (ex == null) return;
            try
            {
                string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FtPdf");
                Directory.CreateDirectory(logDir);
                string logFile = Path.Combine(logDir, "crash.log");
                string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}]\n{ex}\n{new string('-', 50)}\n";
                File.AppendAllText(logFile, entry);
            }
            catch { }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            SingleInstanceService.StopServer();
            try
            {
                _mutex?.ReleaseMutex();
                _mutex?.Dispose();
            }
            catch { }
            base.OnExit(e);
        }
    }
}
