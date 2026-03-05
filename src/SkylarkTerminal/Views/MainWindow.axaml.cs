using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using SkylarkTerminal.ViewModels;
using System;
using System.ComponentModel;

namespace SkylarkTerminal.Views;

public partial class MainWindow : Window
{
    private const double AssetsPanelAutoCollapseThreshold = 180d;
    private const double AssetsPanelSplitterWidth = 8d;
    private const double RightSidebarAutoCollapseThreshold = 220d;
    private const double RightSidebarSplitterWidth = 8d;
    private MainWindowViewModel? _boundViewModel;
    private Grid? _mainContentGrid;

    public MainWindow()
    {
        InitializeComponent();
        ResolveMainContentGrid().LayoutUpdated += OnMainContentGridLayoutUpdated;
    }

    private ColumnDefinition AssetsPanelColumnDefinition => ResolveMainContentGrid().ColumnDefinitions[1];
    private ColumnDefinition AssetsPanelSplitterColumnDefinition => ResolveMainContentGrid().ColumnDefinitions[2];
    private ColumnDefinition RightSidebarSplitterColumnDefinition => ResolveMainContentGrid().ColumnDefinitions[4];
    private ColumnDefinition RightSidebarColumnDefinition => ResolveMainContentGrid().ColumnDefinitions[5];

    private Grid ResolveMainContentGrid()
    {
        _mainContentGrid ??= this.FindControl<Grid>("MainContentGrid")
            ?? throw new InvalidOperationException("MainContentGrid is not found.");
        return _mainContentGrid;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_boundViewModel is not null)
        {
            _boundViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (DataContext is not MainWindowViewModel vm)
        {
            _boundViewModel = null;
            return;
        }

        _boundViewModel = vm;
        _boundViewModel.PropertyChanged += OnViewModelPropertyChanged;

        vm.TopStatusBar.AttachWindowActions(
            minimizeWindowAction: () => WindowState = WindowState.Minimized,
            toggleMaximizeRestoreWindowAction: ToggleMaximizeRestoreWindow,
            closeWindowAction: Close);
        vm.TopStatusBar.SetWindowState(WindowState);
        vm.IsAssetsPanelVisible = AssetsPanelColumnDefinition.Width.Value > 0d;
        SyncAssetsPanelColumnVisibility(vm.IsAssetsPanelVisible);
        SyncRightSidebarColumnVisibility(vm.IsRightSidebarVisible);
        ApplyShellVisualMode(vm.IsShellTransparent);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == WindowStateProperty && _boundViewModel is not null)
        {
            _boundViewModel.TopStatusBar.SetWindowState(change.GetNewValue<WindowState>());
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_mainContentGrid is not null)
        {
            _mainContentGrid.LayoutUpdated -= OnMainContentGridLayoutUpdated;
        }

        if (_boundViewModel is not null)
        {
            _boundViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _boundViewModel = null;
        }

        base.OnClosed(e);
    }

    private void OnWorkspaceTabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        if (args.Item is WorkspaceTabItemViewModel tab)
        {
            vm.CloseTabCommand.Execute(tab);
        }
    }

    private void OnWorkspaceTabContextActionClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        var tab = menuItem.DataContext as WorkspaceTabItemViewModel;
        var action = menuItem.Tag?.ToString();

        switch (action)
        {
            case "duplicate":
                vm.DuplicateTabCommand.Execute(tab);
                break;
            case "close":
                vm.CloseTabCommand.Execute(tab);
                break;
            case "close-others":
                vm.CloseOtherTabsCommand.Execute(tab);
                break;
            case "close-right":
                vm.CloseTabsToRightCommand.Execute(tab);
                break;
            case "close-all":
                vm.CloseAllTabsCommand.Execute(null);
                break;
        }
    }

    private void ToggleMaximizeRestoreWindow()
    {
        if (!CanMaximize)
        {
            return;
        }

        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_boundViewModel is null)
        {
            return;
        }

        if (e.PropertyName == nameof(MainWindowViewModel.IsAssetsPanelVisible))
        {
            SyncAssetsPanelColumnVisibility(_boundViewModel.IsAssetsPanelVisible);
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.IsRightSidebarVisible))
        {
            SyncRightSidebarColumnVisibility(_boundViewModel.IsRightSidebarVisible);
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.IsShellTransparent) ||
                 e.PropertyName == nameof(MainWindowViewModel.ThemeModeLabel))
        {
            ApplyShellVisualMode(_boundViewModel.IsShellTransparent);
        }
    }

    private void OnAssetsPanelSplitterPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_boundViewModel is null)
        {
            return;
        }

        var currentWidth = AssetsPanelColumnDefinition.ActualWidth;
        if (currentWidth <= AssetsPanelAutoCollapseThreshold)
        {
            AssetsPanelColumnDefinition.Width = new GridLength(0d);
            _boundViewModel.IsAssetsPanelVisible = false;
            return;
        }

        if (!_boundViewModel.IsAssetsPanelVisible)
        {
            _boundViewModel.IsAssetsPanelVisible = true;
        }
    }

    private void OnMainContentGridLayoutUpdated(object? sender, EventArgs e)
    {
        if (_boundViewModel is null)
        {
            return;
        }

        if (_boundViewModel.IsAssetsPanelVisible)
        {
            var leftWidth = AssetsPanelColumnDefinition.ActualWidth;
            if (leftWidth > 0d && leftWidth <= AssetsPanelAutoCollapseThreshold)
            {
                AssetsPanelColumnDefinition.Width = new GridLength(0d);
                _boundViewModel.IsAssetsPanelVisible = false;
            }
        }

        if (_boundViewModel.IsRightSidebarVisible)
        {
            var rightWidth = ResolveColumnWidth(RightSidebarColumnDefinition);
            if (rightWidth <= RightSidebarAutoCollapseThreshold)
            {
                CollapseRightSidebar();
            }
        }
    }

    private void SyncAssetsPanelColumnVisibility(bool isOpen)
    {
        if (!isOpen)
        {
            AssetsPanelColumnDefinition.Width = new GridLength(0d);
            AssetsPanelSplitterColumnDefinition.Width = new GridLength(0d);
            return;
        }

        AssetsPanelSplitterColumnDefinition.Width = new GridLength(AssetsPanelSplitterWidth);
        if (AssetsPanelColumnDefinition.Width.Value <= 0d)
        {
            AssetsPanelColumnDefinition.Width = new GridLength(MainWindowViewModel.ExpandedLeftAssetsPaneWidth);
        }
    }

    private void OnRightSidebarSplitterPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_boundViewModel is null)
        {
            return;
        }

        var currentWidth = ResolveColumnWidth(RightSidebarColumnDefinition);
        if (currentWidth <= RightSidebarAutoCollapseThreshold)
        {
            CollapseRightSidebar();
            return;
        }

        if (!_boundViewModel.IsRightSidebarVisible)
        {
            _boundViewModel.IsRightSidebarVisible = true;
        }
    }

    private void SyncRightSidebarColumnVisibility(bool isVisible)
    {
        if (!isVisible)
        {
            CollapseRightSidebar();
            return;
        }

        RightSidebarSplitterColumnDefinition.Width = new GridLength(RightSidebarSplitterWidth);
        if (RightSidebarColumnDefinition.Width.Value <= 0d)
        {
            RightSidebarColumnDefinition.Width = new GridLength(MainWindowViewModel.ExpandedRightSidebarWidth);
        }
    }

    private void CollapseRightSidebar()
    {
        RightSidebarColumnDefinition.Width = new GridLength(0d);
        RightSidebarSplitterColumnDefinition.Width = new GridLength(0d);

        if (_boundViewModel is { IsRightSidebarVisible: true })
        {
            _boundViewModel.IsRightSidebarVisible = false;
        }
    }

    private static double ResolveColumnWidth(ColumnDefinition columnDefinition)
    {
        var defined = columnDefinition.Width;
        if (defined.IsAbsolute && defined.Value > 0d)
        {
            return defined.Value;
        }

        return columnDefinition.ActualWidth;
    }

    private void ApplyShellVisualMode(bool isTransparent)
    {
        TransparencyLevelHint = isTransparent
            ? [WindowTransparencyLevel.Mica, WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Blur]
            : [WindowTransparencyLevel.None];

        ApplyShellPalette(ResolveIsDarkTheme(), isTransparent);
    }

    private void ApplyShellPalette(bool isDarkTheme, bool isTransparent)
    {
        (var window, var topBar, var rail, var assets, var workspace, var tools, var panel, var border, var divider, var tabActive, var tabInactive, var tabSelectionBorder, var terminal) =
            (isDarkTheme, isTransparent) switch
            {
                (true, true) => (
                    "#1A10141A",
                    "#CC1E222A",
                    "#B821262F",
                    "#C0282D36",
                    "#B8171B22",
                    "#C01F242D",
                    "#AA262B34",
                    "#80323843",
                    "#882A313B",
                    "#CC2A303B",
                    "#B81C2129",
                    "#FFFFFFFF",
                    "#CC10141A"),
                (false, true) => (
                    "#1AFFFFFF",
                    "#CCF5F6F8",
                    "#CCE8ECF1",
                    "#CCEEF1F4",
                    "#D9FDFEFF",
                    "#CCF0F3F6",
                    "#CCF1F3F6",
                    "#80D5DBE4",
                    "#88E0E4EB",
                    "#CCE5EAF0",
                    "#CCF2F5F8",
                    "#FF000000",
                    "#CCFFFFFF"),
                (true, false) => (
                    "#FF1A1D24",
                    "#FF1E222A",
                    "#FF21262F",
                    "#FF282D36",
                    "#FF171B22",
                    "#FF1F242D",
                    "#FF262B34",
                    "#FF323843",
                    "#FF2A313B",
                    "#FF2A303B",
                    "#FF1C2129",
                    "#FFFFFFFF",
                    "#FF10141A"),
                _ => (
                    "#FFF2F4F7",
                    "#FFF5F6F8",
                    "#FFE8ECF1",
                    "#FFEEF1F4",
                    "#FFFDFEFF",
                    "#FFF0F3F6",
                    "#FFF1F3F6",
                    "#FFD5DBE4",
                    "#FFE0E4EB",
                    "#FFE5EAF0",
                    "#FFF2F5F8",
                    "#FF000000",
                    "#FFFFFFFF"),
            };

        SetBrushResource("ShellWindowBackground", window);
        SetBrushResource("ShellTopBarBrush", topBar);
        SetBrushResource("ShellRailBrush", rail);
        SetBrushResource("ShellAssetsBrush", assets);
        SetBrushResource("ShellWorkspaceBrush", workspace);
        SetBrushResource("ShellToolsBrush", tools);
        SetBrushResource("ShellPanelBrush", panel);
        SetBrushResource("ShellBorderBrush", border);
        SetBrushResource("ShellDividerBrush", divider);
        SetBrushResource("ShellTabActiveBrush", tabActive);
        SetBrushResource("ShellTabInactiveBrush", tabInactive);
        SetBrushResource("ShellTabSelectionBorderBrush", tabSelectionBorder);
        SetBrushResource("ShellTerminalBrush", terminal);
    }

    private void SetBrushResource(string key, string hexColor)
    {
        Resources[key] = new SolidColorBrush(Color.Parse(hexColor));
    }

    private static bool ResolveIsDarkTheme()
    {
        if (Application.Current is null)
        {
            return true;
        }

        var requested = Application.Current.RequestedThemeVariant;
        if (requested == Avalonia.Styling.ThemeVariant.Default)
        {
            return Application.Current.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;
        }

        return requested == Avalonia.Styling.ThemeVariant.Dark;
    }
}
