using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace FtPdf.Services
{
    public static class SingleInstanceService
    {
        private static CancellationTokenSource? _cts;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AllowSetForegroundWindow(int dwProcessId);

        public const int ASFW_ANY = -1;
        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;

        public static bool TrySendArgs(string pipeName, string[]? args, int timeoutMs = 3000)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                try
                {
                    using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out);
                    int remaining = Math.Max(100, timeoutMs - (int)sw.ElapsedMilliseconds);
                    client.Connect(remaining);

                    using var writer = new StreamWriter(client, Encoding.UTF8);
                    string payload = string.Join("\n", args ?? Array.Empty<string>());
                    writer.Write(payload);
                    writer.Flush();
                    return true;
                }
                catch (TimeoutException)
                {
                    Thread.Sleep(100);
                }
                catch (IOException)
                {
                    Thread.Sleep(100);
                }
                catch
                {
                    Thread.Sleep(100);
                }
            }
            return false;
        }

        public static void StartServer(string pipeName, Action<string[]> onFilesReceived)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    NamedPipeServerStream? server = null;
                    try
                    {
                        server = new NamedPipeServerStream(
                            pipeName,
                            PipeDirection.In,
                            NamedPipeServerStream.MaxAllowedServerInstances,
                            PipeTransmissionMode.Byte,
                            PipeOptions.Asynchronous);

                        await server.WaitForConnectionAsync(token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        server?.Dispose();
                        break;
                    }
                    catch
                    {
                        server?.Dispose();
                        try
                        {
                            await Task.Delay(100, token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        continue;
                    }

                    var currentServer = server;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using (currentServer)
                            using (var reader = new StreamReader(currentServer, Encoding.UTF8))
                            {
                                var payload = await reader.ReadToEndAsync().ConfigureAwait(false);
                                var files = payload.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                                onFilesReceived(files);
                            }
                        }
                        catch { }
                    });
                }
            }, token);
        }

        public static void StopServer()
        {
            try
            {
                _cts?.Cancel();
                _cts?.Dispose();
                _cts = null;
            }
            catch { }
        }

        public static void BringToForeground(Window window)
        {
            try
            {
                if (window.WindowState == WindowState.Minimized)
                {
                    window.WindowState = WindowState.Normal;
                }

                var hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    ShowWindow(hwnd, SW_SHOW);
                    SetForegroundWindow(hwnd);
                }

                window.Activate();
                window.Focus();
            }
            catch { }
        }
    }
}
