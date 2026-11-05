using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CCPDF.Models;
using CCPDF.Services;
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

namespace CCPDF
{
    public class TabDragData
    {
        public PdfDocumentTab Tab { get; set; } = null!;
        public string FilePath { get; set; } = string.Empty;
        public MainWindow? SourceWindow { get; set; }
        public bool HandledByTargetWindow { get; set; } = false;
    }

    public class DarkMenuRenderer : System.Windows.Forms.ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkMenuColorTable()) { }
    }

    public class DarkMenuColorTable : System.Windows.Forms.ProfessionalColorTable
    {
        public override System.Drawing.Color ToolStripDropDownBackground => System.Drawing.Color.FromArgb(11, 15, 25);
        public override System.Drawing.Color ImageMarginGradientBegin => System.Drawing.Color.FromArgb(11, 15, 25);
        public override System.Drawing.Color ImageMarginGradientMiddle => System.Drawing.Color.FromArgb(11, 15, 25);
        public override System.Drawing.Color ImageMarginGradientEnd => System.Drawing.Color.FromArgb(11, 15, 25);
        public override System.Drawing.Color MenuBorder => System.Drawing.Color.FromArgb(30, 41, 59);
        public override System.Drawing.Color MenuItemBorder => System.Drawing.Color.FromArgb(16, 185, 129);
        public override System.Drawing.Color MenuItemSelected => System.Drawing.Color.FromArgb(19, 43, 36);
        public override System.Drawing.Color MenuStripGradientBegin => System.Drawing.Color.FromArgb(11, 15, 25);
        public override System.Drawing.Color MenuStripGradientEnd => System.Drawing.Color.FromArgb(11, 15, 25);
        public override System.Drawing.Color MenuItemSelectedGradientBegin => System.Drawing.Color.FromArgb(19, 43, 36);
        public override System.Drawing.Color MenuItemSelectedGradientEnd => System.Drawing.Color.FromArgb(19, 43, 36);
        public override System.Drawing.Color MenuItemPressedGradientBegin => System.Drawing.Color.FromArgb(24, 54, 46);
        public override System.Drawing.Color MenuItemPressedGradientEnd => System.Drawing.Color.FromArgb(24, 54, 46);
        public override System.Drawing.Color SeparatorDark => System.Drawing.Color.FromArgb(30, 41, 59);
        public override System.Drawing.Color SeparatorLight => System.Drawing.Color.Transparent;
    }

    public class WarmLightMenuRenderer : System.Windows.Forms.ToolStripProfessionalRenderer
    {
        public WarmLightMenuRenderer() : base(new WarmLightMenuColorTable()) { }
    }

    public class WarmLightMenuColorTable : System.Windows.Forms.ProfessionalColorTable
    {
        public override System.Drawing.Color ToolStripDropDownBackground => System.Drawing.Color.FromArgb(246, 250, 247); // #F6FAF7 Pastel Card Surface
        public override System.Drawing.Color ImageMarginGradientBegin => System.Drawing.Color.FromArgb(246, 250, 247);
        public override System.Drawing.Color ImageMarginGradientMiddle => System.Drawing.Color.FromArgb(246, 250, 247);
        public override System.Drawing.Color ImageMarginGradientEnd => System.Drawing.Color.FromArgb(246, 250, 247);
        public override System.Drawing.Color MenuBorder => System.Drawing.Color.FromArgb(197, 216, 202); // #C5D8CA Pastel Sage Border
        public override System.Drawing.Color MenuItemBorder => System.Drawing.Color.FromArgb(22, 163, 74); // #16A34A Emerald Accent
        public override System.Drawing.Color MenuItemSelected => System.Drawing.Color.FromArgb(216, 239, 224); // #D8EFE0 Pastel Mint Pill
        public override System.Drawing.Color MenuStripGradientBegin => System.Drawing.Color.FromArgb(246, 250, 247);
        public override System.Drawing.Color MenuStripGradientEnd => System.Drawing.Color.FromArgb(246, 250, 247);
        public override System.Drawing.Color MenuItemSelectedGradientBegin => System.Drawing.Color.FromArgb(216, 239, 224);
        public override System.Drawing.Color MenuItemSelectedGradientEnd => System.Drawing.Color.FromArgb(216, 239, 224);
        public override System.Drawing.Color MenuItemPressedGradientBegin => System.Drawing.Color.FromArgb(197, 231, 209);
        public override System.Drawing.Color MenuItemPressedGradientEnd => System.Drawing.Color.FromArgb(197, 231, 209);
        public override System.Drawing.Color SeparatorDark => System.Drawing.Color.FromArgb(197, 216, 202);
        public override System.Drawing.Color SeparatorLight => System.Drawing.Color.Transparent;
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
        private PdfSearchManager? _searchManager;
        private DispatcherTimer? _searchDebounceTimer;
        private string _lastSearchedText = string.Empty;
        private bool _commandLineArgsChecked = false;
        private bool _isDarkMode = true;
        public bool IsDarkMode => _isDarkMode;
        private System.Windows.Forms.ContextMenuStrip? _viewerContextMenu;
        private System.Windows.Forms.ToolStripMenuItem[]? _viewerMenuItems;
        private System.Windows.Forms.ContextMenuStrip? _splitContextMenu;
        private System.Windows.Forms.ToolStripMenuItem[]? _splitMenuItems;

        [System.Runtime.InteropServices.DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_DONOTROUND = 1;
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
            StateChanged += MainWindow_StateChanged;
            SizeChanged += (s, e) => UpdateTabsBar();
            PreviewKeyDown += MainWindow_PreviewKeyDown;
            LoadThemePreference();
            InitializePdfRenderer();
            ApplyTheme(_isDarkMode);
            _searchDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _searchDebounceTimer.Tick += (s, e) =>
            {
                _searchDebounceTimer.Stop();
                ExecuteSearch(TxtViewerSearch.Text);
            };
            Loaded += async (s, e) => await UpdateService.AutoCheckOnStartupAsync(isLite: true, this);
            Loaded += (s, e) => 
            {
                AdjustWindowToScreen();
                CheckDefaultAppBanner();
                if (!_commandLineArgsChecked)
                {
                    _commandLineArgsChecked = true;
                    CheckCommandLineArgs();
                }
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
                // Não marcar handled = true para que o WindowChromeWorker nativo do WPF
                // consiga processar o ajuste de bordas da área de cliente sem gerar bordas pretas!
                handled = false;
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
                    int workW = Math.Abs(mi.rcWork.Right - mi.rcWork.Left);
                    int workH = Math.Abs(mi.rcWork.Bottom - mi.rcWork.Top);
                    mmi.ptMaxPosition.x = mi.rcWork.Left - mi.rcMonitor.Left;
                    mmi.ptMaxPosition.y = mi.rcWork.Top - mi.rcMonitor.Top;
                    mmi.ptMaxSize.x = workW;
                    mmi.ptMaxSize.y = workH;
                    mmi.ptMaxTrackSize.x = workW;
                    mmi.ptMaxTrackSize.y = workH;
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

                // Não restringir MaxWidth/MaxHeight da Window para não travar o layout ao maximizar
                ClearValue(MaxWidthProperty);
                ClearValue(MaxHeightProperty);

                double targetW = Math.Min(1180, workArea.Width * 0.92);
                double targetH = Math.Min(700, workArea.Height * 0.90);
                targetW = Math.Max(MinWidth, targetW);
                targetH = Math.Max(MinHeight, targetH);

                Width = targetW;
                Height = targetH;
                Left = workArea.Left + Math.Max(0, (workArea.Width - targetW) / 2.0);
                Top = workArea.Top + Math.Max(0, (workArea.Height - targetH) / 2.0);
            }
            catch { }
        }

        private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Alt) && 
                (e.SystemKey == System.Windows.Input.Key.T || e.Key == System.Windows.Input.Key.T))
            {
                e.Handled = true;
                ApplyTheme(!_isDarkMode);
                return;
            }

            if (System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control))
            {
                if (e.Key == System.Windows.Input.Key.F)
                {
                    e.Handled = true;
                    if (PanelNotepad.Visibility == Visibility.Visible && (TxtEditor.IsKeyboardFocusWithin || TxtNotepadSearch.IsKeyboardFocusWithin))
                    {
                        TxtNotepadSearch.Focus();
                        TxtNotepadSearch.SelectAll();
                    }
                    else
                    {
                        TxtViewerSearch.Focus();
                        TxtViewerSearch.SelectAll();
                    }
                }
                else if (e.Key == System.Windows.Input.Key.O || e.Key == System.Windows.Input.Key.T)
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
            else if (e.Key == System.Windows.Input.Key.F3)
            {
                e.Handled = true;
                if (System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift))
                    SearchPrevMatch();
                else
                    SearchNextMatch();
            }
        }

        private async void CheckCommandLineArgs()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                if (args.Length > 1)
                {
                    // Permite que a janela principal finalize sua renderização inicial antes de abrir arquivos pesados
                    await Dispatcher.Yield(DispatcherPriority.Loaded);
                    for (int i = 1; i < args.Length; i++)
                    {
                        string raw = args[i].Trim('"', ' ');
                        if (!string.IsNullOrWhiteSpace(raw))
                        {
                            string path = raw;
                            try { path = Path.GetFullPath(raw); } catch { }
                            if (File.Exists(path))
                            {
                                OpenTab(path);
                            }
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

            try
            {
                var helper = new System.Windows.Interop.WindowInteropHelper(this);
                if (helper.Handle != IntPtr.Zero)
                {
                    int preference = isMaximized ? DWMWCP_DONOTROUND : DWMWCP_ROUND;
                    DwmSetWindowAttribute(helper.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
                }
            }
            catch { }
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

        [DllImport("uxtheme.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string? pszSubIdList);

        private static System.Windows.Forms.Cursor? _panHandCursor;

        private static System.Windows.Forms.Cursor GetPanHandCursor()
        {
            if (_panHandCursor != null) return _panHandCursor;
            try
            {
                using var stream = typeof(PdfRenderer).Assembly.GetManifestResourceStream("PdfiumViewer.pan.cur");
                if (stream != null)
                {
                    _panHandCursor = new System.Windows.Forms.Cursor(stream);
                    return _panHandCursor;
                }
            }
            catch { }
            return System.Windows.Forms.Cursors.SizeAll;
        }

        private void AttachRightClickPan(PdfRenderer renderer, System.Windows.Forms.ContextMenuStrip contextMenu)
        {
            bool isRightDragging = false;
            System.Drawing.Point dragStartPoint = System.Drawing.Point.Empty;
            System.Drawing.Point startOffsetPoint = System.Drawing.Point.Empty;
            bool hasMovedSignificantly = false;

            void ApplyThemeToRenderer()
            {
                try
                {
                    if (renderer.IsHandleCreated)
                    {
                        SetWindowTheme(renderer.Handle, _isDarkMode ? "DarkMode_Explorer" : "Explorer", null);
                    }
                }
                catch { }
            }

            if (renderer.IsHandleCreated)
                ApplyThemeToRenderer();
            else
                renderer.HandleCreated += (s, e) => ApplyThemeToRenderer();

            renderer.MouseDown += (s, e) =>
            {
                if (e.Button == System.Windows.Forms.MouseButtons.Right || e.Button == System.Windows.Forms.MouseButtons.Middle)
                {
                    isRightDragging = true;
                    dragStartPoint = e.Location;
                    startOffsetPoint = renderer.DisplayRectangle.Location;
                    hasMovedSignificantly = false;
                    renderer.Capture = true;
                    renderer.Cursor = GetPanHandCursor();
                }
            };

            renderer.MouseMove += (s, e) =>
            {
                if (isRightDragging)
                {
                    int dx = e.Location.X - dragStartPoint.X;
                    int dy = e.Location.Y - dragStartPoint.Y;

                    if (Math.Abs(dx) > 2 || Math.Abs(dy) > 2)
                    {
                        hasMovedSignificantly = true;
                        renderer.Cursor = GetPanHandCursor();

                        int targetX = startOffsetPoint.X + dx;
                        int targetY = startOffsetPoint.Y + dy;

                        renderer.SetDisplayRectLocation(new System.Drawing.Point(targetX, targetY));
                    }
                }
            };

            renderer.MouseUp += (s, e) =>
            {
                if ((e.Button == System.Windows.Forms.MouseButtons.Right || e.Button == System.Windows.Forms.MouseButtons.Middle) && isRightDragging)
                {
                    isRightDragging = false;
                    renderer.Capture = false;
                    renderer.Cursor = System.Windows.Forms.Cursors.Default;
                }
            };

            renderer.SetCursor += (s, e) =>
            {
                if (isRightDragging)
                {
                    e.Cursor = GetPanHandCursor();
                }
            };

            contextMenu.Opening += (s, e) =>
            {
                if (hasMovedSignificantly)
                {
                    e.Cancel = true;
                    hasMovedSignificantly = false;
                }
            };
        }

        private void ApplyContextMenuTheme(System.Windows.Forms.ContextMenuStrip? contextMenu, params System.Windows.Forms.ToolStripMenuItem[]? items)
        {
            if (contextMenu == null) return;
            try
            {
                if (_isDarkMode)
                {
                    contextMenu.Renderer = new DarkMenuRenderer();
                    contextMenu.BackColor = System.Drawing.Color.FromArgb(11, 15, 25);
                    contextMenu.ForeColor = System.Drawing.Color.FromArgb(248, 250, 252);
                    if (items != null)
                    {
                        foreach (var item in items)
                        {
                            item.ForeColor = System.Drawing.Color.FromArgb(248, 250, 252);
                            item.BackColor = System.Drawing.Color.FromArgb(11, 15, 25);
                        }
                    }
                }
                else
                {
                    contextMenu.Renderer = new WarmLightMenuRenderer();
                    contextMenu.BackColor = System.Drawing.Color.FromArgb(246, 250, 247);
                    contextMenu.ForeColor = System.Drawing.Color.FromArgb(19, 43, 30);
                    if (items != null)
                    {
                        foreach (var item in items)
                        {
                            item.ForeColor = System.Drawing.Color.FromArgb(19, 43, 30);
                            item.BackColor = System.Drawing.Color.FromArgb(246, 250, 247);
                        }
                    }
                }
            }
            catch { }
        }

        private void InitializePdfRenderer()
        {
            if (_pdfRenderer != null) return;

            _pdfRenderer = new PdfRenderer
            {
                Dock = System.Windows.Forms.DockStyle.Fill,
                BackColor = _isDarkMode ? System.Drawing.Color.FromArgb(11, 15, 25) : System.Drawing.Color.FromArgb(237, 232, 222),
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
            _viewerContextMenu = contextMenu;
            _viewerMenuItems = new[] { copyItem, selectAllItem };
            ApplyContextMenuTheme(_viewerContextMenu, _viewerMenuItems);
            _pdfRenderer.ContextMenuStrip = contextMenu;

            AttachRightClickPan(_pdfRenderer, contextMenu);

            _searchManager = new PdfSearchManager(_pdfRenderer);

            _pdfRenderer.KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == System.Windows.Forms.Keys.F)
                {
                    Dispatcher.Invoke(() =>
                    {
                        TxtViewerSearch.Focus();
                        TxtViewerSearch.SelectAll();
                    });
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
                else if (e.KeyCode == System.Windows.Forms.Keys.F3)
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (e.Shift)
                            SearchPrevMatch();
                        else
                            SearchNextMatch();
                    });
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };

            PdfHost.Child = _pdfRenderer;
        }

        private void EnsureSplitRendererInitialized()
        {
            if (_splitPdfRenderer != null) return;

            _splitPdfRenderer = new PdfRenderer
            {
                Dock = System.Windows.Forms.DockStyle.Fill,
                BackColor = _isDarkMode ? System.Drawing.Color.FromArgb(11, 15, 25) : System.Drawing.Color.FromArgb(237, 232, 222),
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
            _splitContextMenu = splitContextMenu;
            _splitMenuItems = new[] { splitCopyItem, splitSelectAllItem };
            ApplyContextMenuTheme(_splitContextMenu, _splitMenuItems);
            _splitPdfRenderer.ContextMenuStrip = splitContextMenu;

            AttachRightClickPan(_splitPdfRenderer, splitContextMenu);

            SplitPdfHost.Child = _splitPdfRenderer;
        }

        /// <summary>
        /// Carrega o documento PDF diretamente no renderer vetorial nativo do Pdfium.
        /// </summary>
        private async Task LoadAndRenderTab(PdfDocumentTab tab, bool isSplit = false)
        {
            try
            {
                if (tab.PdfDoc == null && File.Exists(tab.FilePath))
                {
                    tab.PdfDoc = !string.IsNullOrEmpty(tab.Password) 
                        ? PdfDocument.Load(tab.FilePath, tab.Password) 
                        : PdfDocument.Load(tab.FilePath);
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

                        if (!string.IsNullOrWhiteSpace(TxtViewerSearch.Text))
                        {
                            ExecuteSearch(TxtViewerSearch.Text);
                        }
                        else
                        {
                            _searchManager?.Reset();
                            BadgeSearchCount.Visibility = Visibility.Collapsed;
                            BtnSearchPrev.IsEnabled = false;
                            BtnSearchNext.IsEnabled = false;
                        }
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

        #region Search Functionality

        private void TxtViewerSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            string text = TxtViewerSearch.Text;
            TxtSearchPlaceholder.Visibility = string.IsNullOrEmpty(text) ? Visibility.Visible : Visibility.Collapsed;
            BtnClearSearch.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;

            _searchDebounceTimer?.Stop();
            if (string.IsNullOrEmpty(text))
            {
                _lastSearchedText = string.Empty;
                _searchManager?.Reset();
                BadgeSearchCount.Visibility = Visibility.Collapsed;
                BtnSearchPrev.IsEnabled = false;
                BtnSearchNext.IsEnabled = false;
            }
            else
            {
                _searchDebounceTimer?.Start();
            }
        }

        private void TxtViewerSearch_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                e.Handled = true;
                _searchDebounceTimer?.Stop();
                string q = TxtViewerSearch.Text.Trim();
                if (q != _lastSearchedText)
                {
                    ExecuteSearch(q);
                }
                else
                {
                    if (System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift))
                        SearchPrevMatch();
                    else
                        SearchNextMatch();
                }
            }
            else if (e.Key == System.Windows.Input.Key.Up)
            {
                e.Handled = true;
                SearchPrevMatch();
            }
            else if (e.Key == System.Windows.Input.Key.Down)
            {
                e.Handled = true;
                SearchNextMatch();
            }
            else if (e.Key == System.Windows.Input.Key.Escape)
            {
                e.Handled = true;
                BtnClearSearch_Click(this, new RoutedEventArgs());
                PdfHost.Focus();
            }
        }

        private void ExecuteSearch(string rawQuery)
        {
            string query = rawQuery?.Trim() ?? string.Empty;
            _lastSearchedText = query;

            if (string.IsNullOrEmpty(query))
            {
                _searchManager?.Reset();
                BadgeSearchCount.Visibility = Visibility.Collapsed;
                BtnSearchPrev.IsEnabled = false;
                BtnSearchNext.IsEnabled = false;
                return;
            }

            if (_searchManager == null && _pdfRenderer != null)
            {
                _searchManager = new PdfSearchManager(_pdfRenderer);
            }

            if (_searchManager == null || _activeTab?.PdfDoc == null)
            {
                TxtSearchCount.Text = "0 de 0";
                TxtSearchCount.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#F87171" : "#DC2626"));
                BadgeSearchCount.Visibility = Visibility.Visible;
                BtnSearchPrev.IsEnabled = false;
                BtnSearchNext.IsEnabled = false;
                return;
            }

            try
            {
                bool found = _searchManager.Search(query);
                int count = _searchManager.MatchCount;

                if (!found || count == 0)
                {
                    TxtSearchCount.Text = "0 de 0";
                    TxtSearchCount.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#F87171" : "#DC2626"));
                    BadgeSearchCount.Visibility = Visibility.Visible;
                    BtnSearchPrev.IsEnabled = false;
                    BtnSearchNext.IsEnabled = false;
                }
                else
                {
                    _searchManager.FindNext(forward: true);
                    UpdateSearchCounter();
                }
            }
            catch
            {
                TxtSearchCount.Text = "0 de 0";
                TxtSearchCount.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#F87171" : "#DC2626"));
                BadgeSearchCount.Visibility = Visibility.Visible;
                BtnSearchPrev.IsEnabled = false;
                BtnSearchNext.IsEnabled = false;
            }
        }

        private void BtnSearchNext_Click(object sender, RoutedEventArgs e)
        {
            SearchNextMatch();
        }

        private void BtnSearchPrev_Click(object sender, RoutedEventArgs e)
        {
            SearchPrevMatch();
        }

        private void SearchNextMatch()
        {
            if (_searchManager == null || _searchManager.MatchCount == 0) return;
            try
            {
                _searchManager.FindNext(forward: true);
                UpdateSearchCounter();
            }
            catch { }
        }

        private void SearchPrevMatch()
        {
            if (_searchManager == null || _searchManager.MatchCount == 0) return;
            try
            {
                _searchManager.FindNext(forward: false);
                UpdateSearchCounter();
            }
            catch { }
        }

        private void UpdateSearchCounter()
        {
            if (_searchManager == null || _searchManager.MatchCount == 0)
            {
                TxtSearchCount.Text = "0 de 0";
                TxtSearchCount.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#F87171" : "#DC2626"));
                BadgeSearchCount.Visibility = Visibility.Visible;
                BtnSearchPrev.IsEnabled = false;
                BtnSearchNext.IsEnabled = false;
                return;
            }

            int currentIdx = _searchManager.Offset >= 0 ? _searchManager.Offset + 1 : 1;
            int total = _searchManager.MatchCount;
            TxtSearchCount.Text = $"{currentIdx} de {total}";
            TxtSearchCount.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#4E9F6E" : "#0A6844"));
            BadgeSearchCount.Visibility = Visibility.Visible;
            BtnSearchPrev.IsEnabled = true;
            BtnSearchNext.IsEnabled = true;
        }

        private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            TxtViewerSearch.Text = string.Empty;
            _lastSearchedText = string.Empty;
            _searchManager?.Reset();
            BadgeSearchCount.Visibility = Visibility.Collapsed;
            BtnSearchPrev.IsEnabled = false;
            BtnSearchNext.IsEnabled = false;
            TxtViewerSearch.Focus();
        }

        #endregion

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

                OpenFileAsync(filePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Erro ao abrir arquivo:\n{ex.Message}", "Falha", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ShowOpeningFeedback(string fileName, double sizeMb, bool isLarge)
        {
            if (PanelOpeningFeedback == null) return;
            if (isLarge)
            {
                TxtOpeningIcon.Text = "📁";
                TxtOpeningTitle.Text = $"Abrindo Arquivo Grande ({sizeMb:0.1} MB)...";
                TxtOpeningDetail.Text = $"Detectado documento extenso: \"{fileName}\". Otimizando leitura e preparando visualização rápida de páginas...";
            }
            else
            {
                TxtOpeningIcon.Text = "⏳";
                TxtOpeningTitle.Text = $"Abrindo {fileName}...";
                TxtOpeningDetail.Text = "Carregando páginas e preparando visualização vetorial nativa...";
            }
            PanelOpeningFeedback.Visibility = Visibility.Visible;
            Mouse.OverrideCursor = Cursors.Wait;
        }

        private void HideOpeningFeedback()
        {
            if (PanelOpeningFeedback == null) return;
            PanelOpeningFeedback.Visibility = Visibility.Collapsed;
            Mouse.OverrideCursor = null;
        }

        private async void OpenFileAsync(string filePath)
        {
            try
            {
                // Verifica se há novas versões disponíveis ao abrir um arquivo para leitura
                _ = UpdateService.CheckForUpdatesAndPromptAsync(isLite: true, this, isStartup: false);

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

                // Verificar tamanho e exibir feedback visual imediato
                var fi = new FileInfo(filePath);
                double sizeMb = fi.Length / (1024.0 * 1024.0);
                bool isLarge = sizeMb >= 5.0;

                ShowOpeningFeedback(fi.Name, sizeMb, isLarge);
                await Dispatcher.Yield(DispatcherPriority.Render);

                // Tenta abrir o documento para verificar integridade e detecção de senha
                PdfDocument? initialDoc = null;
                string? password = null;

                try
                {
                    initialDoc = PdfDocument.Load(filePath);
                }
                catch (PdfException pex) when (pex.Error == PdfError.PasswordProtected || pex.Message.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    HideOpeningFeedback();
                    var dlg = new PasswordPromptDialog(filePath);
                    if (this.IsLoaded && this.IsVisible)
                    {
                        try { dlg.Owner = this; } catch { }
                    }
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
                    HideOpeningFeedback();
                    if (this.IsLoaded && this.IsVisible)
                    {
                        MessageBox.Show(this, $"Erro ao abrir o arquivo PDF:\n{ex.Message}", "Falha na Leitura", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    else
                    {
                        MessageBox.Show($"Erro ao abrir o arquivo PDF:\n{ex.Message}", "Falha na Leitura", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    return;
                }
                finally
                {
                    HideOpeningFeedback();
                }

                int totalPages = initialDoc?.PageCount ?? 1;
                var tab = new PdfDocumentTab
                {
                    FilePath = filePath,
                    Password = password,
                    PdfDoc = initialDoc,
                    TotalPages = totalPages
                };

                _tabs.Add(tab);
                SetActiveTab(tab);

                ShowAnalyzingFeedback(isLarge, sizeMb, totalPages);

                // Run extraction & integrity analysis in background with live progress updates
                _ = Task.Run(() =>
                {
                    var result = _extractionService.ExtractAndAnalyze(filePath, password, progress =>
                    {
                        Dispatcher.InvokeAsync(() =>
                        {
                            if (_activeTab == tab && tab.Extraction == null)
                            {
                                UpdateAnalysisProgress(progress);
                            }
                        });
                    });

                    Dispatcher.Invoke(() =>
                    {
                        tab.Extraction = result;
                        tab.TotalPages = result.Report.TotalPages > 0 ? result.Report.TotalPages : (initialDoc?.PageCount ?? 1);
                        if (_activeTab == tab)
                        {
                            HideAnalyzingFeedback();
                            UpdateNotepadView();
                            UpdatePageCards();
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                HideOpeningFeedback();
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
            Title = $"{tab.FileName} - CCPDF";
            UpdateTabsBar();

            PanelEmptyState.Visibility = Visibility.Collapsed;
            PanelActiveContent.Visibility = Visibility.Visible;
            BtnQuickSave.Visibility = Visibility.Visible;
            BtnToggleNotepad.Visibility = Visibility.Visible;
            BtnTogglePages.Visibility = Visibility.Visible;

            // Load and render via native Pdfium (WindowsFormsHost)
            _ = LoadAndRenderTab(tab, isSplit: false);
            UpdateNotepadView();
            UpdatePageCards();
            if (_isPageSidebarOpen || tab.TotalPages <= 5)
            {
                StartThumbnailGeneration(tab);
            }
        }

        private void CloseTab(PdfDocumentTab tab)
        {
            _thumbnailCts?.Cancel();
            int index = _tabs.IndexOf(tab);

            if (_activeTab == tab)
            {
                if (_tabs.Count > 1)
                {
                    int newIndex = (index == 0) ? 1 : index - 1;
                    var nextTab = _tabs[newIndex];
                    _tabs.Remove(tab);
                    SetActiveTab(nextTab);
                    tab.Dispose();
                }
                else
                {
                    _tabs.Remove(tab);
                    tab.Dispose();
                    CloseAllDocuments();
                }
            }
            else
            {
                _tabs.Remove(tab);
                tab.Dispose();
                UpdateTabsBar();
            }
        }

        private void CloseAllDocuments()
        {
            _thumbnailCts?.Cancel();
            _pageImageMap.Clear();
            _pageCardMap.Clear();
            _activeTab = null;
            Title = "CCPDF - Leitor e Validador";
            PanelEmptyState.Visibility = Visibility.Visible;
            PanelActiveContent.Visibility = Visibility.Collapsed;
            PdfHost.Child = null;
            _searchManager?.Reset();
            _searchManager = null;
            TxtViewerSearch.Text = string.Empty;
            _lastSearchedText = string.Empty;
            BadgeSearchCount.Visibility = Visibility.Collapsed;
            BtnSearchPrev.IsEnabled = false;
            BtnSearchNext.IsEnabled = false;
            BtnClearSearch.Visibility = Visibility.Collapsed;
            TxtNotepadSearch.Text = string.Empty;
            BadgeNotepadSearchCount.Visibility = Visibility.Collapsed;
            BtnNotepadSearchPrev.IsEnabled = false;
            BtnNotepadSearchNext.IsEnabled = false;
            _notepadMatchIndices.Clear();
            _notepadCurrentMatchIndex = -1;
            _pdfRenderer?.Dispose();
            _pdfRenderer = null;
            SplitPdfHost.Child = null;
            _splitPdfRenderer?.Dispose();
            _splitPdfRenderer = null;
            BtnQuickSave.Visibility = Visibility.Collapsed;
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

            Color tabActiveBg = _isDarkMode 
                ? (Color)ColorConverter.ConvertFromString("#111827") 
                : (Color)ColorConverter.ConvertFromString("#F0F5F1");
            Color tabActiveBorder = _isDarkMode 
                ? (Color)ColorConverter.ConvertFromString("#1E293B") 
                : (Color)ColorConverter.ConvertFromString("#C5D8CA");
            Color tabHoverBg = _isDarkMode 
                ? (Color)ColorConverter.ConvertFromString("#151F30") 
                : (Color)ColorConverter.ConvertFromString("#DBEDE0");
            Color tabHoverBorder = _isDarkMode 
                ? (Color)ColorConverter.ConvertFromString("#1E293B") 
                : (Color)ColorConverter.ConvertFromString("#BACFC0");
            Color closeBtnFg = _isDarkMode 
                ? (Color)ColorConverter.ConvertFromString("#94A3B8") 
                : (Color)ColorConverter.ConvertFromString("#3F5E4D");
            Color closeBtnHoverBg = _isDarkMode 
                ? (Color)ColorConverter.ConvertFromString("#334155") 
                : (Color)ColorConverter.ConvertFromString("#C5D8CA");
            Color closeBtnHoverFg = _isDarkMode 
                ? Colors.White 
                : (Color)ColorConverter.ConvertFromString("#132B1E");
            Color titleActiveFg = _isDarkMode 
                ? (Color)ColorConverter.ConvertFromString("#34D399") 
                : (Color)ColorConverter.ConvertFromString("#0E6E3B");
            Color titleInactiveFg = _isDarkMode 
                ? (Color)ColorConverter.ConvertFromString("#94A3B8") 
                : (Color)ColorConverter.ConvertFromString("#3F5E4D");

            foreach (var tab in _tabs)
            {
                bool isActive = (tab == _activeTab);

                var tabBorder = new Border
                {
                    Background = new SolidColorBrush(isActive ? tabActiveBg : Colors.Transparent),
                    BorderBrush = new SolidColorBrush(isActive ? tabActiveBorder : Colors.Transparent),
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
                        tabBorder.Background = new SolidColorBrush(tabHoverBg);
                        tabBorder.BorderBrush = new SolidColorBrush(tabHoverBorder);
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
                    Foreground = new SolidColorBrush(closeBtnFg),
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
                trigger.Setters.Add(new Setter { TargetName = "bd", Property = Border.BackgroundProperty, Value = new SolidColorBrush(closeBtnHoverBg) });
                trigger.Setters.Add(new Setter { Property = Button.ForegroundProperty, Value = new SolidColorBrush(closeBtnHoverFg) });
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
                    Foreground = new SolidColorBrush(isActive ? titleActiveFg : titleInactiveFg),
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

                                // Se não foi solto na barra de outra janela CCPDF
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
            UpdateSidebarToggleButtons();
            UpdatePageCards();
            if (_activeTab != null) StartThumbnailGeneration(_activeTab);
        }

        private void ClosePageSidebar()
        {
            _isPageSidebarOpen = false;
            ColPageSidebar.Width = new GridLength(0);
            PanelPageSidebar.Visibility = Visibility.Collapsed;
            UpdateSidebarToggleButtons();
        }

        private void BtnClosePageSidebar_Click(object sender, RoutedEventArgs e) => ClosePageSidebar();

        private void UpdatePageCards()
        {
            if (_activeTab == null || _activeTab.TotalPages <= 0)
            {
                TxtPageCountBadge.Text = "0 pág.";
                PanelPageCards.Children.Clear();
                _pageImageMap.Clear();
                _pageCardMap.Clear();
                return;
            }

            int total = _activeTab.TotalPages;
            TxtPageCountBadge.Text = $"{total} pág.";

            // Otimização: se a barra lateral de páginas estiver fechada, não gasta processamento construindo dezenas de controles
            if (!_isPageSidebarOpen && total > 5)
            {
                return;
            }

            PanelPageCards.Children.Clear();
            _pageImageMap.Clear();
            _pageCardMap.Clear();

            string cardActiveBg = _isDarkMode ? "#1B3B2D" : "#DBEDE0";
            string cardInactiveBg = _isDarkMode ? "#111827" : "#F6FAF7";
            string cardActiveBorder = _isDarkMode ? "#4E9F6E" : "#16A34A";
            string cardInactiveBorder = _isDarkMode ? "#1E293B" : "#C5D8CA";
            string cardHoverBg = _isDarkMode ? "#1E293B" : "#EAF4ED";
            string cardHoverBorder = _isDarkMode ? "#4E9F6E" : "#16A34A";
            string badgeActiveBg = _isDarkMode ? "#14382B" : "#D8EFE0";
            string badgeInactiveBg = _isDarkMode ? "#070A10" : "#E4EFE7";
            string badgeActiveFg = _isDarkMode ? "#4E9F6E" : "#0E6E3B";
            string badgeInactiveFg = _isDarkMode ? "#94A3B8" : "#3F5E4D";
            string paperBorderColor = _isDarkMode ? "#334155" : "#C5D8CA";

            for (int i = 1; i <= total; i++)
            {
                int pageNum = i;
                bool isActive = (pageNum == _activePageNum);

                var card = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isActive ? cardActiveBg : cardInactiveBg)),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isActive ? cardActiveBorder : cardInactiveBorder)),
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
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(paperBorderColor)),
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
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isActive ? badgeActiveBg : badgeInactiveBg)),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10, 3, 10, 3),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                badge.Child = new TextBlock
                {
                    Text = $"Página {pageNum}",
                    FontSize = 10.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isActive ? badgeActiveFg : badgeInactiveFg))
                };
                sp.Children.Add(badge);

                card.Child = sp;

                card.MouseEnter += (s, e) =>
                {
                    if (pageNum != _activePageNum)
                    {
                        card.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(cardHoverBg));
                        card.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(cardHoverBorder));
                    }
                };

                card.MouseLeave += (s, e) =>
                {
                    if (pageNum != _activePageNum)
                    {
                        card.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(cardInactiveBg));
                        card.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(cardInactiveBorder));
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

        private async void StartThumbnailGeneration(PdfDocumentTab tab)
        {
            _thumbnailCts?.Cancel();
            _thumbnailCts = new CancellationTokenSource();
            var token = _thumbnailCts.Token;

            try
            {
                if (tab.PdfDoc == null && File.Exists(tab.FilePath))
                {
                    try
                    {
                        tab.PdfDoc = !string.IsNullOrEmpty(tab.Password)
                            ? PdfiumViewer.PdfDocument.Load(tab.FilePath, tab.Password)
                            : PdfiumViewer.PdfDocument.Load(tab.FilePath);
                        if (tab.PdfDoc != null) tab.TotalPages = tab.PdfDoc.PageCount;
                    }
                    catch { }
                }

                if (tab.PdfDoc == null) return;
                int total = tab.PdfDoc.PageCount;

                for (int i = 0; i < total; i++)
                {
                    if (token.IsCancellationRequested || _activeTab != tab || tab.PdfDoc == null) break;

                    int pageNum = i + 1;
                    if (!tab.Thumbnails.ContainsKey(pageNum))
                    {
                        var bs = PdfThumbnailService.RenderPageThumbnail(tab.PdfDoc, i, dpi: 96);
                        if (bs != null)
                        {
                            tab.Thumbnails[pageNum] = bs;

                            if (_activeTab == tab && _pageImageMap.TryGetValue(pageNum, out var imgCtrl))
                            {
                                imgCtrl.Source = bs;
                            }
                        }
                    }

                    // Permite que o loop de mensagens processe cliques, scroll e renderização suavemente
                    await Task.Yield();
                }
            }
            catch { }
        }

        private void UpdatePageCardsHighlight()
        {
            string cardActiveBg = _isDarkMode ? "#1B3B2D" : "#DBEDE0";
            string cardInactiveBg = _isDarkMode ? "#111827" : "#F6FAF7";
            string cardActiveBorder = _isDarkMode ? "#4E9F6E" : "#16A34A";
            string cardInactiveBorder = _isDarkMode ? "#1E293B" : "#C5D8CA";
            string badgeActiveBg = _isDarkMode ? "#14382B" : "#D8EFE0";
            string badgeInactiveBg = _isDarkMode ? "#070A10" : "#E4EFE7";
            string badgeActiveFg = _isDarkMode ? "#4E9F6E" : "#0E6E3B";
            string badgeInactiveFg = _isDarkMode ? "#94A3B8" : "#3F5E4D";
            string paperBorderColor = _isDarkMode ? "#334155" : "#C5D8CA";

            var brushCardActiveBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString(cardActiveBg));
            var brushCardInactiveBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString(cardInactiveBg));
            var brushCardActiveBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString(cardActiveBorder));
            var brushCardInactiveBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString(cardInactiveBorder));
            var brushBadgeActiveBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString(badgeActiveBg));
            var brushBadgeInactiveBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString(badgeInactiveBg));
            var brushBadgeActiveFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString(badgeActiveFg));
            var brushBadgeInactiveFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString(badgeInactiveFg));
            var brushPaperBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString(paperBorderColor));

            foreach (var kvp in _pageCardMap)
            {
                int pageNum = kvp.Key;
                var card = kvp.Value;
                bool isActive = (pageNum == _activePageNum);

                card.Background = isActive ? brushCardActiveBg : brushCardInactiveBg;
                card.BorderBrush = isActive ? brushCardActiveBorder : brushCardInactiveBorder;
                card.BorderThickness = new Thickness(isActive ? 2 : 1);

                if (card.Child is StackPanel sp)
                {
                    if (sp.Children.Count > 0 && sp.Children[0] is Border paperBorder)
                    {
                        paperBorder.BorderBrush = brushPaperBorder;
                    }

                    if (sp.Children.Count > 1 && sp.Children[1] is Border badge)
                    {
                        badge.Background = isActive ? brushBadgeActiveBg : brushBadgeInactiveBg;
                        if (badge.Child is TextBlock tb)
                        {
                            tb.Foreground = isActive ? brushBadgeActiveFg : brushBadgeInactiveFg;
                        }
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

        private void ShowAnalyzingFeedback(bool isLargeFile = false, double sizeMb = 0, int totalPages = 0)
        {
            BadgeAnalysisLoading.Visibility = Visibility.Visible;
            ProgressBarAnalysis.Visibility = Visibility.Visible;
            ProgressBarAnalysis.IsIndeterminate = true;

            string analyzingHeaderFg = _isDarkMode ? "#5AA87A" : "#15803D";
            string analyzingBadgeBg = _isDarkMode ? "#172554" : "#E0EEFD";
            string analyzingBadgeBorder = _isDarkMode ? "#4E9F6E" : "#60A5FA";
            string analyzingBadgeFg = _isDarkMode ? "#93C5FD" : "#1D4ED8";
            string analyzingNoticeBg = _isDarkMode ? "#060907" : "#F6FAF7";
            string analyzingNoticeBorder = _isDarkMode ? "#141C17" : "#C5D8CA";
            string analyzingNoticeFg = _isDarkMode ? "#72857A" : "#3F5E4D";

            var brushAnalyzingHeaderFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString(analyzingHeaderFg));
            var brushAnalyzingBadgeBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString(analyzingBadgeBg));
            var brushAnalyzingBadgeBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString(analyzingBadgeBorder));
            var brushAnalyzingBadgeFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString(analyzingBadgeFg));

            if (isLargeFile)
            {
                TxtHeaderDocType.Text = $"Analisando Arquivo Grande ({sizeMb:0.1} MB)...";
                TxtHeaderDocType.Foreground = brushAnalyzingHeaderFg;

                BadgeIntegrity.Background = brushAnalyzingBadgeBg;
                BadgeIntegrity.BorderBrush = brushAnalyzingBadgeBorder;
                TxtIntegrityScore.Text = "Otimizando...";
                TxtIntegrityScore.Foreground = brushAnalyzingBadgeFg;
                TxtIntegrityStatusText.Text = totalPages > 0 ? $"{totalPages} Páginas" : "Arquivo Extenso";

                BorderImportVerdict.Background = brushAnalyzingBadgeBg;
                BorderImportVerdict.BorderBrush = brushAnalyzingBadgeBorder;
                TxtImportVerdictIcon.Text = "⚡";
                TxtImportVerdict.Text = "ARQUIVO GRANDE — ANÁLISE EM SEGUNDO PLANO...";
                TxtImportVerdict.Foreground = brushAnalyzingBadgeFg;

                TxtDiagWarning.Text = $"Arquivo grande detectado ({sizeMb:0.1} MB{(totalPages > 0 ? $" / {totalPages} páginas" : "")}). O documento já está pronto para leitura enquanto a integridade estrutural e OCR são validados.";
                TxtDiagWarning.Foreground = brushAnalyzingBadgeFg;
            }
            else
            {
                TxtHeaderDocType.Text = "Analisando Documento...";
                TxtHeaderDocType.Foreground = brushAnalyzingHeaderFg;

                BadgeIntegrity.Background = brushAnalyzingBadgeBg;
                BadgeIntegrity.BorderBrush = brushAnalyzingBadgeBorder;
                TxtIntegrityScore.Text = "Em Análise...";
                TxtIntegrityScore.Foreground = brushAnalyzingBadgeFg;
                TxtIntegrityStatusText.Text = totalPages > 0 ? $"{totalPages} Páginas" : "Inspecionando Camadas";

                BorderImportVerdict.Background = brushAnalyzingBadgeBg;
                BorderImportVerdict.BorderBrush = brushAnalyzingBadgeBorder;
                TxtImportVerdictIcon.Text = "⏳";
                TxtImportVerdict.Text = "ANALISANDO ESTRUTURA DO PDF...";
                TxtImportVerdict.Foreground = brushAnalyzingBadgeFg;

                TxtDiagWarning.Text = "Aguarde enquanto o CCPDF valida a integridade do texto e analisa elementos visuais...";
                TxtDiagWarning.Foreground = brushAnalyzingBadgeFg;
            }

            BorderRetaguardaNotice.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(analyzingNoticeBg));
            BorderRetaguardaNotice.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(analyzingNoticeBorder));
            TxtRetaguardaNoticeIcon.Text = "🔍";
            TxtRetaguardaNotice.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(analyzingNoticeFg));
            TxtRetaguardaNotice.Text = "Inspecionando texto vetorial, fontes, tabelas e inteligência visual de imagens...";

            TxtDiagStrangeChars.Text = "Sinais Estranhos: ...";
            TxtDiagFormatting.Text = "Formatação: Verificando...";
            TxtDiagImages.Text = "Imagens: Analisando...";
            TxtDiagScannedPages.Text = "Páginas Escaneadas: ...";
            TxtDiagCharCount.Text = "Total de Caracteres: ...";

            TxtEditor.Text = "[Extraindo texto vetorial e analisando conteúdo do documento em segundo plano...]";
            TxtEditorStats.Text = "Analisando...";
            TxtEditorMode.Text = "Modo: Verificando";
        }

        private void UpdateAnalysisProgress(ExtractionProgressInfo progress)
        {
            if (progress.TotalPages > 0 && progress.CurrentPage > 0)
            {
                ProgressBarAnalysis.IsIndeterminate = false;
                ProgressBarAnalysis.Maximum = progress.TotalPages;
                ProgressBarAnalysis.Value = progress.CurrentPage;

                int pct = (int)Math.Round((progress.CurrentPage * 100.0) / progress.TotalPages);
                TxtIntegrityScore.Text = $"{pct}% Analisado";
                TxtIntegrityStatusText.Text = $"Pág. {progress.CurrentPage} de {progress.TotalPages}";

                TxtHeaderDocType.Text = progress.IsLargeFile 
                    ? $"Analisando Arquivo Grande ({progress.CurrentPage}/{progress.TotalPages})..."
                    : $"Analisando ({progress.CurrentPage}/{progress.TotalPages})...";
            }

            if (!string.IsNullOrWhiteSpace(progress.StatusMessage))
            {
                TxtDiagWarning.Text = progress.StatusMessage;
            }
        }

        private void HideAnalyzingFeedback()
        {
            BadgeAnalysisLoading.Visibility = Visibility.Collapsed;
            ProgressBarAnalysis.Visibility = Visibility.Collapsed;
        }

        private void UpdateNotepadView()
        {
            if (_activeTab == null) return;

            if (_activeTab.Extraction == null)
            {
                var fi = File.Exists(_activeTab.FilePath) ? new FileInfo(_activeTab.FilePath) : null;
                double sizeMb = (fi?.Length ?? 0) / (1024.0 * 1024.0);
                ShowAnalyzingFeedback(sizeMb >= 5.0, sizeMb, _activeTab.TotalPages);
                return;
            }

            HideAnalyzingFeedback();

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
                var greenColor = (Color)ColorConverter.ConvertFromString(_isDarkMode ? "#4E9F6E" : "#15803D");
                TxtHeaderDocType.Foreground = new SolidColorBrush(greenColor);
                BadgeIntegrity.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#0C1811" : "#E2ECE4"));
                BadgeIntegrity.BorderBrush = new SolidColorBrush(greenColor);
                TxtIntegrityScore.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#4E9F6E" : "#0A6844"));
            }
            else if (report.IntegrityScore >= 70.0)
            {
                var yellowColor = (Color)ColorConverter.ConvertFromString(_isDarkMode ? "#F59E0B" : "#B45309");
                TxtHeaderDocType.Foreground = new SolidColorBrush(yellowColor);
                BadgeIntegrity.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#3D3215" : "#FEF3C7"));
                BadgeIntegrity.BorderBrush = new SolidColorBrush(yellowColor);
                TxtIntegrityScore.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#FCD34D" : "#92400E"));
            }
            else if (report.IntegrityScore >= 35.0)
            {
                var orangeColor = (Color)ColorConverter.ConvertFromString(_isDarkMode ? "#F97316" : "#C2410C");
                TxtHeaderDocType.Foreground = new SolidColorBrush(orangeColor);
                BadgeIntegrity.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#3E2619" : "#FFEDD5"));
                BadgeIntegrity.BorderBrush = new SolidColorBrush(orangeColor);
                TxtIntegrityScore.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#FDBA74" : "#9A3412"));
            }
            else
            {
                var redColor = (Color)ColorConverter.ConvertFromString(_isDarkMode ? "#EF4444" : "#DC2626");
                TxtHeaderDocType.Foreground = new SolidColorBrush(redColor);
                BadgeIntegrity.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#3E1C1E" : "#FEE2E2"));
                BadgeIntegrity.BorderBrush = new SolidColorBrush(redColor);
                TxtIntegrityScore.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#FCA5A5" : "#991B1B"));
            }

            TxtImportVerdict.Text = report.ImportVerdict;

            bool isUnimportableOrDataLoss = report.ImportVerdict.StartsWith("DOCUMENTO NÃO IMPORTÁVEL", StringComparison.OrdinalIgnoreCase) ||
                                           report.HasAnomalousImages ||
                                           report.HasVectorCurvedLabels ||
                                           report.OcrDiscoveredValues.Count > 0 ||
                                           (report.ScannedPagesCount > 0 && report.TotalCharacters < 30) ||
                                           report.IntegrityScore < 60;

            if (report.IsImageWithUnderlyingTextOrOcr)
            {
                TxtImportVerdictIcon.Text = "⚠️";
                BorderImportVerdict.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#3D3215" : "#FEF3C7"));
                BorderImportVerdict.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#F59E0B" : "#F59E0B"));
                TxtImportVerdict.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#FCD34D" : "#92400E"));

                BorderRetaguardaNotice.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#292210" : "#FFFBEB"));
                BorderRetaguardaNotice.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#B45309" : "#FCD34D"));
                TxtRetaguardaNoticeIcon.Text = "⚠️";
                TxtRetaguardaNotice.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#FDE68A" : "#78350F"));
                TxtRetaguardaNotice.Text = "Atenção: pode ser um arquivo convertido. Caso não importe ou tenha erros nos valores ou outros campos, envie para a retaguarda.";
            }
            else if (report.HasVectorCurvedLabels)
            {
                TxtImportVerdictIcon.Text = "⛔";
                BorderImportVerdict.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#3E1C1E" : "#FEE2E2"));
                BorderImportVerdict.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#EF4444" : "#EF4444"));
                TxtImportVerdict.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#FCA5A5" : "#991B1B"));

                BorderRetaguardaNotice.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#2B1517" : "#FEF2F2"));
                BorderRetaguardaNotice.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#991B1B" : "#FCA5A5"));
                TxtRetaguardaNoticeIcon.Text = "⛔";
                TxtRetaguardaNotice.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#FECACA" : "#7F1D1D"));
                if (report.VectorCurvedValueSamples.Count > 0)
                {
                    TxtRetaguardaNotice.Text = "Atenção: valores e lançamentos contábeis foram impressos em curvas/desenho e não existem no texto. Documento inviável para importação.";
                }
                else
                {
                    TxtRetaguardaNotice.Text = "Atenção: o texto teve perda de informações importantes e tem altas chances de ser inviável tecnicamente. Informe o cliente sobre a possibilidade e envie para a retaguarda.";
                }
            }
            else if (isUnimportableOrDataLoss)
            {
                TxtImportVerdictIcon.Text = "⛔";
                BorderImportVerdict.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#3E1C1E" : "#FEE2E2"));
                BorderImportVerdict.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#EF4444" : "#EF4444"));
                TxtImportVerdict.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#FCA5A5" : "#991B1B"));

                BorderRetaguardaNotice.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#2B1517" : "#FEF2F2"));
                BorderRetaguardaNotice.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#991B1B" : "#FCA5A5"));
                TxtRetaguardaNoticeIcon.Text = "⛔";
                TxtRetaguardaNotice.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#FECACA" : "#7F1D1D"));
                TxtRetaguardaNotice.Text = "Atenção: documento não importável. Informe o cliente.";
            }
            else if (report.ImportVerdict.Contains("FALHAS", StringComparison.OrdinalIgnoreCase) ||
                     report.ImportVerdict.StartsWith("Atenção", StringComparison.OrdinalIgnoreCase))
            {
                TxtImportVerdictIcon.Text = "⚠️";
                BorderImportVerdict.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#3D3215" : "#FEF3C7"));
                BorderImportVerdict.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#F59E0B" : "#F59E0B"));
                TxtImportVerdict.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#FCD34D" : "#92400E"));

                BorderRetaguardaNotice.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#292210" : "#FFFBEB"));
                BorderRetaguardaNotice.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#B45309" : "#FCD34D"));
                TxtRetaguardaNoticeIcon.Text = "ℹ️";
                TxtRetaguardaNotice.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#FDE68A" : "#78350F"));
                TxtRetaguardaNotice.Text = "Atenção: caso mesmo como importável ele não importe, envie para a retaguarda.";
            }
            else
            {
                TxtImportVerdictIcon.Text = "✅";
                BorderImportVerdict.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#0C1811" : "#E2ECE4"));
                BorderImportVerdict.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#4E9F6E" : "#15803D"));
                TxtImportVerdict.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#4E9F6E" : "#0A6844"));

                BorderRetaguardaNotice.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#060907" : "#F6FAF7"));
                BorderRetaguardaNotice.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#141C17" : "#C5D8CA"));
                TxtRetaguardaNoticeIcon.Text = "ℹ️";
                TxtRetaguardaNotice.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#8A9A91" : "#3F5E4D"));
                TxtRetaguardaNotice.Text = "Atenção: caso mesmo como importável ele não importe, envie para a retaguarda.";
            }

            TxtDiagFormatting.Text = $"Formatação: {report.FormattingQuality}";
            TxtDiagStrangeChars.Text = $"Sinais Estranhos: {report.StrangeCharactersCount}";

            // Imagens no Documento
            if (report.HasAnomalousImages && report.AnomalousInlineImagesCount > 0)
            {
                TxtDiagImages.Text = $"Imagens: {report.TotalImagesFound:N0} (⚠️ {report.AnomalousInlineImagesCount} no texto)";
                TxtDiagImages.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#FB923C" : "#C2410C"));
            }
            else if (report.IsImageWithUnderlyingTextOrOcr)
            {
                TxtDiagImages.Text = $"Imagens: {report.TotalImagesFound:N0} (⚠️ Camada OCR)";
                TxtDiagImages.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#FBBF24" : "#B45309"));
            }
            else
            {
                TxtDiagImages.Text = $"Imagens: {report.TotalImagesFound:N0}";
                TxtDiagImages.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#E2E8F0" : "#132B1E"));
            }

            // Páginas Escaneadas / Convertidas
            if (report.PagesWithUnderlyingTextCount > 0 || report.OcrRecognizedPagesCount > 0)
            {
                int convertedCount = report.PagesWithUnderlyingTextCount + report.OcrRecognizedPagesCount;
                TxtDiagScannedPages.Text = $"Pág. Convertidas (OCR): {convertedCount} de {report.TotalPages}";
                TxtDiagScannedPages.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#FBBF24" : "#B45309"));
            }
            else if (report.ScannedPagesCount > 0)
            {
                TxtDiagScannedPages.Text = $"Pág. Escaneadas: {report.ScannedPagesCount} de {report.TotalPages}";
                TxtDiagScannedPages.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#F87171" : "#DC2626"));
            }
            else
            {
                TxtDiagScannedPages.Text = $"Pág. Escaneadas: 0 de {report.TotalPages}";
                TxtDiagScannedPages.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#94A3B8" : "#3F5E4D"));
            }

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
                TxtDiagWarning.Text = "• " + string.Join("\n\n• ", report.DiagnosticWarnings);
                if (report.HasAnomalousImages || report.IsImageWithUnderlyingTextOrOcr)
                {
                    TxtDiagWarning.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#FDBA74" : "#C2410C"));
                }
                else if (report.IntegrityScore < 70)
                {
                    TxtDiagWarning.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#FCD34D" : "#B45309"));
                }
                else
                {
                    TxtDiagWarning.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#5AA87A" : "#0A6844"));
                }
            }
            else
            {
                TxtDiagWarning.Text = "Texto bem estruturado e sem anomalias detectadas.";
                TxtDiagWarning.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#5AA87A" : "#0A6844"));
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
            UpdateSidebarToggleButtons();
        }

        private void CloseNotepad()
        {
            _isNotepadOpen = false;
            ColNotepad.Width = new GridLength(0);
            PanelNotepad.Visibility = Visibility.Collapsed;
            SplitterBar.Visibility = Visibility.Collapsed;
            UpdateSidebarToggleButtons();
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

        #region Notepad Search Logic
        private readonly List<int> _notepadMatchIndices = new();
        private int _notepadCurrentMatchIndex = -1;

        private void TxtNotepadSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            string query = TxtNotepadSearch.Text;
            bool hasText = !string.IsNullOrEmpty(query);
            TxtNotepadSearchPlaceholder.Visibility = hasText ? Visibility.Collapsed : Visibility.Visible;
            BtnClearNotepadSearch.Visibility = hasText ? Visibility.Visible : Visibility.Collapsed;

            ExecuteNotepadSearch(query, moveNext: false);
        }

        private void TxtNotepadSearch_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                e.Handled = true;
                if (System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift))
                    NotepadSearchPrev();
                else
                    NotepadSearchNext();
            }
            else if (e.Key == System.Windows.Input.Key.Escape)
            {
                e.Handled = true;
                BtnClearNotepadSearch_Click(this, new RoutedEventArgs());
            }
        }

        private void BtnClearNotepadSearch_Click(object sender, RoutedEventArgs e)
        {
            TxtNotepadSearch.Text = string.Empty;
            _notepadMatchIndices.Clear();
            _notepadCurrentMatchIndex = -1;
            BadgeNotepadSearchCount.Visibility = Visibility.Collapsed;
            BtnNotepadSearchPrev.IsEnabled = false;
            BtnNotepadSearchNext.IsEnabled = false;
            TxtEditor.Focus();
        }

        private void BtnNotepadSearchPrev_Click(object sender, RoutedEventArgs e) => NotepadSearchPrev();
        private void BtnNotepadSearchNext_Click(object sender, RoutedEventArgs e) => NotepadSearchNext();

        private void ExecuteNotepadSearch(string query, bool moveNext)
        {
            _notepadMatchIndices.Clear();
            _notepadCurrentMatchIndex = -1;

            if (string.IsNullOrWhiteSpace(query) || string.IsNullOrEmpty(TxtEditor.Text))
            {
                BadgeNotepadSearchCount.Visibility = Visibility.Collapsed;
                BtnNotepadSearchPrev.IsEnabled = false;
                BtnNotepadSearchNext.IsEnabled = false;
                return;
            }

            string text = TxtEditor.Text;
            int start = 0;
            while ((start = text.IndexOf(query, start, StringComparison.OrdinalIgnoreCase)) != -1)
            {
                _notepadMatchIndices.Add(start);
                start += query.Length;
            }

            int count = _notepadMatchIndices.Count;
            if (count > 0)
            {
                _notepadCurrentMatchIndex = 0;
                BadgeNotepadSearchCount.Visibility = Visibility.Visible;
                TxtNotepadSearchCount.Text = $"1 de {count}";
                TxtNotepadSearchCount.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#4E9F6E" : "#0A6844"));
                BtnNotepadSearchPrev.IsEnabled = true;
                BtnNotepadSearchNext.IsEnabled = true;
                HighlightNotepadMatch();
            }
            else
            {
                BadgeNotepadSearchCount.Visibility = Visibility.Visible;
                TxtNotepadSearchCount.Text = "0 de 0";
                TxtNotepadSearchCount.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#F87171" : "#DC2626"));
                BtnNotepadSearchPrev.IsEnabled = false;
                BtnNotepadSearchNext.IsEnabled = false;
            }
        }

        private void NotepadSearchNext()
        {
            if (_notepadMatchIndices.Count == 0) return;
            _notepadCurrentMatchIndex = (_notepadCurrentMatchIndex + 1) % _notepadMatchIndices.Count;
            TxtNotepadSearchCount.Text = $"{_notepadCurrentMatchIndex + 1} de {_notepadMatchIndices.Count}";
            TxtNotepadSearchCount.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#4E9F6E" : "#0A6844"));
            HighlightNotepadMatch();
        }

        private void NotepadSearchPrev()
        {
            if (_notepadMatchIndices.Count == 0) return;
            _notepadCurrentMatchIndex = (_notepadCurrentMatchIndex - 1 + _notepadMatchIndices.Count) % _notepadMatchIndices.Count;
            TxtNotepadSearchCount.Text = $"{_notepadCurrentMatchIndex + 1} de {_notepadMatchIndices.Count}";
            TxtNotepadSearchCount.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#4E9F6E" : "#0A6844"));
            HighlightNotepadMatch();
        }

        private void HighlightNotepadMatch()
        {
            if (_notepadCurrentMatchIndex < 0 || _notepadCurrentMatchIndex >= _notepadMatchIndices.Count) return;
            int charIndex = _notepadMatchIndices[_notepadCurrentMatchIndex];
            int length = TxtNotepadSearch.Text.Length;

            TxtEditor.Focus();
            TxtEditor.Select(charIndex, length);

            int lineIndex = TxtEditor.GetLineIndexFromCharacterIndex(charIndex);
            if (lineIndex >= 0)
            {
                TxtEditor.ScrollToLine(lineIndex);
            }
        }
        #endregion

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

        private void TxtEditor_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateEditorStats();
            if (!string.IsNullOrEmpty(TxtNotepadSearch?.Text))
            {
                ExecuteNotepadSearch(TxtNotepadSearch.Text, moveNext: false);
            }
        }

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

        
        #region Theme Management (Dark / Light Mode)

        private string GetThemeConfigPath()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CCPDF");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, "theme.config");
        }

        private void LoadThemePreference()
        {
            try
            {
                string file = GetThemeConfigPath();
                if (File.Exists(file))
                {
                    string txt = File.ReadAllText(file).Trim();
                    if (bool.TryParse(txt, out bool dark))
                    {
                        _isDarkMode = dark;
                    }
                }
            }
            catch { }
        }

        private void SaveThemePreference()
        {
            try
            {
                string file = GetThemeConfigPath();
                File.WriteAllText(file, _isDarkMode.ToString());
            }
            catch { }
        }

        private void BtnToggleTheme_Click(object sender, RoutedEventArgs e)
        {
            ApplyTheme(!_isDarkMode);
        }

        private void UpdateSidebarToggleButtons()
        {
            if (BtnTogglePages == null || BtnToggleNotepad == null) return;

            if (_isDarkMode)
            {
                // Dark Mode: Emerald pill when open, subtle slate when closed
                BtnTogglePages.Background = _isPageSidebarOpen 
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#132B24")) 
                    : Brushes.Transparent;
                BtnTogglePages.Foreground = _isPageSidebarOpen 
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#34D399")) 
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"));
                BtnTogglePages.BorderBrush = _isPageSidebarOpen 
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")) 
                    : Brushes.Transparent;
                BtnTogglePages.BorderThickness = new Thickness(_isPageSidebarOpen ? 1 : 0);

                BtnToggleNotepad.Background = _isNotepadOpen 
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#132B24")) 
                    : Brushes.Transparent;
                BtnToggleNotepad.Foreground = _isNotepadOpen 
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#34D399")) 
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FACC15"));
                BtnToggleNotepad.BorderBrush = _isNotepadOpen 
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")) 
                    : Brushes.Transparent;
                BtnToggleNotepad.BorderThickness = new Thickness(_isNotepadOpen ? 1 : 0);
            }
            else
            {
                // Light Mode: Soft pastel mint pill when open
                BtnTogglePages.Background = _isPageSidebarOpen 
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D8EFE0")) 
                    : Brushes.Transparent;
                BtnTogglePages.Foreground = _isPageSidebarOpen 
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0E6E3B")) 
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3F5E4D"));
                BtnTogglePages.BorderBrush = _isPageSidebarOpen 
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16A34A")) 
                    : Brushes.Transparent;
                BtnTogglePages.BorderThickness = new Thickness(_isPageSidebarOpen ? 1 : 0);

                BtnToggleNotepad.Background = _isNotepadOpen 
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D8EFE0")) 
                    : Brushes.Transparent;
                BtnToggleNotepad.Foreground = _isNotepadOpen 
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0E6E3B")) 
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B45309"));
                BtnToggleNotepad.BorderBrush = _isNotepadOpen 
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16A34A")) 
                    : Brushes.Transparent;
                BtnToggleNotepad.BorderThickness = new Thickness(_isNotepadOpen ? 1 : 0);
            }
        }

        public void ApplyTheme(bool isDark)
        {
            _isDarkMode = isDark;
            SaveThemePreference();

            // Colors definition based on research:
            // Dark Mode: Deep charcoal slate (#0B0F19), surface #070A10, card #111827, borders #1E293B
            // Pastel Green Light Mode: Soft sage / mint pastel (#F0F5F1), surface #E4EFE7, card #F6FAF7, input #FFFFFF, editor #F8FCF9, borders #C5D8CA, text #132B1E, secondary #3F5E4D, muted #628271
            string bgWindow = isDark ? "#0B0F19" : "#F0F5F1";
            string bgSurface = isDark ? "#070A10" : "#E4EFE7";
            string bgCard = isDark ? "#111827" : "#F6FAF7";
            string bgInput = isDark ? "#111827" : "#FFFFFF";
            string bgEditor = isDark ? "#080C14" : "#F8FCF9";
            string borderCol = isDark ? "#1E293B" : "#C5D8CA";
            string textPrimary = isDark ? "#F8FAFC" : "#132B1E";
            string textSecondary = isDark ? "#94A3B8" : "#3F5E4D";
            string textMuted = isDark ? "#64748B" : "#628271";
            string accentCol = isDark ? "#10B981" : "#16A34A";
            string accentText = isDark ? "#34D399" : "#0E6E3B";
            string accentPillBg = isDark ? "#132B24" : "#D8EFE0";
            string warningPillBg = isDark ? "#3F2E18" : "#FEF3C7";
            string warningPillBorder = isDark ? "#F59E0B" : "#D97706";
            string warningText = isDark ? "#FBBF24" : "#92400E";
            string hoverBg = isDark ? "#1E293B" : "#D8EADC";
            string titleBarHoverFg = isDark ? "#F8FAFC" : "#132B1E";
            string scrollThumbNormal = isDark ? "#334155" : "#BACFC0";
            string scrollThumbHover = isDark ? "#475569" : "#9FBAA7";
            string scrollThumbPressed = isDark ? "#64748B" : "#7F9E88";

            var brushBgWindow = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgWindow));
            var brushBgSurface = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgSurface));
            var brushBgCard = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgCard));
            var brushBgInput = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgInput));
            var brushBgEditor = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgEditor));
            var brushBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString(borderCol));
            var brushTextPrimary = new SolidColorBrush((Color)ColorConverter.ConvertFromString(textPrimary));
            var brushTextSecondary = new SolidColorBrush((Color)ColorConverter.ConvertFromString(textSecondary));
            var brushTextMuted = new SolidColorBrush((Color)ColorConverter.ConvertFromString(textMuted));
            var brushAccent = new SolidColorBrush((Color)ColorConverter.ConvertFromString(accentCol));
            var brushAccentText = new SolidColorBrush((Color)ColorConverter.ConvertFromString(accentText));
            var brushAccentPillBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString(accentPillBg));

            if (System.Windows.Application.Current != null)
            {
                var res = System.Windows.Application.Current.Resources;
                res["BackgroundBrush"] = brushBgWindow;
                res["SurfaceBrush"] = brushBgSurface;
                res["CardBrush"] = brushBgCard;
                res["BorderBrush"] = brushBorder;
                res["TextPrimary"] = brushTextPrimary;
                res["TextSecondary"] = brushTextSecondary;
                res["TextMuted"] = brushTextMuted;
                res["AccentPrimary"] = brushAccent;
                res["AccentText"] = brushAccentText;
                res["AccentPillBg"] = brushAccentPillBg;
                res["WarningPillBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(warningPillBg));
                res["WarningPillBorder"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(warningPillBorder));
                res["WarningText"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(warningText));
                res["HoverBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hoverBg));
                res["TitleBarHoverFg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(titleBarHoverFg));
                res["ToolTipBackground"] = brushBgCard;
                res["ToolTipForeground"] = brushTextPrimary;
                res["ToolTipBorder"] = brushBorder;
                res["ScrollBarThumbNormal"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(scrollThumbNormal));
                res["ScrollBarThumbHover"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(scrollThumbHover));
                res["ScrollBarThumbPressed"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(scrollThumbPressed));
            }

            // Window DWM Immersive Dark Mode
            try
            {
                var helper = new System.Windows.Interop.WindowInteropHelper(this);
                if (helper.Handle != IntPtr.Zero)
                {
                    int darkModeAttr = isDark ? 1 : 0;
                    DwmSetWindowAttribute(helper.Handle, 20, ref darkModeAttr, sizeof(int));
                }
            }
            catch { }

            // Window root
            if (WindowRootBorder != null)
            {
                WindowRootBorder.Background = brushBgWindow;
                WindowRootBorder.BorderBrush = brushBorder;
            }
            if (RootGrid != null) RootGrid.Background = brushBgWindow;

            // Title Bar
            if (TitleBarBorder != null)
            {
                TitleBarBorder.Background = brushBgSurface;
                TitleBarBorder.BorderBrush = brushBorder;
            }
            if (TxtBrandName != null) TxtBrandName.Foreground = brushTextPrimary;
            if (BtnNewTab != null) BtnNewTab.Foreground = brushTextSecondary;

            // Window Controls & Theme Toggle
            if (BtnToggleTheme != null)
            {
                BtnToggleTheme.ToolTip = isDark ? "Alternar para Modo Claro (Alt+T)" : "Alternar para Modo Escuro (Alt+T)";
            }
            if (PathThemeIcon != null)
            {
                if (isDark)
                {
                    // Sun Icon (Warm Gold/Amber)
                    PathThemeIcon.Data = Geometry.Parse("M12 7c-2.76 0-5 2.24-5 5s2.24 5 5 5 5-2.24 5-5-2.24-5-5-5zM2 13h2c.55 0 1-.45 1-1s-.45-1-1-1H2c-.55 0-1 .45-1 1s.45 1 1 1zm18 0h2c.55 0 1-.45 1-1s-.45-1-1-1h-2c-.55 0-1 .45-1 1s.45 1 1 1zM11 2v2c0 .55.45 1 1 1s1-.45 1-1V2c0-.55-.45-1-1-1s-1 .45-1 1zm0 18v2c0 .55.45 1 1 1s1-.45 1-1v-2c0-.55-.45-1-1-1s-1 .45-1 1zM5.99 4.58c-.39-.39-1.03-.39-1.41 0s-.39 1.03 0 1.41l1.06 1.06c.39.39 1.03.39 1.41 0s.39-1.03 0-1.41L5.99 4.58zm12.37 12.37c-.39-.39-1.03-.39-1.41 0s-.39 1.03 0 1.41l1.06 1.06c.39.39 1.03.39 1.41 0s.39-1.03 0-1.41l-1.06-1.06zm1.06-10.96c.39-.39.39-1.03 0-1.41s-1.03-.39-1.41 0l-1.06 1.06c-.39.39-.39 1.03 0 1.41s1.03.39 1.41 0l1.06-1.06zM7.05 18.36c.39-.39.39-1.03 0-1.41s-1.03-.39-1.41 0l-1.06 1.06c-.39.39-.39 1.03 0 1.41s1.03.39 1.41 0l1.06-1.06z");
                    PathThemeIcon.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FBBF24"));
                }
                else
                {
                    // Moon Crescent Icon (Deep Forest Slate)
                    PathThemeIcon.Data = Geometry.Parse("M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z");
                    PathThemeIcon.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#234232"));
                }
            }
            if (BtnMinimize != null) BtnMinimize.Foreground = brushTextSecondary;
            if (BtnMaximize != null) BtnMaximize.Foreground = brushTextSecondary;
            if (BtnCloseWindow != null) BtnCloseWindow.Foreground = brushTextSecondary;

            // Left Sidebar
            if (BorderLeftSidebar != null)
            {
                BorderLeftSidebar.Background = brushBgSurface;
                BorderLeftSidebar.BorderBrush = brushBorder;
            }
            if (SidebarSeparator != null) SidebarSeparator.Fill = brushBorder;
            if (BtnOpenFile != null)
            {
                BtnOpenFile.Background = brushBgCard;
                BtnOpenFile.Foreground = brushAccent;
                BtnOpenFile.BorderBrush = brushBorder;
                BtnOpenFile.BorderThickness = new Thickness(1);
            }
            if (BtnQuickSave != null)
            {
                BtnQuickSave.Background = brushBgCard;
                BtnQuickSave.Foreground = brushAccent;
                BtnQuickSave.BorderBrush = brushBorder;
                BtnQuickSave.BorderThickness = new Thickness(1);
            }
            if (BtnSettings != null)
            {
                BtnSettings.Background = brushBgCard;
                BtnSettings.Foreground = brushTextSecondary;
                BtnSettings.BorderBrush = brushBorder;
                BtnSettings.BorderThickness = new Thickness(1);
            }

            // Page Sidebar
            if (PanelPageSidebar != null)
            {
                PanelPageSidebar.Background = brushBgSurface;
                PanelPageSidebar.BorderBrush = brushBorder;
            }
            if (TxtPageSidebarTitle != null) TxtPageSidebarTitle.Foreground = brushTextSecondary;
            if (BadgePageCount != null)
            {
                BadgePageCount.Background = brushAccentPillBg;
                TxtPageCountBadge.Foreground = brushAccentText;
            }
            if (BtnClosePageSidebar != null) BtnClosePageSidebar.Foreground = brushTextSecondary;

            // Center Viewer Area
            if (GridCenterViewer != null) GridCenterViewer.Background = brushBgWindow;
            if (BorderEmptyIcon != null)
            {
                BorderEmptyIcon.Background = brushBgCard;
                BorderEmptyIcon.BorderBrush = brushBorder;
            }
            if (TxtEmptyTitle != null) TxtEmptyTitle.Foreground = brushTextPrimary;
            if (TxtEmptyDesc != null) TxtEmptyDesc.Foreground = brushTextSecondary;
            if (BtnEmptyOpen != null)
            {
                BtnEmptyOpen.Background = brushAccent;
                BtnEmptyOpen.Foreground = Brushes.White;
            }

            // Zoom & Quick Bar
            if (BorderZoomBar != null)
            {
                BorderZoomBar.Background = brushBgSurface;
                BorderZoomBar.BorderBrush = brushBorder;
            }
            if (TxtViewerPageInfo != null) TxtViewerPageInfo.Foreground = brushTextSecondary;
            if (BadgeAnalysisLoading != null)
            {
                BadgeAnalysisLoading.Background = brushBgCard;
                BadgeAnalysisLoading.BorderBrush = brushAccent;
                TxtAnalysisLoading.Foreground = brushAccentText;
            }

            // PDF Search Bar
            if (BorderViewerSearch != null)
            {
                BorderViewerSearch.Background = brushBgInput;
                BorderViewerSearch.BorderBrush = brushBorder;
            }
            if (TxtViewerSearch != null)
            {
                TxtViewerSearch.Foreground = brushTextPrimary;
                TxtViewerSearch.CaretBrush = brushAccent;
            }
            if (TxtSearchPlaceholder != null) TxtSearchPlaceholder.Foreground = brushTextMuted;
            if (BtnClearSearch != null) BtnClearSearch.Foreground = brushTextSecondary;
            if (BadgeSearchCount != null)
            {
                BadgeSearchCount.Background = brushBorder;
                TxtSearchCount.Foreground = brushAccentText;
            }
            if (SearchSeparator != null) SearchSeparator.Background = brushBorder;
            if (BtnSearchPrev != null) BtnSearchPrev.Foreground = brushTextSecondary;
            if (BtnSearchNext != null) BtnSearchNext.Foreground = brushTextSecondary;

            // Zoom controls
            if (BtnZoomOut != null)
            {
                BtnZoomOut.Background = brushBgCard;
                BtnZoomOut.Foreground = brushTextPrimary;
                BtnZoomOut.BorderBrush = brushBorder;
                BtnZoomOut.BorderThickness = new Thickness(1);
            }
            if (TxtZoomLevel != null) TxtZoomLevel.Foreground = brushTextSecondary;
            if (BtnZoomIn != null)
            {
                BtnZoomIn.Background = brushBgCard;
                BtnZoomIn.Foreground = brushTextPrimary;
                BtnZoomIn.BorderBrush = brushBorder;
                BtnZoomIn.BorderThickness = new Thickness(1);
            }
            if (BtnZoomFit != null)
            {
                BtnZoomFit.Background = brushBgCard;
                BtnZoomFit.Foreground = brushAccentText;
                BtnZoomFit.BorderBrush = brushBorder;
                BtnZoomFit.BorderThickness = new Thickness(1);
            }

            // Split View Elements
            if (SplitViewSplitter != null) SplitViewSplitter.Background = brushBorder;
            if (PanelSplitViewer != null) PanelSplitViewer.Background = brushBgSurface;
            if (SplitHeaderBar != null)
            {
                SplitHeaderBar.Background = brushBgSurface;
                SplitHeaderBar.BorderBrush = brushBorder;
            }
            if (TxtSplitFileName != null) TxtSplitFileName.Foreground = brushTextPrimary;
            if (BadgeSplitTab != null)
            {
                BadgeSplitTab.Background = brushAccentPillBg;
                BadgeSplitTab.BorderBrush = brushAccent;
            }

            // Notepad Panel
            if (SplitterBar != null) SplitterBar.Background = brushBorder;
            if (PanelNotepad != null)
            {
                PanelNotepad.Background = brushBgSurface;
                PanelNotepad.BorderBrush = brushBorder;
            }
            if (BorderNotepadHeader != null)
            {
                BorderNotepadHeader.Background = brushBgSurface;
                BorderNotepadHeader.BorderBrush = brushBorder;
            }
            if (BtnCloseNotepad != null) BtnCloseNotepad.Foreground = brushTextSecondary;

            // Diagnostics Card
            if (BorderDiagnostics != null)
            {
                BorderDiagnostics.Background = brushBgCard;
                BorderDiagnostics.BorderBrush = brushBorder;
            }
            if (TxtDiagStrangeChars != null) TxtDiagStrangeChars.Foreground = brushTextSecondary;
            if (TxtDiagCharCount != null) TxtDiagCharCount.Foreground = brushTextSecondary;
            if (TxtDiagImages != null) TxtDiagImages.Foreground = brushTextSecondary;
            if (TxtDiagScannedPages != null) TxtDiagScannedPages.Foreground = brushTextSecondary;
            if (TxtDiagFormatting != null) TxtDiagFormatting.Foreground = brushTextSecondary;
            if (TxtDiagWarning != null) TxtDiagWarning.Foreground = brushTextSecondary;

            if (ExpanderProps != null) ExpanderProps.Foreground = brushTextSecondary;
            if (BorderPropsContent != null)
            {
                BorderPropsContent.Background = brushBgEditor;
                BorderPropsContent.BorderBrush = brushBorder;
            }

            // Notepad Action Buttons
            if (BtnToggleRawText != null)
            {
                BtnToggleRawText.Background = brushBgCard;
                BtnToggleRawText.BorderBrush = brushBorder;
                BtnToggleRawText.Foreground = brushTextPrimary;
            }
            if (BtnCopyAllText != null)
            {
                BtnCopyAllText.Background = brushBgCard;
                BtnCopyAllText.BorderBrush = brushBorder;
                BtnCopyAllText.Foreground = brushTextPrimary;
            }
            if (BtnSaveTxt != null)
            {
                BtnSaveTxt.Background = brushBgCard;
                BtnSaveTxt.BorderBrush = brushBorder;
                BtnSaveTxt.Foreground = brushTextPrimary;
            }
            if (BtnToggleWrap != null)
            {
                BtnToggleWrap.Background = brushBgCard;
                BtnToggleWrap.BorderBrush = brushBorder;
                BtnToggleWrap.Foreground = brushTextPrimary;
            }

            // Notepad Search Box
            if (BorderNotepadSearch != null)
            {
                BorderNotepadSearch.Background = brushBgInput;
                BorderNotepadSearch.BorderBrush = brushBorder;
            }
            if (TxtNotepadSearch != null)
            {
                TxtNotepadSearch.Foreground = brushTextPrimary;
                TxtNotepadSearch.CaretBrush = brushAccent;
            }
            if (TxtNotepadSearchPlaceholder != null) TxtNotepadSearchPlaceholder.Foreground = brushTextMuted;
            if (BtnClearNotepadSearch != null) BtnClearNotepadSearch.Foreground = brushTextSecondary;
            if (BadgeNotepadSearchCount != null)
            {
                BadgeNotepadSearchCount.Background = brushBorder;
                TxtNotepadSearchCount.Foreground = brushAccentText;
            }
            if (BtnNotepadSearchPrev != null) BtnNotepadSearchPrev.Foreground = brushTextSecondary;
            if (BtnNotepadSearchNext != null) BtnNotepadSearchNext.Foreground = brushTextSecondary;

            // Notepad Editor
            if (BorderTxtEditor != null)
            {
                BorderTxtEditor.Background = brushBgEditor;
                BorderTxtEditor.BorderBrush = brushBorder;
            }
            if (TxtEditor != null)
            {
                TxtEditor.Background = brushBgEditor;
                TxtEditor.Foreground = brushTextPrimary;
                TxtEditor.CaretBrush = brushAccent;
            }
            if (TxtEditorStats != null) TxtEditorStats.Foreground = brushTextMuted;
            if (TxtEditorMode != null) TxtEditorMode.Foreground = brushTextMuted;

            // Banner Default App
            if (BannerDefaultApp != null)
            {
                BannerDefaultApp.Background = brushBgCard;
                BannerDefaultApp.BorderBrush = brushAccent;
            }
            if (TxtBannerDefault != null) TxtBannerDefault.Foreground = brushTextPrimary;
            if (BtnDismissDefaultBanner != null) BtnDismissDefaultBanner.Foreground = brushTextSecondary;
            if (BtnSetDefaultBanner != null)
            {
                BtnSetDefaultBanner.Background = brushAccent;
                BtnSetDefaultBanner.Foreground = Brushes.White;
            }

            if (PanelOpeningFeedback != null)
            {
                PanelOpeningFeedback.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isDark ? "#E6070A10" : "#E6F0F5F1"));
            }

            // Native PDF Viewers BackColor & Dynamic Win32 Scrollbar Theme
            var rendererBack = isDark 
                ? System.Drawing.Color.FromArgb(11, 15, 25) 
                : System.Drawing.Color.FromArgb(238, 246, 240);

            if (_pdfRenderer != null)
            {
                _pdfRenderer.BackColor = rendererBack;
                if (_pdfRenderer.IsHandleCreated)
                {
                    try { SetWindowTheme(_pdfRenderer.Handle, isDark ? "DarkMode_Explorer" : "Explorer", null); } catch { }
                }
            }
            if (_splitPdfRenderer != null)
            {
                _splitPdfRenderer.BackColor = rendererBack;
                if (_splitPdfRenderer.IsHandleCreated)
                {
                    try { SetWindowTheme(_splitPdfRenderer.Handle, isDark ? "DarkMode_Explorer" : "Explorer", null); } catch { }
                }
            }

            // Context Menus
            ApplyContextMenuTheme(_viewerContextMenu, _viewerMenuItems);
            ApplyContextMenuTheme(_splitContextMenu, _splitMenuItems);

            // Update Dynamic Subviews
            UpdateSidebarToggleButtons();
            UpdateTabsBar();
            UpdatePageCardsHighlight();
            UpdateNotepadView();
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
