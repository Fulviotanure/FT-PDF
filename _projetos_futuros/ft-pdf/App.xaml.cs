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
