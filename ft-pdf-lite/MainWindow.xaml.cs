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

namespace FtPdfLite
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
        private bool _isRawTextMode = false;
        private bool _isNotepadOpen = false;
        private bool _isWebViewInitialized = false;
        private bool _isPageSidebarOpen = false;
        private PdfDocumentTab? _splitTab = null;
        private bool _isSplitWebViewInitialized = false;
        private readonly Dictionary<int, Image> _pageImageMap = new();
        private readonly Dictionary<int, Border> _pageCardMap = new();
        private int _activePageNum = 1;
        private CancellationTokenSource? _thumbnailCts;
        private CancellationTokenSource? _jumpCts;

        public MainWindow()
        {
            InitializeComponent();
            StateChanged += MainWindow_StateChanged;
            PreviewKeyDown += MainWindow_PreviewKeyDown;
            InitializeViewerAsync();
            CheckCommandLineArgs();
            Loaded += async (s, e) => await UpdateService.AutoCheckOnStartupAsync(isLite: true, this);
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

        private async void InitializeViewerAsync()
        {
            try
            {
                PdfWebViewer.DefaultBackgroundColor = System.Drawing.Color.FromArgb(15, 23, 42);

                var userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FtPdfLite", "WebView2");
                Directory.CreateDirectory(userDataFolder);
                var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await PdfWebViewer.EnsureCoreWebView2Async(env);
                _isWebViewInitialized = true;
                PdfWebViewer.CoreWebView2.Settings.IsStatusBarEnabled = false;
                PdfWebViewer.CoreWebView2.Settings.AreDevToolsEnabled = false;
                PdfWebViewer.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                PdfWebViewer.CoreWebView2.Settings.IsZoomControlEnabled = false;

                _ = EnsureSplitViewerInitializedAsync();

                if (_activeTab != null && File.Exists(_activeTab.FilePath))
                {
                    NavigateToPdf(_activeTab.FilePath);
                }
            }
            catch
            {
            }
        }

        private void NavigateToPdf(string filePath)
        {
            string cleanUrl = $"{new Uri(filePath).AbsoluteUri}#toolbar=0&navpanes=0";
            if (_isWebViewInitialized && PdfWebViewer.CoreWebView2 != null)
            {
                PdfWebViewer.CoreWebView2.Navigate(cleanUrl);
            }
            else
            {
                PdfWebViewer.Source = new Uri(cleanUrl);
            }
        }

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
            Title = $"{tab.FileName} - FT PDF Lite";
            UpdateTabsBar();

            PanelEmptyState.Visibility = Visibility.Collapsed;
            PanelActiveContent.Visibility = Visibility.Visible;
            BtnQuickSave.Visibility = Visibility.Visible;
            BtnQuickCopy.Visibility = Visibility.Visible;
            BtnToggleNotepad.Visibility = Visibility.Visible;
            BtnTogglePages.Visibility = Visibility.Visible;

            NavigateToPdf(tab.FilePath);
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
                                    PdfWebViewer.Visibility = Visibility.Hidden;
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
                                PdfWebViewer.Visibility = Visibility.Visible;
                                if (_splitTab != null) SplitPdfWebViewer.Visibility = Visibility.Visible;
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

            await EnsureSplitViewerInitializedAsync();

            string cleanUrl = $"{new Uri(tab.FilePath).AbsoluteUri}#toolbar=0&navpanes=0";
            if (_isSplitWebViewInitialized && SplitPdfWebViewer.CoreWebView2 != null)
            {
                SplitPdfWebViewer.CoreWebView2.Navigate(cleanUrl);
            }
            else
            {
                SplitPdfWebViewer.Source = new Uri(cleanUrl);
            }
        }

        private async Task EnsureSplitViewerInitializedAsync()
        {
            if (_isSplitWebViewInitialized) return;
            try
            {
                SplitPdfWebViewer.DefaultBackgroundColor = System.Drawing.Color.FromArgb(15, 23, 42);
                var userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FtPdfLite", "WebView2_Split");
                Directory.CreateDirectory(userDataFolder);
                var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await SplitPdfWebViewer.EnsureCoreWebView2Async(env);
                _isSplitWebViewInitialized = true;
                SplitPdfWebViewer.CoreWebView2.Settings.IsStatusBarEnabled = false;
                SplitPdfWebViewer.CoreWebView2.Settings.AreDevToolsEnabled = false;
                SplitPdfWebViewer.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                SplitPdfWebViewer.CoreWebView2.Settings.IsZoomControlEnabled = false;
            }
            catch { }
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

        private async void JumpToPage(int pageNum)
        {
            if (_activeTab == null || string.IsNullOrWhiteSpace(_activeTab.FilePath)) return;

            _activePageNum = pageNum;
            UpdatePageCardsHighlight();

            if (_pageCardMap.TryGetValue(pageNum, out var card))
            {
                card.BringIntoView();
            }

            _jumpCts?.Cancel();
            _jumpCts = new CancellationTokenSource();
            var token = _jumpCts.Token;

            string cleanUrl = $"{new Uri(_activeTab.FilePath).AbsoluteUri}#page={pageNum}&toolbar=0&navpanes=0";
            if (_isWebViewInitialized && PdfWebViewer.CoreWebView2 != null)
            {
                try
                {
                    // Transição imediata com mesma cor escura de fundo para não causar flash branco,
                    // forçando o Chromium a recarregar o documento diretamente na página alvo (#page=N)
                    PdfWebViewer.CoreWebView2.NavigateToString("<html style='background:#0F172A;margin:0;padding:0;overflow:hidden;'></html>");
                    await Task.Delay(40, token);
                    if (!token.IsCancellationRequested)
                    {
                        PdfWebViewer.CoreWebView2.Navigate(cleanUrl);
                    }
                }
                catch (OperationCanceledException) { }
                catch
                {
                    PdfWebViewer.CoreWebView2.Navigate(cleanUrl);
                }
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
