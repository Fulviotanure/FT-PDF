using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using FtPdf.Dialogs;
using FtPdf.Models;
using FtPdf.Services;
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
using ComboBoxItem = System.Windows.Controls.ComboBoxItem;
using SelectionChangedEventArgs = System.Windows.Controls.SelectionChangedEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;
using Vector = System.Windows.Vector;
using DragEventArgs = System.Windows.DragEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using DragDropEffects = System.Windows.DragDropEffects;
using DataObject = System.Windows.DataObject;
using DataFormats = System.Windows.DataFormats;
using Grid = System.Windows.Controls.Grid;
using ColumnDefinition = System.Windows.Controls.ColumnDefinition;
using System.Threading;
using Image = System.Windows.Controls.Image;
using Stretch = System.Windows.Media.Stretch;
using BitmapScalingMode = System.Windows.Media.BitmapScalingMode;

namespace FtPdf
{
    public class TabDragData
    {
        public PdfDocumentTab Tab { get; set; } = null!;
        public string FilePath { get; set; } = string.Empty;
        public MainWindow? SourceWindow { get; set; }
    }

    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<PdfDocumentTab> _tabs = new();
        private PdfDocumentTab? _activeTab;
        private readonly PdfExtractionService _extractionService = new();
        private readonly PdfEditingService _editingService = new();
        private bool _isRawTextMode = false;
        private bool _isNotepadOpen = false;
        private bool _isPageSidebarOpen = false;
        private PdfDocumentTab? _splitTab = null;
        private readonly Dictionary<int, Image> _pageImageMap = new();
        private readonly Dictionary<int, Border> _pageCardMap = new();
        private int _activePageNum = 1;
        private CancellationTokenSource? _thumbnailCts;

        // Native Pdfium viewer state
        private const double BASE_DPI = 120.0;
        private const double ZOOM_STEP = 0.15;
        private const double ZOOM_MIN = 0.3;
        private const double ZOOM_MAX = 4.0;
        private CancellationTokenSource? _renderCts;
        private CancellationTokenSource? _splitRenderCts;

        public MainWindow()
        {
            InitializeComponent();
            StateChanged += MainWindow_StateChanged;
            PreviewKeyDown += MainWindow_PreviewKeyDown;
            CheckCommandLineArgs();
            Loaded += async (s, e) => await UpdateService.AutoCheckOnStartupAsync(isLite: false, this);
            Loaded += (s, e) => CheckDefaultAppBanner();
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

        private void Window_Drop(object sender, System.Windows.DragEventArgs e)
        {
            try
            {
                if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
                {
                    string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
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
        }

        #region Custom Integrated Window Chrome Controls (Min, Max, Close)

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

        #region Native Pdfium Viewer

        /// <summary>
        /// Opens the Pdfium document for the tab (if not already open) and renders all pages.
        /// </summary>
        private async Task LoadAndRenderTab(PdfDocumentTab tab, bool isSplit = false)
        {
            try
            {
                // Open the document if not already cached on the tab
                if (tab.PdfDoc == null || tab.PdfDoc.PageCount == 0)
                {
                    tab.PdfDoc?.Dispose();
                    tab.PdfDoc = await Task.Run(() => PdfViewerService.OpenDocument(tab.FilePath));
                    if (tab.PdfDoc == null) return;
                    tab.TotalPages = tab.PdfDoc.PageCount;
                }

                await RenderPdfPagesAsync(tab, isSplit);
            }
            catch { }
        }

        /// <summary>
        /// Renders all pages of the tab's PDF into the viewer StackPanel.
        /// Pages are rendered on a background thread and added to the UI progressively.
        /// </summary>
        private async Task RenderPdfPagesAsync(PdfDocumentTab tab, bool isSplit = false)
        {
            var cts = new CancellationTokenSource();
            if (isSplit)
            {
                _splitRenderCts?.Cancel();
                _splitRenderCts = cts;
            }
            else
            {
                _renderCts?.Cancel();
                _renderCts = cts;
            }

            var token = cts.Token;
            var panel = isSplit ? SplitPdfPagesPanel : PdfPagesPanel;
            var doc = tab.PdfDoc;
            if (doc == null) return;

            double zoom = tab.ZoomLevel;
            int total = doc.PageCount;

            // Clear old pages
            await Dispatcher.InvokeAsync(() =>
            {
                panel.Children.Clear();
                tab.PageOffsets.Clear();
                if (!isSplit)
                {
                    TxtViewerPageInfo.Text = $"Página 1 de {total}";
                    UpdateZoomLabel(zoom);
                }
            });

            double accumulatedOffset = 16; // top margin

            for (int i = 0; i < total; i++)
            {
                if (token.IsCancellationRequested) return;

                int pageIndex = i;
                int pageNum = i + 1;

                // Get page size on background thread
                var pageSize = await Task.Run(() => PdfViewerService.GetPageSize(doc, pageIndex, BASE_DPI, zoom));

                // Create placeholder on UI thread
                var border = await Dispatcher.InvokeAsync(() =>
                {
                    var img = new Image
                    {
                        Width = pageSize.Width,
                        Height = pageSize.Height,
                        Stretch = Stretch.None,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                        VerticalAlignment = System.Windows.VerticalAlignment.Top
                    };
                    RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);

                    var bd = new Border
                    {
                        Child = img,
                        Background = Brushes.White,
                        BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B")),
                        BorderThickness = new Thickness(1),
                        Margin = new Thickness(0, 0, 0, 12),
                        Effect = new System.Windows.Media.Effects.DropShadowEffect
                        {
                            BlurRadius = 18,
                            ShadowDepth = 4,
                            Opacity = 0.35,
                            Color = Colors.Black
                        }
                    };

                    tab.PageOffsets[pageNum] = accumulatedOffset;
                    panel.Children.Add(bd);
                    return (bd, img);
                });

                accumulatedOffset += pageSize.Height + 12;

                // Render bitmap on background thread
                var bitmap = await Task.Run(() =>
                {
                    if (token.IsCancellationRequested) return null;
                    return PdfViewerService.RenderPage(doc, pageIndex, BASE_DPI, zoom);
                }, token);

                if (token.IsCancellationRequested) return;
                if (bitmap == null) continue;

                await Dispatcher.InvokeAsync(() =>
                {
                    border.img.Source = bitmap;
                    border.img.Width = bitmap.PixelWidth;
                    border.img.Height = bitmap.PixelHeight;
                });
            }
        }

        private void UpdateZoomLabel(double zoom)
        {
            TxtZoomLevel.Text = $"{(int)Math.Round(zoom * 100)}%";
        }

        private async void BtnZoomIn_Click(object sender, RoutedEventArgs e)
        {
            if (_activeTab == null) return;
            _activeTab.ZoomLevel = Math.Min(ZOOM_MAX, _activeTab.ZoomLevel + ZOOM_STEP);
            UpdateZoomLabel(_activeTab.ZoomLevel);
            await RenderPdfPagesAsync(_activeTab, isSplit: false);
        }

        private async void BtnZoomOut_Click(object sender, RoutedEventArgs e)
        {
            if (_activeTab == null) return;
            _activeTab.ZoomLevel = Math.Max(ZOOM_MIN, _activeTab.ZoomLevel - ZOOM_STEP);
            UpdateZoomLabel(_activeTab.ZoomLevel);
            await RenderPdfPagesAsync(_activeTab, isSplit: false);
        }

        private async void BtnZoomFit_Click(object sender, RoutedEventArgs e)
        {
            if (_activeTab?.PdfDoc == null) return;
            // Fit first page width to the ScrollViewer's viewport width
            var size = PdfViewerService.GetPageSize(_activeTab.PdfDoc, 0, BASE_DPI, 1.0);
            double viewportWidth = PdfScrollViewer.ViewportWidth - 32; // margins
            if (viewportWidth > 0 && size.Width > 0)
            {
                _activeTab.ZoomLevel = Math.Clamp(viewportWidth / size.Width, ZOOM_MIN, ZOOM_MAX);
            }
            else
            {
                _activeTab.ZoomLevel = 1.0;
            }
            UpdateZoomLabel(_activeTab.ZoomLevel);
            await RenderPdfPagesAsync(_activeTab, isSplit: false);
        }

        private async void PdfScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control))
            {
                e.Handled = true;
                if (_activeTab == null) return;
                double newZoom = e.Delta > 0
                    ? Math.Min(ZOOM_MAX, _activeTab.ZoomLevel + ZOOM_STEP)
                    : Math.Max(ZOOM_MIN, _activeTab.ZoomLevel - ZOOM_STEP);
                if (Math.Abs(newZoom - _activeTab.ZoomLevel) < 0.001) return;
                _activeTab.ZoomLevel = newZoom;
                UpdateZoomLabel(newZoom);
                await RenderPdfPagesAsync(_activeTab, isSplit: false);
            }
        }

        private async void SplitScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control))
            {
                e.Handled = true;
                if (_splitTab == null) return;
                double newZoom = e.Delta > 0
                    ? Math.Min(ZOOM_MAX, _splitTab.ZoomLevel + ZOOM_STEP)
                    : Math.Max(ZOOM_MIN, _splitTab.ZoomLevel - ZOOM_STEP);
                if (Math.Abs(newZoom - _splitTab.ZoomLevel) < 0.001) return;
                _splitTab.ZoomLevel = newZoom;
                await RenderPdfPagesAsync(_splitTab, isSplit: true);
            }
        }

        private void PdfScrollViewer_ScrollChanged(object sender, System.Windows.Controls.ScrollChangedEventArgs e)
        {
            if (_activeTab == null || _activeTab.PageOffsets.Count == 0) return;
            double viewport = PdfScrollViewer.VerticalOffset + PdfScrollViewer.ViewportHeight / 2;
            int currentPage = 1;
            foreach (var kv in _activeTab.PageOffsets.OrderBy(x => x.Key))
            {
                if (kv.Value <= viewport) currentPage = kv.Key;
                else break;
            }
            if (currentPage != _activePageNum)
            {
                _activePageNum = currentPage;
                UpdatePageCardsHighlight();
                TxtViewerPageInfo.Text = $"Página {currentPage} de {_activeTab.TotalPages}";
            }
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

                // Check overflow: if tabs are already at capacity (at minimum width limit ~48px)
                double availableWidth = ScrollTabs?.ActualWidth ?? 0;
                if (availableWidth <= 0) availableWidth = 800;
                double btnWidth = BtnNewTab?.ActualWidth > 0 ? BtnNewTab.ActualWidth : 95;
                double usableWidth = Math.Max(100, availableWidth - btnWidth - 30);
                int maxPossibleTabs = Math.Max(1, (int)(usableWidth / 52.0));

                if (_tabs.Count >= maxPossibleTabs && _tabs.Count > 0)
                {
                    // Open in a new window to prevent tab bar overflow beyond 1 character!
                    var newWin = new MainWindow();
                    newWin.Show();
                    newWin.OpenTab(filePath);
                    return;
                }

                var tab = new PdfDocumentTab
                {
                    FilePath = filePath
                };

                _tabs.Add(tab);
                SetActiveTab(tab);

                // Run extraction & integrity analysis in background
                _ = Task.Run(() =>
                {
                    var result = _extractionService.ExtractAndAnalyze(filePath);
                    Dispatcher.Invoke(() =>
                    {
                        tab.Extraction = result;
                        tab.TotalPages = result.Report.TotalPages;
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
            _activeTab = tab;
            _activePageNum = 1;
            Title = $"{tab.FileName} - FT PDF";
            UpdateTabsBar();

            PanelEmptyState.Visibility = Visibility.Collapsed;
            PanelActiveContent.Visibility = Visibility.Visible;
            BtnQuickSave.Visibility = Visibility.Visible;
            BtnQuickCopy.Visibility = Visibility.Visible;
            BtnToggleNotepad.Visibility = Visibility.Visible;
            BtnTogglePages.Visibility = Visibility.Visible;

            // Load and render via native Pdfium (no WebView, no flicker)
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
            Title = "FT PDF";
            PanelEmptyState.Visibility = Visibility.Visible;
            PanelActiveContent.Visibility = Visibility.Collapsed;
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

            double availableWidth = ScrollTabs?.ActualWidth ?? 0;
            if (availableWidth <= 0) availableWidth = 800;
            double btnWidth = BtnNewTab?.ActualWidth > 0 ? BtnNewTab.ActualWidth : 95;
            double usableWidth = Math.Max(100, availableWidth - btnWidth - 30);
            int count = _tabs.Count;
            double widthForTabs = Math.Max(48 * count, usableWidth - (count * 4));
            double targetWidth = count > 0 ? (widthForTabs / count) : 300;
            double tabWidth = Math.Clamp(targetWidth, 48, 300);

            foreach (var tab in _tabs)
            {
                bool isActive = (tab == _activeTab);

                var tabBorder = new Border
                {
                    Background = new SolidColorBrush(isActive 
                        ? (Color)ColorConverter.ConvertFromString("#1E293B") 
                        : Colors.Transparent),
                    BorderBrush = new SolidColorBrush(isActive 
                        ? (Color)ColorConverter.ConvertFromString("#334155") 
                        : Colors.Transparent),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(5),
                    Width = tabWidth,
                    Height = 28,
                    Padding = new Thickness(Math.Min(10, Math.Max(4, tabWidth * 0.05)), 2, Math.Min(8, Math.Max(4, tabWidth * 0.04)), 2),
                    Margin = new Thickness(0, 0, 4, 0),
                    Cursor = Cursors.Hand,
                    ToolTip = $"{tab.FileName}\n(Arraste para fora para dividir a tela lado a lado)"
                };

                var dp = new DockPanel { LastChildFill = true, VerticalAlignment = VerticalAlignment.Center };

                var icon = new TextBlock
                {
                    Text = "📄",
                    FontSize = 11,
                    Margin = new Thickness(0, 0, tabWidth > 70 ? 6 : 2, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                DockPanel.SetDock(icon, Dock.Left);

                var closeBtn = new Button
                {
                    Content = "✕",
                    FontSize = 9.5,
                    Width = 16,
                    Height = 16,
                    Margin = new Thickness(tabWidth > 70 ? 6 : 1, 0, 0, 0),
                    Background = Brushes.Transparent,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B")),
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    ToolTip = "Fechar aba"
                };
                closeBtn.Click += (s, e) =>
                {
                    e.Handled = true;
                    CloseTab(tab);
                };
                DockPanel.SetDock(closeBtn, Dock.Right);

                double maxTitleWidth = Math.Max(8, tabWidth - 44);
                var title = new TextBlock
                {
                    Text = tab.FileName,
                    FontSize = 11.5,
                    FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = new SolidColorBrush(isActive ? Colors.White : (Color)ColorConverter.ConvertFromString("#94A3B8")),
                    MaxWidth = maxTitleWidth,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center
                };

                dp.Children.Add(icon);
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
                                if (_tabs.Count > 1 && _splitTab == null)
                                {
                                    OverlaySplitDropZone.Visibility = Visibility.Visible;
                                    PdfScrollViewer.Visibility = Visibility.Hidden;
                                }

                                var dragData = new TabDragData
                                {
                                    Tab = tab,
                                    FilePath = tab.FilePath,
                                    SourceWindow = this
                                };

                                var dataObj = new DataObject();
                                dataObj.SetData(typeof(TabDragData), dragData);
                                dataObj.SetData(DataFormats.FileDrop, new string[] { tab.FilePath });
                                dataObj.SetData(DataFormats.Text, tab.FilePath);

                                DragDrop.DoDragDrop(tabBorder, dataObj, DragDropEffects.Move);
                            }
                            catch { }
                            finally
                            {
                                OverlaySplitDropZone.Visibility = Visibility.Collapsed;
                                PdfScrollViewer.Visibility = Visibility.Visible;
                                if (_splitTab != null) SplitPdfScrollViewer.Visibility = Visibility.Visible;
                            }
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

        #region Editing Tools Handlers

        private Point _dragStartPoint;
        private double _startHOffset;
        private double _startVOffset;
        private bool _isDraggingPopup = false;
        private string _selectedTextColorHex = "#000000";

        private void BtnToolText_Click(object sender, RoutedEventArgs e)
        {
            if (_activeTab == null)
            {
                MessageBox.Show(this, "Abra um arquivo PDF primeiro para inserir caixas de texto.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Populate Page dropdown
            CmbFloatingPage.Items.Clear();
            for (int i = 1; i <= _activeTab.TotalPages; i++)
            {
                CmbFloatingPage.Items.Add(new ComboBoxItem { Content = i.ToString(), IsSelected = (i == 1) });
            }

            // Center initial position
            PopupFloatingTextBox.HorizontalOffset = 0;
            PopupFloatingTextBox.VerticalOffset = 0;

            // Reset text and open
            TxtFloatingInput.Text = "Digite seu texto aqui...";
            PopupFloatingTextBox.IsOpen = true;

            // Set focus to the text box and select all so user can immediately type
            Dispatcher.BeginInvoke(new Action(() =>
            {
                TxtFloatingInput.Focus();
                TxtFloatingInput.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        #region Floating Text Box Interactivity Handlers

        private void DragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDraggingPopup = true;
            _dragStartPoint = e.GetPosition(this);
            _startHOffset = PopupFloatingTextBox.HorizontalOffset;
            _startVOffset = PopupFloatingTextBox.VerticalOffset;
            ((UIElement)sender).CaptureMouse();
        }

        private void DragHandle_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingPopup)
            {
                Point current = e.GetPosition(this);
                Vector diff = current - _dragStartPoint;
                PopupFloatingTextBox.HorizontalOffset = _startHOffset + diff.X;
                PopupFloatingTextBox.VerticalOffset = _startVOffset + diff.Y;
            }
        }

        private void DragHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingPopup)
            {
                _isDraggingPopup = false;
                ((UIElement)sender).ReleaseMouseCapture();
            }
        }

        private void ResizeThumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            double newWidth = Math.Max(180, BoxBorderContainer.Width + e.HorizontalChange);
            double newHeight = Math.Max(50, BoxBorderContainer.Height + e.VerticalChange);
            BoxBorderContainer.Width = newWidth;
            BoxBorderContainer.Height = newHeight;
        }

        private void CmbFloatingFontSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbFloatingFontSize != null && CmbFloatingFontSize.SelectedItem is ComboBoxItem item &&
                double.TryParse(item.Content.ToString(), out double sz) &&
                TxtFloatingInput != null)
            {
                TxtFloatingInput.FontSize = sz;
            }
        }

        private void BtnColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string hex)
            {
                _selectedTextColorHex = hex;
                var color = (Color)ColorConverter.ConvertFromString(hex);
                TxtFloatingInput.Foreground = new SolidColorBrush(color);

                // Update visual indication on color buttons
                Button[] colorBtns = { BtnColorBlack, BtnColorWhite, BtnColorRed, BtnColorBlue, BtnColorYellow };
                foreach (var b in colorBtns)
                {
                    if (b == null) continue;
                    bool isThis = (b == btn);
                    b.BorderBrush = isThis ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#38BDF8")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));
                    b.BorderThickness = isThis ? new Thickness(2) : new Thickness(1);
                }
            }
        }

        private void BtnFloatingStyle_Click(object sender, RoutedEventArgs e)
        {
            if (TxtFloatingInput == null) return;
            TxtFloatingInput.FontWeight = (BtnFloatingBold.IsChecked == true) ? FontWeights.Bold : FontWeights.Normal;
            TxtFloatingInput.FontStyle = (BtnFloatingItalic.IsChecked == true) ? FontStyles.Italic : FontStyles.Normal;
        }

        private void BtnCloseFloatingText_Click(object sender, RoutedEventArgs e)
        {
            PopupFloatingTextBox.IsOpen = false;
        }

        private void BtnApplyFloatingText_Click(object sender, RoutedEventArgs e)
        {
            if (_activeTab == null || !File.Exists(_activeTab.FilePath)) return;

            string text = TxtFloatingInput.Text.Trim();
            if (string.IsNullOrEmpty(text))
            {
                MessageBox.Show(this, "Por favor, digite algum texto antes de aplicar no PDF.", "Texto Vazio", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int pageNumber = 1;
            if (CmbFloatingPage.SelectedItem is ComboBoxItem pageItem && int.TryParse(pageItem.Content.ToString(), out int p))
            {
                pageNumber = p;
            }

            double fontSize = TxtFloatingInput.FontSize;
            bool isBold = BtnFloatingBold.IsChecked == true;
            bool isItalic = BtnFloatingItalic.IsChecked == true;

            var wpfColor = (Color)ColorConverter.ConvertFromString(_selectedTextColorHex);
            var xColor = PdfSharp.Drawing.XColor.FromArgb(wpfColor.A, wpfColor.R, wpfColor.G, wpfColor.B);

            // Compute PDF coordinates based on container size
            double viewerW = Math.Max(CenterDocumentContainer.ActualWidth, 400);
            double viewerH = Math.Max(CenterDocumentContainer.ActualHeight, 400);

            double relX = (viewerW / 2.0) + PopupFloatingTextBox.HorizontalOffset - (BoxBorderContainer.Width / 2.0);
            double relY = (viewerH / 2.0) + PopupFloatingTextBox.VerticalOffset - (BoxBorderContainer.Height / 2.0);

            double normX = Math.Clamp(relX / viewerW, 0.05, 0.90);
            double normY = Math.Clamp(relY / viewerH, 0.05, 0.90);

            double pdfPageW = 595;
            double pdfPageH = 842;
            try
            {
                using var doc = PdfSharp.Pdf.IO.PdfReader.Open(_activeTab.FilePath, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
                if (pageNumber <= doc.PageCount)
                {
                    pdfPageW = doc.Pages[pageNumber - 1].Width.Point;
                    pdfPageH = doc.Pages[pageNumber - 1].Height.Point;
                }
            }
            catch {}

            double pdfX = normX * pdfPageW;
            double pdfY = normY * pdfPageH;
            double pdfBoxWidth = (BoxBorderContainer.Width / viewerW) * pdfPageW;

            var saveDialog = new SaveFileDialog
            {
                Filter = "Arquivo PDF (*.pdf)|*.pdf",
                DefaultExt = "pdf",
                FileName = Path.GetFileNameWithoutExtension(_activeTab.FilePath) + "_texto.pdf",
                Title = "Salvar PDF com Caixa de Texto"
            };

            if (saveDialog.ShowDialog(this) == true)
            {
                try
                {
                    _editingService.InsertFormattedTextBox(
                        _activeTab.FilePath,
                        saveDialog.FileName,
                        pageNumber,
                        text,
                        pdfX,
                        pdfY,
                        pdfBoxWidth,
                        fontSize,
                        xColor,
                        isBold,
                        isItalic
                    );

                    PopupFloatingTextBox.IsOpen = false;
                    OpenTab(saveDialog.FileName);
                    MessageBox.Show(this, "Texto inserido com sucesso!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Erro ao gravar texto no PDF:\n{ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion

        private void BtnToolHighlight_Click(object sender, RoutedEventArgs e)
        {
            if (_activeTab == null)
            {
                MessageBox.Show(this, "Abra um arquivo PDF primeiro para adicionar destaques.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new HighlightDialog(_activeTab.TotalPages, 1) { Owner = this };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var saveDialog = new SaveFileDialog
                    {
                        Filter = "Arquivo PDF (*.pdf)|*.pdf",
                        DefaultExt = "pdf",
                        FileName = Path.GetFileNameWithoutExtension(_activeTab.FilePath) + "_destacado.pdf",
                        Title = "Salvar PDF com Destaque"
                    };

                    if (saveDialog.ShowDialog(this) == true)
                    {
                        _editingService.AddHighlight(
                            _activeTab.FilePath,
                            saveDialog.FileName,
                            dialog.PageNumber,
                            dialog.PosX,
                            dialog.PosY,
                            dialog.RectWidth,
                            dialog.RectHeight,
                            dialog.HighlightColor
                        );

                        OpenTab(saveDialog.FileName);
                        MessageBox.Show(this, "Destaque aplicado com sucesso!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Erro ao aplicar destaque no PDF:\n{ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnToolSignature_Click(object sender, RoutedEventArgs e)
        {
            if (_activeTab == null)
            {
                MessageBox.Show(this, "Abra um arquivo PDF primeiro para assinar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SignatureDialog(_activeTab.TotalPages, 1) { Owner = this };

            if (dialog.ShowDialog() == true && dialog.SignatureImageBytes != null)
            {
                try
                {
                    var saveDialog = new SaveFileDialog
                    {
                        Filter = "Arquivo PDF (*.pdf)|*.pdf",
                        DefaultExt = "pdf",
                        FileName = Path.GetFileNameWithoutExtension(_activeTab.FilePath) + "_assinado.pdf",
                        Title = "Salvar PDF com Assinatura"
                    };

                    if (saveDialog.ShowDialog(this) == true)
                    {
                        _editingService.InsertSignature(
                            _activeTab.FilePath,
                            saveDialog.FileName,
                            dialog.PageNumber,
                            dialog.SignatureImageBytes,
                            dialog.PosX,
                            dialog.PosY,
                            dialog.SigWidth,
                            dialog.SigHeight
                        );

                        OpenTab(saveDialog.FileName);
                        MessageBox.Show(this, "Assinatura digital aplicada com sucesso!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Erro ao aplicar assinatura no PDF:\n{ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnToolSplit_Click(object sender, RoutedEventArgs e)
        {
            if (_activeTab == null)
            {
                MessageBox.Show(this, "Abra um arquivo PDF primeiro para extrair páginas.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SplitPdfDialog(_activeTab.TotalPages, 1) { Owner = this };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    _editingService.ExtractPages(_activeTab.FilePath, dialog.OutputFilePath, dialog.SelectedPages);
                    MessageBox.Show(this, $"Páginas extraídas com sucesso para:\n{dialog.OutputFilePath}", "Extração Concluída", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Erro ao extrair páginas do PDF:\n{ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnToolMerge_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new MergePdfDialog(_activeTab?.FilePath) { Owner = this };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    _editingService.MergePdfs(dialog.FilesToMerge, dialog.OutputFilePath);
                    var result = MessageBox.Show(this, $"PDFs mesclados com sucesso em:\n{dialog.OutputFilePath}\n\nDeseja abrir o documento mesclado agora?", "Mesclagem Concluída", MessageBoxButton.YesNo, MessageBoxImage.Information);
                    if (result == MessageBoxResult.Yes)
                    {
                        OpenTab(dialog.OutputFilePath);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Erro ao mesclar arquivos PDF:\n{ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnToolRotate_Click(object sender, RoutedEventArgs e)
        {
            if (_activeTab == null)
            {
                MessageBox.Show(this, "Abra um arquivo PDF primeiro para girar páginas.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                string tempOut = Path.Combine(Path.GetDirectoryName(_activeTab.FilePath)!,
                    Path.GetFileNameWithoutExtension(_activeTab.FilePath) + "_girado.pdf");

                _editingService.RotatePages(_activeTab.FilePath, tempOut, Enumerable.Range(1, _activeTab.TotalPages), 90);

                OpenTab(tempOut);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Erro ao girar documento:\n{ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
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

        /// <summary>Anima suavemente a largura de uma ColumnDefinition de um valor para outro.</summary>
        private void AnimateColumn(ColumnDefinition col, double from, double to, int durationMs = 220, Action? onCompleted = null)
        {
            var anim = new GridLengthAnimation
            {
                From = new GridLength(from),
                To = new GridLength(to),
                Duration = new Duration(TimeSpan.FromMilliseconds(durationMs)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            if (onCompleted != null)
                anim.Completed += (s, e) => onCompleted();
            col.BeginAnimation(ColumnDefinition.WidthProperty, anim);
        }

        private void OpenNotepad()
        {
            _isNotepadOpen = true;
            double currentWidth = ColNotepad.ActualWidth > 0 ? ColNotepad.ActualWidth : 0;
            PanelNotepad.Visibility = Visibility.Visible;
            SplitterBar.Visibility = Visibility.Visible;
            BtnToggleNotepad.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"));
            BtnToggleNotepad.Foreground = Brushes.White;
            AnimateColumn(ColNotepad, currentWidth, 530);
        }

        private void CloseNotepad()
        {
            _isNotepadOpen = false;
            double currentWidth = ColNotepad.ActualWidth > 0 ? ColNotepad.ActualWidth : 530;
            BtnToggleNotepad.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B"));
            BtnToggleNotepad.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FACC15"));
            AnimateColumn(ColNotepad, currentWidth, 0, onCompleted: () =>
            {
                PanelNotepad.Visibility = Visibility.Collapsed;
                SplitterBar.Visibility = Visibility.Collapsed;
            });
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
                    if (data.Tab != null && _splitTab == data.Tab)
                    {
                        // Unir tela dividida de volta como aba
                        ExitSplitScreen(mergeBack: true);
                        e.Handled = true;
                        return;
                    }

                    if (data.SourceWindow != null && data.SourceWindow != this && !string.IsNullOrEmpty(data.FilePath))
                    {
                        // Integrar aba vinda de outra janela para esta janela
                        data.SourceWindow.RemoveTabByPath(data.FilePath);
                        OpenTab(data.FilePath);
                        e.Handled = true;
                        return;
                    }

                    if (data.Tab != null && _tabs.Contains(data.Tab))
                    {
                        SetActiveTab(data.Tab);
                        e.Handled = true;
                        return;
                    }
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

        private void SplitDropZone_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(TabDragData)) || e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
            }
        }

        private void SplitDropZone_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(TabDragData)))
            {
                var data = e.Data.GetData(typeof(TabDragData)) as TabDragData;
                if (data?.Tab != null)
                {
                    OpenInSplitScreen(data.Tab);
                    e.Handled = true;
                }
            }
            else if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null && files.Length > 0 && files[0].EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    var tab = new PdfDocumentTab { FilePath = files[0] };
                    OpenInSplitScreen(tab);
                    e.Handled = true;
                }
            }
        }

        private void ViewerArea_DragOver(object sender, DragEventArgs e) => SplitDropZone_DragOver(sender, e);
        private void ViewerArea_Drop(object sender, DragEventArgs e) => SplitDropZone_Drop(sender, e);

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

        public void RemoveTabByPath(string filePath)
        {
            var tab = _tabs.FirstOrDefault(t => t.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
            if (tab != null)
            {
                CloseTab(tab);
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
            double currentWidth = ColPageSidebar.ActualWidth > 0 ? ColPageSidebar.ActualWidth : 0;
            PanelPageSidebar.Visibility = Visibility.Visible;
            BtnTogglePages.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"));
            BtnTogglePages.Foreground = Brushes.White;
            AnimateColumn(ColPageSidebar, currentWidth, 200);
            UpdatePageCards();
            if (_activeTab != null) StartThumbnailGeneration(_activeTab);
        }

        private void ClosePageSidebar()
        {
            _isPageSidebarOpen = false;
            double currentWidth = ColPageSidebar.ActualWidth > 0 ? ColPageSidebar.ActualWidth : 200;
            BtnTogglePages.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B"));
            BtnTogglePages.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#38BDF8"));
            AnimateColumn(ColPageSidebar, currentWidth, 0, onCompleted: () =>
            {
                PanelPageSidebar.Visibility = Visibility.Collapsed;
            });
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
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 8),
                    MaxHeight = 220,
                    ClipToBounds = true
                };

                var imgThumb = new Image
                {
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
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
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center
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
                    using var doc = PdfiumViewer.PdfDocument.Load(tab.FilePath);
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

        private System.Windows.Threading.DispatcherTimer? _scrollAnimationTimer;

        private void SmoothScrollTo(System.Windows.Controls.ScrollViewer scrollViewer, double targetOffset, int durationMs = 280)
        {
            _scrollAnimationTimer?.Stop();
            double startOffset = scrollViewer.VerticalOffset;
            if (Math.Abs(startOffset - targetOffset) < 1) return;

            var startTime = DateTime.UtcNow;
            _scrollAnimationTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
            };

            _scrollAnimationTimer.Tick += (s, e) =>
            {
                double elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
                double progress = Math.Clamp(elapsed / durationMs, 0.0, 1.0);

                // Cubic ease out curve: 1 - (1 - t)^3
                double eased = 1.0 - Math.Pow(1.0 - progress, 3);
                double currentOffset = startOffset + (targetOffset - startOffset) * eased;

                scrollViewer.ScrollToVerticalOffset(currentOffset);

                if (progress >= 1.0)
                {
                    _scrollAnimationTimer?.Stop();
                    scrollViewer.ScrollToVerticalOffset(targetOffset);
                }
            };

            _scrollAnimationTimer.Start();
        }

        private void JumpToPage(int pageNum)
        {
            if (_activeTab == null) return;

            _activePageNum = pageNum;
            UpdatePageCardsHighlight();
            TxtViewerPageInfo.Text = $"Página {pageNum} de {_activeTab.TotalPages}";

            // Scroll the sidebar card into view
            if (_pageCardMap.TryGetValue(pageNum, out var card))
                card.BringIntoView();

            // Native WPF smooth scroll — silky smooth, zero flicker
            if (_activeTab.PageOffsets.TryGetValue(pageNum, out double offset))
            {
                SmoothScrollTo(PdfScrollViewer, offset, 280);
            }
        }

        #endregion

        #region Default App Banner

        private void CheckDefaultAppBanner()
        {
            try
            {
                bool isDef = DefaultAppService.IsDefaultPdfReader(isLite: false);
                bool dismissed = DefaultAppService.IsDismissed(isLite: false);
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
            DefaultAppService.RegisterAndSetDefault(isLite: false);
        }

        private void BtnDismissDefaultBanner_Click(object sender, RoutedEventArgs e)
        {
            BannerDefaultApp.Visibility = Visibility.Collapsed;
            DefaultAppService.DismissPrompt(isLite: false);
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
