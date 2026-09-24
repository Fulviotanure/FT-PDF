using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using FtPdfLite.Models;
using FtPdfLite.Services;
using PdfiumViewer;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Brushes = System.Windows.Media.Brushes;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using Clipboard = System.Windows.Clipboard;
using Path = System.IO.Path;
using Cursors = System.Windows.Input.Cursors;
using Button = System.Windows.Controls.Button;
using Orientation = System.Windows.Controls.Orientation;
using StackPanel = System.Windows.Controls.StackPanel;
using DockPanel = System.Windows.Controls.DockPanel;
using Dock = System.Windows.Controls.Dock;
using TextBlock = System.Windows.Controls.TextBlock;
using Border = System.Windows.Controls.Border;
using TextBox = System.Windows.Controls.TextBox;
using Grid = System.Windows.Controls.Grid;
using ColumnDefinition = System.Windows.Controls.ColumnDefinition;
using System.Threading;
using Image = System.Windows.Controls.Image;
using Stretch = System.Windows.Media.Stretch;
using BitmapScalingMode = System.Windows.Media.BitmapScalingMode;
using DataObject = System.Windows.DataObject;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Point = System.Windows.Point;
using Vector = System.Windows.Vector;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using WindowChrome = System.Windows.Shell.WindowChrome;
using ControlTemplate = System.Windows.Controls.ControlTemplate;
using ContentPresenter = System.Windows.Controls.ContentPresenter;

namespace FtPdfLite
{
    public class TabDragData
    {
        public PdfDocumentTab Tab { get; set; } = null!;
        public string FilePath { get; set; } = string.Empty;
        public MainWindow? SourceWindow { get; set; }
        public bool HandledByTargetWindow { get; set; } = false;
    }

    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<PdfDocumentTab> _tabs = new();
        private PdfDocumentTab? _activeTab;
        private readonly PdfExtractionService _extractionService = new();
        private bool _isRawTextMode = false;
        private bool _isNotepadOpen = false;
        private bool _isPageSidebarOpen = false;
        private PdfDocumentTab? _splitTab = null;
        private readonly Dictionary<int, Image> _pageImageMap = new();
        private readonly Dictionary<int, Border> _pageCardMap = new();
        private int _activePageNum = 1;
        private CancellationTokenSource? _thumbnailCts;
        private PdfRenderer? _pdfRenderer;
        private PdfRenderer? _splitPdfRenderer;

        [System.Runtime.InteropServices.DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        private const int WM_GETMINMAXINFO = 0x0024;
        private const int MONITOR_DEFAULTTONEAREST = 0x00000002;

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr handle, int flags);

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        public MainWindow()
        {
            InitializeComponent();
            AdjustWindowToScreen();
            StateChanged += MainWindow_StateChanged;
            SizeChanged += (s, e) => UpdateTabsBar();
            PreviewKeyDown += MainWindow_PreviewKeyDown;
            InitializePdfRenderer();
            CheckCommandLineArgs();
            Loaded += async (s, e) => await UpdateService.AutoCheckOnStartupAsync(isLite: true, this);
            Loaded += (s, e) => 
            {
                AdjustWindowToScreen();
                CheckDefaultAppBanner();
            };
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            try
            {
                var helper = new System.Windows.Interop.WindowInteropHelper(this);
                var source = System.Windows.Interop.HwndSource.FromHwnd(helper.Handle);
                source?.AddHook(WindowProc);

                int preference = DWMWCP_ROUND;
                DwmSetWindowAttribute(helper.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
            }
            catch { }
        }

        private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_GETMINMAXINFO)
            {
                WmGetMinMaxInfo(hwnd, lParam);
                handled = true;
            }
            return IntPtr.Zero;
        }

        private static void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
        {
            var mmi = System.Runtime.InteropServices.Marshal.PtrToStructure<MINMAXINFO>(lParam);
            IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor != IntPtr.Zero)
            {
                var mi = new MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
                if (GetMonitorInfo(monitor, ref mi))
                {
                    mmi.ptMaxPosition.x = Math.Abs(mi.rcWork.Left - mi.rcMonitor.Left);
                    mmi.ptMaxPosition.y = Math.Abs(mi.rcWork.Top - mi.rcMonitor.Top);
                    mmi.ptMaxSize.x = Math.Abs(mi.rcWork.Right - mi.rcWork.Left);
                    mmi.ptMaxSize.y = Math.Abs(mi.rcWork.Bottom - mi.rcWork.Top);
                    mmi.ptMinTrackSize.x = 760;
                    mmi.ptMinTrackSize.y = 450;
                }
            }
            System.Runtime.InteropServices.Marshal.StructureToPtr(mmi, lParam, true);
        }

        private void AdjustWindowToScreen()
        {
            try
            {
                var workArea = SystemParameters.WorkArea;
                if (workArea.Width <= 0 || workArea.Height <= 0) return;

                MaxWidth = workArea.Width;
                MaxHeight = workArea.Height;

                if (workArea.Width <= 1366 || workArea.Height <= 768)
                {
                    WindowState = WindowState.Maximized;
                }
                else
                {
                    double targetW = Math.Min(1180, workArea.Width * 0.90);
                    double targetH = Math.Min(700, workArea.Height * 0.90);

                    Width = targetW;
                    Height = targetH;
                    Left = workArea.Left + (workArea.Width - targetW) / 2.0;
                    Top = workArea.Top + (workArea.Height - targetH) / 2.0;
                }
            }
            catch { }
        }

        private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control))
            {
                if (e.Key == System.Windows.Input.Key.O || e.Key == System.Windows.Input.Key.T)
                {
                    e.Handled = true;
                    BtnOpenFile_Click(this, new RoutedEventArgs());
                }
                else if (e.Key == System.Windows.Input.Key.W)
                {
                    if (_activeTab != null)
                    {
                        e.Handled = true;
                        CloseTab(_activeTab);
                    }
                }
                else if (e.Key == System.Windows.Input.Key.S)
                {
                    e.Handled = true;
                    BtnQuickSave_Click(this, new RoutedEventArgs());
                }
                else if (e.Key == System.Windows.Input.Key.Tab)
                {
                    if (_tabs.Count > 1 && _activeTab != null)
                    {
                        e.Handled = true;
                        int step = System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift) ? -1 : 1;
                        int idx = (_tabs.IndexOf(_activeTab) + step + _tabs.Count) % _tabs.Count;
                        SetActiveTab(_tabs[idx]);
                    }
                }
            }
        }

        private void CheckCommandLineArgs()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                if (args.Length > 1)
                {
                    for (int i = 1; i < args.Length; i++)
                    {
                        string path = args[i].Trim('"', ' ');
                        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                        {
                            OpenTab(path);
                        }
                    }
                }
            }
            catch {}
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    foreach (var file in files)
                    {
                        if (File.Exists(file) && file.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                        {
                            OpenTab(file);
                        }
                    }
                }
            }
            catch {}
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (BtnMaximize != null)
            {
                BtnMaximize.Content = WindowState == WindowState.Maximized ? "🗗" : "🗖";
                BtnMaximize.ToolTip = WindowState == WindowState.Maximized ? "Restaurar" : "Maximizar";
            }
            UpdateWindowCorners();
        }

        private void UpdateWindowCorners()
        {
            bool isMaximized = (WindowState == WindowState.Maximized);
            if (WindowRootBorder != null)
            {
                WindowRootBorder.CornerRadius = isMaximized ? new CornerRadius(0) : new CornerRadius(10);
                WindowRootBorder.BorderThickness = isMaximized ? new Thickness(0) : new Thickness(1);
                WindowRootBorder.Padding = isMaximized ? new Thickness(6) : new Thickness(0);
            }
            if (TitleBarBorder != null)
            {
                TitleBarBorder.CornerRadius = isMaximized ? new CornerRadius(0) : new CornerRadius(9, 9, 0, 0);
            }
        }



        #region Custom Integrated Window Controls (Min, Max, Close)

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = (WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
        }

        private void BtnCloseWindow_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject dep)
            {
                DependencyObject? current = dep;
                while (current != null && current != sender)
                {
                    if (current is System.Windows.Controls.Button)
                        return;
                    current = VisualTreeHelper.GetParent(current);
                }
            }

            if (e.ClickCount == 2)
            {
                BtnMaximize_Click(sender, e);
            }
            else if (e.LeftButton == MouseButtonState.Pressed)
            {
                try
                {
                    DragMove();
                }
                catch { }
            }
        }

        private void ScrollViewer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject dep)
            {
                DependencyObject? current = dep;
                while (current != null && current != sender)
                {
                    if (current is System.Windows.Controls.Button)
                        return;
                    if (current is Border b && b.Parent == PanelTabs)
                        return;
                    current = VisualTreeHelper.GetParent(current);
                }
            }

            TitleBar_MouseLeftButtonDown(sender, e);
        }

        #endregion

        #region Native Pdfium Viewer (WindowsFormsHost)

        private void InitializePdfRenderer()
        {
            if (_pdfRenderer != null) return;

            _pdfRenderer = new PdfRenderer
            {
                Dock = System.Windows.Forms.DockStyle.Fill,
                BackColor = System.Drawing.Color.FromArgb(19, 24, 43),
                CursorMode = PdfViewerCursorMode.TextSelection,
                ZoomMode = PdfViewerZoomMode.FitWidth
            };

            _pdfRenderer.DisplayRectangleChanged += (s, e) =>
            {
                Dispatcher.InvokeAsync(() =>
                {
                    if (_activeTab == null || _pdfRenderer == null) return;
                    int currentPage = _pdfRenderer.Page + 1;
                    if (currentPage != _activePageNum)
                    {
                        _activePageNum = currentPage;
                        TxtViewerPageInfo.Text = $"Página {currentPage} de {_activeTab.TotalPages}";
                        UpdatePageCardsHighlight();
                    }
                });
            };

            _pdfRenderer.ZoomChanged += (s, e) =>
            {
                Dispatcher.InvokeAsync(() =>
                {
                    if (_pdfRenderer != null)
                        UpdateZoomLabel(_pdfRenderer.Zoom);
                });
            };

            // Menu de contexto com clique direito para copiar texto ou selecionar tudo
            var contextMenu = new System.Windows.Forms.ContextMenuStrip();
            var copyItem = new System.Windows.Forms.ToolStripMenuItem("Copiar texto", null, (s, e) => _pdfRenderer.CopySelection());
            var selectAllItem = new System.Windows.Forms.ToolStripMenuItem("Selecionar tudo", null, (s, e) => _pdfRenderer.SelectAll());
            contextMenu.Items.Add(copyItem);
            contextMenu.Items.Add(selectAllItem);
            contextMenu.Opening += (s, e) =>
            {
                copyItem.Enabled = _pdfRenderer.IsTextSelected;
            };
            _pdfRenderer.ContextMenuStrip = contextMenu;

            PdfHost.Child = _pdfRenderer;
        }

        private void EnsureSplitRendererInitialized()
        {
            if (_splitPdfRenderer != null) return;

            _splitPdfRenderer = new PdfRenderer
            {
                Dock = System.Windows.Forms.DockStyle.Fill,
                BackColor = System.Drawing.Color.FromArgb(19, 24, 43),
                CursorMode = PdfViewerCursorMode.TextSelection,
                ZoomMode = PdfViewerZoomMode.FitWidth
            };

            var splitContextMenu = new System.Windows.Forms.ContextMenuStrip();
            var splitCopyItem = new System.Windows.Forms.ToolStripMenuItem("Copiar texto", null, (s, e) => _splitPdfRenderer.CopySelection());
            var splitSelectAllItem = new System.Windows.Forms.ToolStripMenuItem("Selecionar tudo", null, (s, e) => _splitPdfRenderer.SelectAll());
            splitContextMenu.Items.Add(splitCopyItem);
            splitContextMenu.Items.Add(splitSelectAllItem);
            splitContextMenu.Opening += (s, e) =>
            {
                splitCopyItem.Enabled = _splitPdfRenderer.IsTextSelected;
            };
            _splitPdfRenderer.ContextMenuStrip = splitContextMenu;

            SplitPdfHost.Child = _splitPdfRenderer;
        }

        /// <summary>
        /// Carrega o documento PDF diretamente no renderer vetorial nativo do Pdfium.
        /// </summary>
        private async Task LoadAndRenderTab(PdfDocumentTab tab, bool isSplit = false)
        {
            try
            {
                if (tab.PdfDoc == null)
                {
                    tab.PdfDoc = await Task.Run(() => 
                        !string.IsNullOrEmpty(tab.Password) 
                            ? PdfDocument.Load(tab.FilePath, tab.Password) 
                            : PdfDocument.Load(tab.FilePath));
                    if (tab.PdfDoc == null) return;
                    tab.TotalPages = tab.PdfDoc.PageCount;
                }

                if (isSplit)
                {
                    EnsureSplitRendererInitialized();
                    _splitPdfRenderer?.Load(tab.PdfDoc);
                    if (tab.CurrentPage > 0 && _splitPdfRenderer != null)
                    {
                        int zeroPage = Math.Clamp(tab.CurrentPage - 1, 0, tab.TotalPages - 1);
                        _splitPdfRenderer.Page = zeroPage;
                    }
                }
                else
                {
                    InitializePdfRenderer();
                    if (_pdfRenderer != null)
                    {
                        _pdfRenderer.Load(tab.PdfDoc);
                        if (tab.CurrentPage > 0)
                        {
                            int zeroPage = Math.Clamp(tab.CurrentPage - 1, 0, tab.TotalPages - 1);
                            _pdfRenderer.Page = zeroPage;
                        }
                        _activePageNum = _pdfRenderer.Page + 1;
                        TxtViewerPageInfo.Text = $"Página {_activePageNum} de {tab.TotalPages}";
                        UpdateZoomLabel(_pdfRenderer.Zoom);
                    }
                }
            }
            catch { }
        }

        private void UpdateZoomLabel(double zoom)
        {
            TxtZoomLevel.Text = $"{(int)Math.Round(zoom * 100)}%";
        }

        private void BtnZoomIn_Click(object sender, RoutedEventArgs e)
        {
            if (_pdfRenderer == null) return;
            _pdfRenderer.Zoom = Math.Min(4.0, _pdfRenderer.Zoom * 1.25);
            UpdateZoomLabel(_pdfRenderer.Zoom);
        }

        private void BtnZoomOut_Click(object sender, RoutedEventArgs e)
        {
            if (_pdfRenderer == null) return;
            _pdfRenderer.Zoom = Math.Max(0.25, _pdfRenderer.Zoom / 1.25);
            UpdateZoomLabel(_pdfRenderer.Zoom);
        }

        private void BtnZoomFit_Click(object sender, RoutedEventArgs e)
        {
            if (_pdfRenderer == null) return;
            _pdfRenderer.ZoomMode = PdfViewerZoomMode.FitWidth;
            UpdateZoomLabel(_pdfRenderer.Zoom);
        }

        #endregion

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            var settings = new SettingsWindow { Owner = this };
            settings.ShowDialog();
            CheckDefaultAppBanner();
        }

        private void BtnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Arquivos PDF (*.pdf)|*.pdf|Todos os Arquivos (*.*)|*.*",
                Multiselect = true,
                Title = "Selecionar Arquivos PDF"
            };

            if (dialog.ShowDialog(this) == true)
            {
                foreach (var fileName in dialog.FileNames)
                {
                    OpenTab(fileName);
                }
            }
        }

        public void OpenTab(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath))
                    return;

                try { filePath = Path.GetFullPath(filePath); } catch { }

                if (!File.Exists(filePath))
                {
                    MessageBox.Show(this, $"O arquivo não foi encontrado:\n{filePath}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var existing = _tabs.FirstOrDefault(t => t.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    SetActiveTab(existing);
                    return;
                }

                // Check overflow: if tabs are already at capacity (at minimum width limit ~60px)
                double totalWidth = TitleBarBorder?.ActualWidth > 0 ? TitleBarBorder.ActualWidth : (ActualWidth > 0 ? ActualWidth : 1200);
                double usableWidth = Math.Max(200, totalWidth - 400);
                int maxPossibleTabs = Math.Max(1, (int)(usableWidth / 60.0));

                if (_tabs.Count >= maxPossibleTabs && _tabs.Count > 0)
                {
                    // Open in a new window to prevent tab bar overflow beyond limits!
                    var newWin = new MainWindow();
                    newWin.Show();
                    newWin.OpenTab(filePath);
                    return;
                }

                // Tenta abrir o documento para verificar integridade e detecção de senha
                PdfDocument? initialDoc = null;
                string? password = null;

                try
                {
                    initialDoc = PdfDocument.Load(filePath);
                }
                catch (PdfException pex) when (pex.Error == PdfError.PasswordProtected || pex.Message.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var dlg = new PasswordPromptDialog(filePath) { Owner = this };
                    if (dlg.ShowDialog() == true && dlg.LoadedDocument != null)
                    {
                        initialDoc = dlg.LoadedDocument;
                        password = dlg.EnteredPassword;
                    }
                    else
                    {
                        // Usuário cancelou a abertura do arquivo protegido
                        return;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Erro ao abrir o arquivo PDF:\n{ex.Message}", "Falha na Leitura", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var tab = new PdfDocumentTab
                {
                    FilePath = filePath,
                    Password = password,
                    PdfDoc = initialDoc,
                    TotalPages = initialDoc?.PageCount ?? 1
                };

                _tabs.Add(tab);
                SetActiveTab(tab);

                // Run extraction & integrity analysis in background
                _ = Task.Run(() =>
                {
                    var result = _extractionService.ExtractAndAnalyze(filePath, password);
                    Dispatcher.Invoke(() =>
                    {
                        tab.Extraction = result;
                        tab.TotalPages = result.Report.TotalPages > 0 ? result.Report.TotalPages : (initialDoc?.PageCount ?? 1);
                        if (_activeTab == tab)
                        {
                            UpdateNotepadView();
                            UpdatePageCards();
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Erro ao abrir o arquivo PDF:\n{ex.Message}", "Falha na Leitura", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SetActiveTab(PdfDocumentTab tab)
        {
            if (_activeTab != null && _pdfRenderer != null)
            {
                _activeTab.CurrentPage = _pdfRenderer.Page + 1;
                _activeTab.ZoomLevel = _pdfRenderer.Zoom;
            }

            _activeTab = tab;
            _activePageNum = tab.CurrentPage;
            Title = $"{tab.FileName} - FT PDF Lite";
            UpdateTabsBar();

            PanelEmptyState.Visibility = Visibility.Collapsed;
            PanelActiveContent.Visibility = Visibility.Visible;
            BtnQuickSave.Visibility = Visibility.Visible;
            BtnQuickCopy.Visibility = Visibility.Visible;
            BtnToggleNotepad.Visibility = Visibility.Visible;
            BtnTogglePages.Visibility = Visibility.Visible;

            // Load and render via native Pdfium (WindowsFormsHost)
            _ = LoadAndRenderTab(tab, isSplit: false);
            UpdateNotepadView();
            UpdatePageCards();
            StartThumbnailGeneration(tab);
        }

        private void CloseTab(PdfDocumentTab tab)
        {
            int index = _tabs.IndexOf(tab);
            tab.Dispose();
            _tabs.Remove(tab);

            if (_activeTab == tab)
            {
                if (_tabs.Count > 0)
                {
                    int newIndex = Math.Clamp(index - 1, 0, _tabs.Count - 1);
                    SetActiveTab(_tabs[newIndex]);
                }
                else
                {
                    CloseAllDocuments();
                }
            }
            else
            {
                UpdateTabsBar();
            }
        }

        private void CloseAllDocuments()
        {
            _thumbnailCts?.Cancel();
            _pageImageMap.Clear();
            _pageCardMap.Clear();
            _activeTab = null;
            Title = "FT PDF Lite - Leitor e Validador";
            PanelEmptyState.Visibility = Visibility.Visible;
            PanelActiveContent.Visibility = Visibility.Collapsed;
            PdfHost.Child = null;
            _pdfRenderer?.Dispose();
            _pdfRenderer = null;
            SplitPdfHost.Child = null;
            _splitPdfRenderer?.Dispose();
            _splitPdfRenderer = null;
            BtnQuickSave.Visibility = Visibility.Collapsed;
            BtnQuickCopy.Visibility = Visibility.Collapsed;
            BtnToggleNotepad.Visibility = Visibility.Collapsed;
            BtnTogglePages.Visibility = Visibility.Collapsed;
            ClosePageSidebar();
            ExitSplitScreen(mergeBack: false);
            CloseNotepad();
            UpdateTabsBar();
        }

        private void UpdateTabsBar()
        {
            PanelTabs.Children.Clear();

            double totalWidth = TitleBarBorder?.ActualWidth > 0 ? TitleBarBorder.ActualWidth : (ActualWidth > 0 ? ActualWidth : 1200);
            double logoWidth = 140;
            double controlsWidth = 145;
            double newTabBtnsWidth = 45; // Apenas botão (+)
            double spacerMinWidth = 90; // Garante espaço confortável para clicar e arrastar a janela
            double usableWidth = Math.Max(120, totalWidth - logoWidth - controlsWidth - newTabBtnsWidth - spacerMinWidth);
            int count = _tabs.Count;

            double maxTabWidth = 240;
            double minTabWidth = 60;
            double targetWidth = count > 0 ? (usableWidth / count) : maxTabWidth;
            double tabWidth = Math.Clamp(targetWidth, minTabWidth, maxTabWidth);

            foreach (var tab in _tabs)
            {
                bool isActive = (tab == _activeTab);

                var tabBorder = new Border
                {
                    Background = new SolidColorBrush(isActive 
                        ? (Color)ColorConverter.ConvertFromString("#182234") 
                        : Colors.Transparent),
                    BorderBrush = new SolidColorBrush(isActive 
                        ? (Color)ColorConverter.ConvertFromString("#334155") 
                        : Colors.Transparent),
                    BorderThickness = new Thickness(1, 1, 1, 0),
                    CornerRadius = new CornerRadius(7, 7, 0, 0),
                    Width = tabWidth,
                    Height = 35,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Padding = new Thickness(Math.Min(12, Math.Max(6, tabWidth * 0.06)), 0, Math.Min(8, Math.Max(4, tabWidth * 0.04)), 0),
                    Margin = new Thickness(0, 0, 3, 0),
                    Cursor = Cursors.Hand,
                    ToolTip = $"{tab.FileName}\n(Arraste para fora para dividir a tela lado a lado)"
                };
                WindowChrome.SetIsHitTestVisibleInChrome(tabBorder, true);

                if (!isActive)
                {
                    tabBorder.MouseEnter += (s, e) =>
                    {
                        tabBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#162032"));
                        tabBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D3748"));
                    };
                    tabBorder.MouseLeave += (s, e) =>
                    {
                        tabBorder.Background = Brushes.Transparent;
                        tabBorder.BorderBrush = Brushes.Transparent;
                    };
                }

                var dp = new DockPanel { LastChildFill = true, VerticalAlignment = VerticalAlignment.Center };

                FrameworkElement iconElement;
                if (tab.IsPasswordProtected)
                {
                    iconElement = new TextBlock
                    {
                        Text = "🔒",
                        FontSize = 11,
                        Margin = new Thickness(0, 0, tabWidth > 80 ? 8 : 4, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        ToolTip = "Documento Protegido por Senha"
                    };
                }
                else
                {
                    try
                    {
                        var img = new Image
                        {
                            Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/logo.png")),
                            Width = 15,
                            Height = 15,
                            Margin = new Thickness(0, 0, tabWidth > 80 ? 8 : 4, 0),
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                        iconElement = img;
                    }
                    catch
                    {
                        iconElement = new TextBlock
                        {
                            Text = "📄",
                            FontSize = 11,
                            Margin = new Thickness(0, 0, tabWidth > 80 ? 8 : 4, 0),
                            VerticalAlignment = VerticalAlignment.Center
                        };
                    }
                }
                DockPanel.SetDock(iconElement, Dock.Left);

                var closeBtn = new Button
                {
                    Content = "✕",
                    FontSize = 9.5,
                    FontWeight = FontWeights.Bold,
                    Width = 20,
                    Height = 20,
                    Margin = new Thickness(tabWidth > 80 ? 6 : 2, 0, 0, 0),
                    Background = Brushes.Transparent,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8")),
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    ToolTip = "Fechar aba (Ctrl+W)"
                };
                closeBtn.Click += (s, e) =>
                {
                    e.Handled = true;
                    CloseTab(tab);
                };

                var closeStyle = new Style(typeof(Button));
                var closeTemplate = new ControlTemplate(typeof(Button));
                var bdFactory = new FrameworkElementFactory(typeof(Border), "bd");
                bdFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
                bdFactory.SetValue(Border.BackgroundProperty, Brushes.Transparent);
                var cpFactory = new FrameworkElementFactory(typeof(ContentPresenter));
                cpFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                cpFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
                bdFactory.AppendChild(cpFactory);
                closeTemplate.VisualTree = bdFactory;

                var trigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
                trigger.Setters.Add(new Setter { TargetName = "bd", Property = Border.BackgroundProperty, Value = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155")) });
                trigger.Setters.Add(new Setter { Property = Button.ForegroundProperty, Value = Brushes.White });
                closeTemplate.Triggers.Add(trigger);
                closeStyle.Setters.Add(new Setter(Button.TemplateProperty, closeTemplate));
                closeBtn.Style = closeStyle;

                DockPanel.SetDock(closeBtn, Dock.Right);

                double maxTitleWidth = Math.Max(12, tabWidth - (tabWidth > 80 ? 54 : 32));
                var title = new TextBlock
                {
                    Text = tab.FileName,
                    FontSize = 12,
                    FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = new SolidColorBrush(isActive ? Colors.White : (Color)ColorConverter.ConvertFromString("#94A3B8")),
                    MaxWidth = maxTitleWidth,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center
                };

                dp.Children.Add(iconElement);
                dp.Children.Add(closeBtn);
                dp.Children.Add(title);
                tabBorder.Child = dp;

                Point dragStartPoint = new Point();
                bool isMouseDown = false;

                tabBorder.PreviewMouseLeftButtonDown += (s, e) =>
                {
                    DependencyObject? cur = e.OriginalSource as DependencyObject;
                    while (cur != null && cur != tabBorder)
                    {
                        if (cur is Button) return;
                        cur = VisualTreeHelper.GetParent(cur);
                    }
                    dragStartPoint = e.GetPosition(null);
                    isMouseDown = true;
                };

                tabBorder.PreviewMouseMove += (s, e) =>
                {
                    if (isMouseDown && e.LeftButton == MouseButtonState.Pressed)
                    {
                        Point currentPoint = e.GetPosition(null);
                        Vector diff = dragStartPoint - currentPoint;
                        if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                            Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                        {
                            isMouseDown = false;
                            try
                            {
                                var dragData = new TabDragData
                                {
                                    Tab = tab,
                                    FilePath = tab.FilePath,
                                    SourceWindow = this,
                                    HandledByTargetWindow = false
                                };

                                var dataObj = new DataObject();
                                dataObj.SetData(typeof(TabDragData), dragData);
                                dataObj.SetData(DataFormats.FileDrop, new string[] { tab.FilePath });
                                dataObj.SetData(DataFormats.Text, tab.FilePath);

                                DragDrop.DoDragDrop(tabBorder, dataObj, DragDropEffects.Move);

                                // Se não foi solto na barra de outra janela FT PDF Lite
                                if (!dragData.HandledByTargetWindow)
                                {
                                    var curPos = System.Windows.Forms.Cursor.Position;
                                    Point windowPoint = PointFromScreen(new Point(curPos.X, curPos.Y));
                                    bool isInsideTitleBar = (windowPoint.X >= 0 && windowPoint.X <= ActualWidth &&
                                                             windowPoint.Y >= 0 && windowPoint.Y <= (TitleBarBorder?.ActualHeight ?? 44));

                                    // Se soltou fora da barra de abas, cria uma nova janela destacada!
                                    if (!isInsideTitleBar)
                                    {
                                        string path = tab.FilePath;
                                        if (_tabs.Count == 1)
                                        {
                                            // Se for a única aba da janela atual, reposiciona a janela para onde foi solto
                                            WindowState = WindowState.Normal;
                                            Left = Math.Max(0, curPos.X - 200);
                                            Top = Math.Max(0, curPos.Y - 20);
                                            Activate();
                                        }
                                        else
                                        {
                                            // Havendo mais abas, destaca a aba em uma nova janela independente
                                            var newWin = new MainWindow
                                            {
                                                WindowStartupLocation = WindowStartupLocation.Manual,
                                                Left = Math.Max(0, curPos.X - 200),
                                                Top = Math.Max(0, curPos.Y - 20)
                                            };
                                            newWin.Show();
                                            newWin.OpenTab(path);
                                            newWin.Activate();

                                            // Remove a aba da janela de origem
                                            DetachTabAndCloseIfEmpty(tab);
                                        }
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                };

                tabBorder.PreviewMouseLeftButtonUp += (s, e) =>
                {
                    if (isMouseDown)
                    {
                        isMouseDown = false;
                        if (tab != _activeTab)
                        {
                            SetActiveTab(tab);
                        }
                    }
                };

                tabBorder.MouseDown += (s, e) =>
                {
                    if (e.ChangedButton == MouseButton.Middle && e.ButtonState == MouseButtonState.Pressed)
                    {
                        e.Handled = true;
                        CloseTab(tab);
                    }
                };

                PanelTabs.Children.Add(tabBorder);
            }
        }

        #region Split Screen & Drag Drop

        private void ScrollTabs_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateTabsBar();
        }

        private void TabsBar_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(TabDragData)) || e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
            }
        }

        private void TabsBar_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(TabDragData)))
            {
                var data = e.Data.GetData(typeof(TabDragData)) as TabDragData;
                if (data != null)
                {
                    data.HandledByTargetWindow = true;

                    if (data.SourceWindow == this)
                    {
                        e.Handled = true;
                        return;
                    }

                    // Solto vindo de OUTRA janela para a barra desta janela!
                    string filePath = data.FilePath;
                    var srcWin = data.SourceWindow;

                    // 1. Remove da janela de origem (fecha a janela de origem se ela não tiver mais abas)
                    if (srcWin != null && data.Tab != null)
                    {
                        srcWin.Dispatcher.InvokeAsync(() =>
                        {
                            srcWin.DetachTabAndCloseIfEmpty(data.Tab);
                        });
                    }

                    // 2. Abre a aba nesta janela de destino
                    OpenTab(filePath);
                    Activate();
                    e.Handled = true;
                    return;
                }
            }
            else if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null)
                {
                    foreach (var file in files)
                    {
                        if (file.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                        {
                            OpenTab(file);
                        }
                    }
                    e.Handled = true;
                }
            }
        }

        private async void OpenInSplitScreen(PdfDocumentTab tab)
        {
            if (_splitTab == tab) return;

            // Se só tiver 1 aba e for a ativa, não faz split de si mesmo
            if (_tabs.Count <= 1 && _activeTab == tab) return;

            _splitTab = tab;
            _tabs.Remove(tab);

            if (_activeTab == tab && _tabs.Count > 0)
            {
                SetActiveTab(_tabs[0]);
            }
            else
            {
                UpdateTabsBar();
            }

            TxtSplitFileName.Text = tab.FileName;
            ColSecondaryViewer.Width = new GridLength(1, GridUnitType.Star);
            SplitViewSplitter.Visibility = Visibility.Visible;
            PanelSplitViewer.Visibility = Visibility.Visible;

            // Native Pdfium rendering — no WebView initialization needed
            await LoadAndRenderTab(tab, isSplit: true);
        }

        private void ExitSplitScreen(bool mergeBack)
        {
            var tab = _splitTab;
            _splitTab = null;

            ColSecondaryViewer.Width = new GridLength(0);
            SplitViewSplitter.Visibility = Visibility.Collapsed;
            PanelSplitViewer.Visibility = Visibility.Collapsed;

            SplitPdfHost.Child = null;
            _splitPdfRenderer?.Dispose();
            _splitPdfRenderer = null;

            if (tab != null)
            {
                if (mergeBack)
                {
                    _tabs.Add(tab);
                    SetActiveTab(tab);
                }
                else
                {
                    tab.Dispose();
                    UpdateTabsBar();
                }
            }
        }

        private void BtnMergeSplitTab_Click(object sender, RoutedEventArgs e)
        {
            ExitSplitScreen(mergeBack: true);
        }

        private void BtnCloseSplitTab_Click(object sender, RoutedEventArgs e)
        {
            ExitSplitScreen(mergeBack: false);
        }

        private Point _splitDragStartPoint;
        private bool _isSplitMouseDown = false;

        private void SplitHeaderBar_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _splitDragStartPoint = e.GetPosition(null);
            _isSplitMouseDown = true;
        }

        private void SplitHeaderBar_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_isSplitMouseDown && e.LeftButton == MouseButtonState.Pressed && _splitTab != null)
            {
                Point currentPoint = e.GetPosition(null);
                Vector diff = _splitDragStartPoint - currentPoint;
                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    _isSplitMouseDown = false;
                    try
                    {
                        var dragData = new TabDragData
                        {
                            Tab = _splitTab,
                            FilePath = _splitTab.FilePath,
                            SourceWindow = this
                        };

                        var dataObj = new DataObject();
                        dataObj.SetData(typeof(TabDragData), dragData);
                        dataObj.SetData(DataFormats.FileDrop, new string[] { _splitTab.FilePath });
                        dataObj.SetData(DataFormats.Text, _splitTab.FilePath);

                        DragDrop.DoDragDrop(SplitHeaderBar, dataObj, DragDropEffects.Move);
                    }
                    catch { }
                }
            }
        }

        private void SplitHeaderBar_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isSplitMouseDown = false;
        }

        public void DetachTabAndCloseIfEmpty(PdfDocumentTab tab)
        {
            int index = _tabs.IndexOf(tab);
            if (index >= 0)
            {
                tab.Dispose();
                _tabs.RemoveAt(index);
            }

            if (_tabs.Count == 0)
            {
                // Se não restou mais nenhuma aba nesta janela, fecha e faz sumir a janela!
                Close();
            }
            else
            {
                if (_activeTab == tab || _activeTab == null)
                {
                    int newIndex = Math.Clamp(index, 0, _tabs.Count - 1);
                    SetActiveTab(_tabs[newIndex]);
                }
                else
                {
                    UpdateTabsBar();
                }
            }
        }

        public void RemoveTabByPath(string filePath)
        {
            var tab = _tabs.FirstOrDefault(t => t.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
            if (tab != null)
            {
                DetachTabAndCloseIfEmpty(tab);
            }
        }

        #endregion

        #region Page Navigator Sidebar

        private void BtnTogglePages_Click(object sender, RoutedEventArgs e)
        {
            if (_isPageSidebarOpen) ClosePageSidebar(); else OpenPageSidebar();
        }

        private void OpenPageSidebar()
        {
            _isPageSidebarOpen = true;
            ColPageSidebar.Width = new GridLength(200);
            PanelPageSidebar.Visibility = Visibility.Visible;
            BtnTogglePages.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"));
            BtnTogglePages.Foreground = Brushes.White;
            UpdatePageCards();
            if (_activeTab != null) StartThumbnailGeneration(_activeTab);
        }

        private void ClosePageSidebar()
        {
            _isPageSidebarOpen = false;
            ColPageSidebar.Width = new GridLength(0);
            PanelPageSidebar.Visibility = Visibility.Collapsed;
            BtnTogglePages.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B"));
            BtnTogglePages.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#38BDF8"));
        }

        private void BtnClosePageSidebar_Click(object sender, RoutedEventArgs e) => ClosePageSidebar();

        private void UpdatePageCards()
        {
            PanelPageCards.Children.Clear();
            _pageImageMap.Clear();
            _pageCardMap.Clear();

            if (_activeTab == null || _activeTab.TotalPages <= 0)
            {
                TxtPageCountBadge.Text = "0 pág.";
                return;
            }

            int total = _activeTab.TotalPages;
            TxtPageCountBadge.Text = $"{total} pág.";

            for (int i = 1; i <= total; i++)
            {
                int pageNum = i;
                bool isActive = (pageNum == _activePageNum);

                var card = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isActive ? "#1E293B" : "#131D31")),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isActive ? "#38BDF8" : "#1E293B")),
                    BorderThickness = new Thickness(isActive ? 2 : 1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(8),
                    Margin = new Thickness(0, 0, 0, 10),
                    Cursor = Cursors.Hand,
                    ToolTip = $"Página {pageNum} - Clique para ir"
                };

                var sp = new StackPanel();

                // Visual Paper Preview Frame
                var paperBorder = new Border
                {
                    Background = Brushes.White,
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155")),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(3),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 8),
                    MaxHeight = 220,
                    ClipToBounds = true
                };

                var imgThumb = new Image
                {
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    MinWidth = 80,
                    MinHeight = 100
                };
                RenderOptions.SetBitmapScalingMode(imgThumb, BitmapScalingMode.HighQuality);

                if (_activeTab.Thumbnails.TryGetValue(pageNum, out var cachedThumb) && cachedThumb != null)
                {
                    imgThumb.Source = cachedThumb;
                }
                else
                {
                    _pageImageMap[pageNum] = imgThumb;
                }

                paperBorder.Child = imgThumb;
                sp.Children.Add(paperBorder);

                // Page Number Pill Badge
                var badge = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isActive ? "#1E3A8A" : "#1E293B")),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10, 3, 10, 3),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                badge.Child = new TextBlock
                {
                    Text = $"Página {pageNum}",
                    FontSize = 10.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isActive ? "#93C5FD" : "#94A3B8"))
                };
                sp.Children.Add(badge);

                card.Child = sp;

                card.MouseEnter += (s, e) =>
                {
                    if (pageNum != _activePageNum)
                    {
                        card.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B"));
                        card.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#38BDF8"));
                    }
                };

                card.MouseLeave += (s, e) =>
                {
                    if (pageNum != _activePageNum)
                    {
                        card.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#131D31"));
                        card.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B"));
                    }
                };

                card.MouseLeftButtonDown += (s, e) =>
                {
                    JumpToPage(pageNum);
                };

                _pageCardMap[pageNum] = card;
                PanelPageCards.Children.Add(card);
            }
        }

        private void StartThumbnailGeneration(PdfDocumentTab tab)
        {
            _thumbnailCts?.Cancel();
            _thumbnailCts = new CancellationTokenSource();
            var token = _thumbnailCts.Token;

            Task.Run(() =>
            {
                try
                {
                    if (!File.Exists(tab.FilePath)) return;
                    using var doc = !string.IsNullOrEmpty(tab.Password)
                        ? PdfiumViewer.PdfDocument.Load(tab.FilePath, tab.Password)
                        : PdfiumViewer.PdfDocument.Load(tab.FilePath);
                    int total = doc.PageCount;

                    for (int i = 0; i < total; i++)
                    {
                        if (token.IsCancellationRequested) break;

                        int pageNum = i + 1;
                        if (!tab.Thumbnails.ContainsKey(pageNum))
                        {
                            var bs = PdfThumbnailService.RenderPageThumbnail(doc, i, dpi: 96);
                            if (bs != null)
                            {
                                tab.Thumbnails[pageNum] = bs;

                                Dispatcher.Invoke(() =>
                                {
                                    if (_activeTab == tab && _pageImageMap.TryGetValue(pageNum, out var imgCtrl))
                                    {
                                        imgCtrl.Source = bs;
                                    }
                                });
                            }
                        }
                    }
                }
                catch { }
            }, token);
        }

        private void UpdatePageCardsHighlight()
        {
            foreach (var kvp in _pageCardMap)
            {
                int pageNum = kvp.Key;
                var card = kvp.Value;
                bool isActive = (pageNum == _activePageNum);

                card.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isActive ? "#1E293B" : "#131D31"));
                card.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isActive ? "#38BDF8" : "#1E293B"));
                card.BorderThickness = new Thickness(isActive ? 2 : 1);

                if (card.Child is StackPanel sp && sp.Children.Count > 1 && sp.Children[1] is Border badge)
                {
                    badge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isActive ? "#1E3A8A" : "#1E293B"));
                    if (badge.Child is TextBlock tb)
                    {
                        tb.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isActive ? "#93C5FD" : "#94A3B8"));
                    }
                }
            }
        }

        private void JumpToPage(int pageNum)
        {
            if (_activeTab == null || string.IsNullOrWhiteSpace(_activeTab.FilePath)) return;

            _activePageNum = pageNum;
            UpdatePageCardsHighlight();

            if (_pageCardMap.TryGetValue(pageNum, out var card))
            {
                card.BringIntoView();
            }

            TxtViewerPageInfo.Text = $"Página {pageNum} de {_activeTab.TotalPages}";

            if (_pdfRenderer != null && _activeTab.TotalPages > 0)
            {
                int zeroPage = Math.Clamp(pageNum - 1, 0, _activeTab.TotalPages - 1);
                _pdfRenderer.Page = zeroPage;
            }
        }

        #endregion

        #region Validation & Notepad Panel

        private void UpdateNotepadView()
        {
            if (_activeTab?.Extraction == null) return;

            var report = _activeTab.Extraction.Report;
            var props = _activeTab.Extraction.Properties;

            TxtEditor.Text = _isRawTextMode ? _activeTab.Extraction.RawText : _activeTab.Extraction.FormattedText;
            TxtEditorMode.Text = _isRawTextMode ? "Modo: Texto Cru (Raw)" : "Modo: Layout Preservado";
            BtnToggleRawText.Content = _isRawTextMode ? "Exibir Texto Formatado" : "Exibir Texto Cru";

            TxtHeaderDocType.Text = report.DocumentType;
            TxtIntegrityScore.Text = $"{report.IntegrityScore:0.0}% Integridade";
            TxtIntegrityStatusText.Text = report.IntegrityStatus;

            if (report.IntegrityScore >= 100.0)
            {
                var greenColor = (Color)ColorConverter.ConvertFromString("#10B981");
                TxtHeaderDocType.Foreground = new SolidColorBrush(greenColor);
                BadgeIntegrity.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E3A2F"));
                BadgeIntegrity.BorderBrush = new SolidColorBrush(greenColor);
                TxtIntegrityScore.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#34D399"));
            }
            else if (report.IntegrityScore >= 70.0)
            {
                var yellowColor = (Color)ColorConverter.ConvertFromString("#F59E0B");
                TxtHeaderDocType.Foreground = new SolidColorBrush(yellowColor);
                BadgeIntegrity.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3D3215"));
                BadgeIntegrity.BorderBrush = new SolidColorBrush(yellowColor);
                TxtIntegrityScore.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FCD34D"));
            }
            else if (report.IntegrityScore >= 35.0)
            {
                var orangeColor = (Color)ColorConverter.ConvertFromString("#F97316");
                TxtHeaderDocType.Foreground = new SolidColorBrush(orangeColor);
                BadgeIntegrity.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3E2619"));
                BadgeIntegrity.BorderBrush = new SolidColorBrush(orangeColor);
                TxtIntegrityScore.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FDBA74"));
            }
            else
            {
                var redColor = (Color)ColorConverter.ConvertFromString("#EF4444");
                TxtHeaderDocType.Foreground = new SolidColorBrush(redColor);
                BadgeIntegrity.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3E1C1E"));
                BadgeIntegrity.BorderBrush = new SolidColorBrush(redColor);
                TxtIntegrityScore.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FCA5A5"));
            }

            TxtImportVerdict.Text = report.ImportVerdict;
            if (report.ImportVerdict == "O arquivo importa")
            {
                TxtImportVerdictIcon.Text = "✅";
                BorderImportVerdict.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E3A2F"));
                BorderImportVerdict.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                TxtImportVerdict.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#34D399"));
            }
            else if (report.ImportVerdict.StartsWith("Atenção") ||
                     report.ImportVerdict.Contains("pode importar com falha") ||
                     report.ImportVerdict.Contains("pode importar com erros"))
            {
                TxtImportVerdictIcon.Text = "⚠️";
                BorderImportVerdict.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3D3215"));
                BorderImportVerdict.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
                TxtImportVerdict.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FCD34D"));
            }
            else if (report.ImportVerdict.Contains("grandes chances", StringComparison.OrdinalIgnoreCase) ||
                     report.ImportVerdict.Contains("Grandes chances"))
            {
                TxtImportVerdictIcon.Text = "⚠️";
                BorderImportVerdict.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3E2619"));
                BorderImportVerdict.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F97316"));
                TxtImportVerdict.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FDBA74"));
            }
            else
            {
                TxtImportVerdictIcon.Text = "⛔";
                BorderImportVerdict.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3E1C1E"));
                BorderImportVerdict.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                TxtImportVerdict.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FCA5A5"));
            }

            TxtDiagFormatting.Text = $"Formatação: {report.FormattingQuality}";
            TxtDiagStrangeChars.Text = $"Sinais Estranhos: {report.StrangeCharactersCount}";
            TxtDiagImages.Text = $"Imagens / Scans: {report.TotalImagesFound} ({report.ScannedPagesCount} pág. scan)";
            TxtDiagCharCount.Text = $"Total Caracteres: {report.TotalCharacters:N0}";

            TxtPropFileSize.Text = props.FileSize;
            TxtPropPdfVersion.Text = props.PdfVersion;
            TxtPropDimensions.Text = props.PageDimensions;
            TxtPropOrientation.Text = props.PageOrientation;
            TxtPropAuthorProducer.Text = $"{props.Author} / {props.Producer}";
            TxtPropCreationDate.Text = props.CreationDate;
            TxtPropSecurity.Text = props.Security;

            if (report.DiagnosticWarnings.Count > 0)
            {
                TxtDiagWarning.Text = string.Join(" • ", report.DiagnosticWarnings);
            }
            else
            {
                TxtDiagWarning.Text = "Texto bem estruturado e sem anomalias detectadas.";
            }

            UpdateEditorStats();
        }

        private void BtnToggleNotepad_Click(object sender, RoutedEventArgs e)
        {
            if (_isNotepadOpen) CloseNotepad(); else OpenNotepad();
        }

        private void OpenNotepad()
        {
            _isNotepadOpen = true;
            ColNotepad.Width = new GridLength(530);
            PanelNotepad.Visibility = Visibility.Visible;
            SplitterBar.Visibility = Visibility.Visible;
            BtnToggleNotepad.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"));
            BtnToggleNotepad.Foreground = Brushes.White;
        }

        private void CloseNotepad()
        {
            _isNotepadOpen = false;
            ColNotepad.Width = new GridLength(0);
            PanelNotepad.Visibility = Visibility.Collapsed;
            SplitterBar.Visibility = Visibility.Collapsed;
            BtnToggleNotepad.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B"));
            BtnToggleNotepad.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FACC15"));
        }

        private void BtnCloseNotepad_Click(object sender, RoutedEventArgs e) => CloseNotepad();

        private void BtnToggleRawText_Click(object sender, RoutedEventArgs e)
        {
            _isRawTextMode = !_isRawTextMode;
            UpdateNotepadView();
        }

        private void BtnQuickSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_activeTab == null || !File.Exists(_activeTab.FilePath)) return;

                var dialog = new SaveFileDialog
                {
                    Filter = "Arquivo PDF (*.pdf)|*.pdf|Todos os Arquivos (*.*)|*.*",
                    DefaultExt = "pdf",
                    FileName = Path.GetFileNameWithoutExtension(_activeTab.FilePath) + "_copia.pdf"
                };

                if (dialog.ShowDialog(this) == true)
                {
                    File.Copy(_activeTab.FilePath, dialog.FileName, overwrite: true);
                    MessageBox.Show(this, "Arquivo PDF salvo com sucesso!", "Salvo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Erro ao salvar o arquivo: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnQuickCopy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string textToCopy = _activeTab?.Extraction != null ? _activeTab.Extraction.FormattedText : TxtEditor.Text;
                if (!string.IsNullOrEmpty(textToCopy))
                {
                    Clipboard.SetText(textToCopy);
                    MessageBox.Show(this, "Conteúdo do PDF copiado para a Área de Transferência com sucesso!", "Copiado", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(this, "Nenhum texto disponível para copiar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Não foi possível copiar: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnCopyText_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(TxtEditor.Text))
                {
                    Clipboard.SetText(TxtEditor.Text);
                    MessageBox.Show(this, "Texto copiado para a Área de Transferência com sucesso!", "Copiado", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Não foi possível copiar: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnSaveTxt_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "Arquivo de Texto (*.txt)|*.txt|Todos os Arquivos (*.*)|*.*",
                    DefaultExt = "txt",
                    FileName = Path.GetFileNameWithoutExtension(_activeTab?.FilePath ?? "Documento") + "_extraido.txt"
                };

                if (dialog.ShowDialog(this) == true)
                {
                    File.WriteAllText(dialog.FileName, TxtEditor.Text);
                    MessageBox.Show(this, "Arquivo salvo com sucesso!", "Salvo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Erro ao salvar o arquivo: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnToggleWrap_Click(object sender, RoutedEventArgs e)
        {
            if (TxtEditor.TextWrapping == TextWrapping.Wrap)
            {
                TxtEditor.TextWrapping = TextWrapping.NoWrap;
                BtnToggleWrap.Content = "Quebrar Linha: OFF";
            }
            else
            {
                TxtEditor.TextWrapping = TextWrapping.Wrap;
                BtnToggleWrap.Content = "Quebrar Linha: ON";
            }
        }

        private void TxtEditor_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => UpdateEditorStats();

        private void UpdateEditorStats()
        {
            int chars = TxtEditor.Text.Length;
            int words = TxtEditor.Text.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
            TxtEditorStats.Text = $"Caracteres: {chars:N0} | Palavras: {words:N0}";
        }

        #endregion

        #region Default App Banner

        private void CheckDefaultAppBanner()
        {
            try
            {
                bool isDef = DefaultAppService.IsDefaultPdfReader(isLite: true);
                bool dismissed = DefaultAppService.IsDismissed(isLite: true);
                BannerDefaultApp.Visibility = (!isDef && !dismissed) ? Visibility.Visible : Visibility.Collapsed;
            }
            catch
            {
                BannerDefaultApp.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnSetDefaultBanner_Click(object sender, RoutedEventArgs e)
        {
            BannerDefaultApp.Visibility = Visibility.Collapsed;
            DefaultAppService.RegisterAndSetDefault(isLite: true);
        }

        private void BtnDismissDefaultBanner_Click(object sender, RoutedEventArgs e)
        {
            BannerDefaultApp.Visibility = Visibility.Collapsed;
            DefaultAppService.DismissPrompt(isLite: true);
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            foreach (var tab in _tabs) tab.Dispose();
            _tabs.Clear();
            base.OnClosed(e);
        }
    }
}
