using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;
using SkylarkTerminal.Models;
using SkylarkTerminal.Services;
using SkylarkTerminal.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace SkylarkTerminal.Views;

public partial class MainWindow : Window
{
    private const double AssetsPanelAutoCollapseThreshold = 180d;
    private const double AssetsPanelSplitterWidth = 8d;
    private const double RightSidebarAutoCollapseThreshold = 220d;
    private const double RightSidebarSplitterWidth = 8d;
    private const int ContextMenuOverlaySuppressionWindowMs = 160;
    private const double AssetsSelectionDragActivationDistance = 4d;
    private MainWindowViewModel? _boundViewModel;
    private Grid? _mainContentGrid;
    private DateTimeOffset _lastContextMenuOpenedAt = DateTimeOffset.MinValue;
    private bool _isFlatSelectionDragPending;
    private bool _isFlatSelectionDragging;
    private bool _isFlatSelectionAdditive;
    private Point _flatSelectionDragStart;
    private List<AssetNode> _flatSelectionBaseline = [];

    public MainWindow()
    {
        InitializeComponent();
        ResolveMainContentGrid().LayoutUpdated += OnMainContentGridLayoutUpdated;
        AddHandler(PointerPressedEvent, OnWindowPointerPressed, RoutingStrategies.Tunnel);
        AddHandler(ContextRequestedEvent, OnWindowContextRequested, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
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

        RemoveHandler(PointerPressedEvent, OnWindowPointerPressed);
        RemoveHandler(ContextRequestedEvent, OnWindowContextRequested);

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
            RuntimeLogger.Info("tab-close", $"Close requested. tab_id={tab.Id}, header={tab.Header}");
            vm.CloseTabCommand.Execute(tab);
        }
    }

    private void OnWorkspaceTabContextActionClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        WorkspaceTabItemViewModel? tab = null;
        string? action = null;

        if (sender is MenuItem menuItem)
        {
            tab = menuItem.DataContext as WorkspaceTabItemViewModel;
            action = menuItem.Tag?.ToString();
        }
        else if (sender is MenuFlyoutItem flyoutItem)
        {
            tab = flyoutItem.DataContext as WorkspaceTabItemViewModel;
            action = flyoutItem.Tag?.ToString();
        }
        else
        {
            return;
        }

        switch (action)
        {
            case "duplicate":
                RuntimeLogger.Info("tab-context", $"Action duplicate. tab_id={tab?.Id ?? "<null>"}");
                vm.DuplicateTabCommand.Execute(tab);
                break;
            case "close":
                RuntimeLogger.Info("tab-context", $"Action close. tab_id={tab?.Id ?? "<null>"}");
                vm.CloseTabCommand.Execute(tab);
                break;
            case "close-others":
                RuntimeLogger.Info("tab-context", $"Action close-others. tab_id={tab?.Id ?? "<null>"}");
                vm.CloseOtherTabsCommand.Execute(tab);
                break;
            case "close-left":
                RuntimeLogger.Info("tab-context", $"Action close-left. tab_id={tab?.Id ?? "<null>"}");
                vm.CloseTabsToLeftCommand.Execute(tab);
                break;
            case "close-right":
                RuntimeLogger.Info("tab-context", $"Action close-right. tab_id={tab?.Id ?? "<null>"}");
                vm.CloseTabsToRightCommand.Execute(tab);
                break;
            case "close-all":
                RuntimeLogger.Info("tab-context", "Action close-all.");
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
        else if (e.PropertyName == nameof(MainWindowViewModel.IsAssetsSearchOpen) &&
                 _boundViewModel.IsAssetsSearchOpen)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var searchBox = this.FindControl<TextBox>("AssetsSearchTextBox");
                searchBox?.Focus();
                searchBox?.SelectAll();
            }, DispatcherPriority.Background);
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
        (var window, var topBar, var rail, var assetsToolbar, var assetsHeaderSeparator, var assets, var workspace, var tools, var panel, var border, var divider, var tabActive, var tabInactive, var tabSelectionBorder, var terminal) =
            (isDarkTheme, isTransparent) switch
            {
                (true, true) => (
                    "#1A10141A",
                    "#CC1E222A",
                    "#B821262F",
                    "#C4262B34",
                    "#A0485362",
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
                    "#CCECF0F4",
                    "#80C7D0DC",
                    "#CCEEF1F4",
                    "#D9FDFEFF",
                    "#CCF0F3F6",
                    "#CCF1F3F6",
                    "#80D5DBE4",
                    "#88E0E4EB",
                    "#CCE5EAF0",
                    "#CCF2F5F8",
                    "#FF000000",
                    "#CC10141A"),
                (true, false) => (
                    "#FF1A1D24",
                    "#FF1E222A",
                    "#FF21262F",
                    "#FF262B34",
                    "#FF3A4250",
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
                    "#FFEBEFF3",
                    "#FFD1D8E2",
                    "#FFEEF1F4",
                    "#FFFDFEFF",
                    "#FFF0F3F6",
                    "#FFF1F3F6",
                    "#FFD5DBE4",
                    "#FFE0E4EB",
                    "#FFE5EAF0",
                    "#FFF2F5F8",
                    "#FF000000",
                    "#FF10141A"),
            };

        SetBrushResource("ShellWindowBackground", window);
        SetBrushResource("ShellTopBarBrush", topBar);
        SetBrushResource("ShellRailBrush", rail);
        SetBrushResource("ShellAssetsToolbarBrush", assetsToolbar);
        SetBrushResource("ShellAssetsHeaderSeparatorBrush", assetsHeaderSeparator);
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
        RuntimeLogger.Info(
            "shell-palette",
            $"Applied palette. dark={isDarkTheme}, transparent={isTransparent}, terminal={terminal}");
    }

    private void SetBrushResource(string key, string hexColor)
    {
        Resources[key] = new SolidColorBrush(Color.Parse(hexColor));
    }

    private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_boundViewModel is null)
        {
            return;
        }

        var sourceElement = e.Source as StyledElement;
        var searchBox = this.FindControl<TextBox>("AssetsSearchTextBox");
        var searchToggleButton = this.FindControl<Button>("AssetsSearchToggleButton");
        var pointProperties = e.GetCurrentPoint(this).Properties;
        if (pointProperties.IsRightButtonPressed)
        {
            if (sourceElement?.GetType().Name == "LightDismissOverlayLayer")
            {
                var elapsed = DateTimeOffset.UtcNow - _lastContextMenuOpenedAt;
                if (elapsed.TotalMilliseconds >= 0d &&
                    elapsed.TotalMilliseconds <= ContextMenuOverlaySuppressionWindowMs)
                {
                    e.Handled = true;
                    return;
                }
            }
        }

        var shouldCloseSearch = MainWindowInteractionPolicy.ShouldCloseAssetsSearchOnPointerPressed(
            isAssetsSearchOpen: _boundViewModel.IsAssetsSearchOpen,
            assetsSearchText: _boundViewModel.AssetsSearchText,
            isLeftButtonPressed: pointProperties.IsLeftButtonPressed,
            isPointerInsideSearchBox: IsSourceWithin(searchBox, sourceElement),
            isPointerInsideSearchToggleButton: IsSourceWithin(searchToggleButton, sourceElement));

        if (!shouldCloseSearch)
        {
            return;
        }

        _boundViewModel.CloseAssetsSearchIfEmptyCommand.Execute(null);
    }

    private void OnWindowContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        var sourceElement = e.Source as StyledElement;
        var hostControl = TryResolveContextHost(sourceElement);
        var hasContextMenu = hostControl?.ContextMenu is not null;
        var hasContextFlyout = hostControl?.ContextFlyout is not null;
        if (hasContextMenu || hasContextFlyout)
        {
            _lastContextMenuOpenedAt = DateTimeOffset.UtcNow;
        }
    }

    private static bool IsSourceWithin(Control? control, StyledElement? source)
    {
        if (control is null || source is null)
        {
            return false;
        }

        var current = source;
        while (current is not null)
        {
            if (ReferenceEquals(current, control))
            {
                return true;
            }

            current = current.Parent as StyledElement;
        }

        return false;
    }

    private void OnAssetsSearchBoxLostFocus(object? sender, RoutedEventArgs e)
    {
        _boundViewModel?.CloseAssetsSearchIfEmptyCommand.Execute(null);
    }

    private static Control? TryResolveContextHost(StyledElement? source)
    {
        var current = source;
        while (current is not null)
        {
            if (current is Control control &&
                (control.ContextMenu is not null || control.ContextFlyout is not null))
            {
                return control;
            }

            current = current.Parent as StyledElement;
        }

        return null;
    }

    private void OnFlatAssetSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _ = e;
        if (_boundViewModel is null ||
            sender is not ListBox listBox ||
            !listBox.IsVisible)
        {
            return;
        }

        var selected = listBox.SelectedItems?.OfType<AssetNode>() ?? [];
        _boundViewModel.SetSelectedAssets(selected);
    }

    private void OnAssetsFlatListPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_boundViewModel is null ||
            !_boundViewModel.IsFlatViewMode ||
            sender is not ListBox listBox)
        {
            return;
        }

        var pointProperties = e.GetCurrentPoint(listBox).Properties;
        if (!pointProperties.IsLeftButtonPressed)
        {
            return;
        }

        if (e.Source is StyledElement sourceElement &&
            IsSourceWithinListBoxItem(sourceElement))
        {
            return;
        }

        _flatSelectionDragStart = ClampPointToControlBounds(e.GetPosition(listBox), listBox);
        _isFlatSelectionDragPending = true;
        _isFlatSelectionDragging = false;
        _isFlatSelectionAdditive = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        _flatSelectionBaseline = _isFlatSelectionAdditive
            ? listBox.SelectedItems?.OfType<AssetNode>().Distinct().ToList() ?? []
            : [];
        e.Pointer.Capture(listBox);
    }

    private void OnAssetsFlatListPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_boundViewModel is null ||
            !_boundViewModel.IsFlatViewMode ||
            sender is not ListBox listBox ||
            (!_isFlatSelectionDragPending && !_isFlatSelectionDragging))
        {
            return;
        }

        var pointProperties = e.GetCurrentPoint(listBox).Properties;
        if (!pointProperties.IsLeftButtonPressed)
        {
            StopFlatSelectionDrag(listBox, e.Pointer);
            return;
        }

        var currentPoint = ClampPointToControlBounds(e.GetPosition(listBox), listBox);
        if (_isFlatSelectionDragPending)
        {
            var dragDistance = Math.Abs(currentPoint.X - _flatSelectionDragStart.X) +
                               Math.Abs(currentPoint.Y - _flatSelectionDragStart.Y);
            if (dragDistance < AssetsSelectionDragActivationDistance)
            {
                return;
            }

            _isFlatSelectionDragPending = false;
            _isFlatSelectionDragging = true;
            if (!_isFlatSelectionAdditive && listBox.SelectedItems is not null)
            {
                listBox.SelectedItems.Clear();
            }
        }

        if (!_isFlatSelectionDragging)
        {
            return;
        }

        var selectionRect = BuildNormalizedRect(_flatSelectionDragStart, currentPoint);
        UpdateAssetsSelectionMarquee(selectionRect);

        var hitNodes = ResolveNodesInsideSelectionRect(listBox, selectionRect);
        var targetNodes = _isFlatSelectionAdditive
            ? _flatSelectionBaseline.Concat(hitNodes).Distinct().ToList()
            : hitNodes;
        ApplyFlatSelection(listBox, targetNodes);
        e.Handled = true;
    }

    private void OnAssetsFlatListPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is not ListBox listBox ||
            (!_isFlatSelectionDragPending && !_isFlatSelectionDragging))
        {
            return;
        }

        var wasDragging = _isFlatSelectionDragging;
        StopFlatSelectionDrag(listBox, e.Pointer);
        e.Handled = wasDragging;
    }

    private void OnAssetListItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_boundViewModel is null ||
            sender is not Border border ||
            border.DataContext is not AssetNode assetNode ||
            !_boundViewModel.IsFlatViewMode ||
            !e.GetCurrentPoint(border).Properties.IsRightButtonPressed)
        {
            return;
        }

        var listBox = this.FindControl<ListBox>("AssetsFlatListBox");
        if (listBox?.SelectedItems is null)
        {
            return;
        }

        if (listBox.SelectedItems.Contains(assetNode))
        {
            return;
        }

        listBox.SelectedItems.Clear();
        listBox.SelectedItems.Add(assetNode);
        _boundViewModel.SetSelectedAssets(listBox.SelectedItems.OfType<AssetNode>());
    }

    private void OnAssetNodeDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_boundViewModel is null ||
            sender is not Control control ||
            control.DataContext is not ConnectionNode connectionNode)
        {
            return;
        }

        if (_boundViewModel.OpenAssetInNewTabCommand.CanExecute(connectionNode))
        {
            RuntimeLogger.Info(
                "asset-doubletap",
                $"Open tab requested by double tap. id={connectionNode.Id}, name={connectionNode.Name}, host={connectionNode.Host}, port={connectionNode.Port}");
            _boundViewModel.OpenAssetInNewTabCommand.Execute(connectionNode);
            e.Handled = true;
        }
        else
        {
            RuntimeLogger.Warn(
                "asset-doubletap",
                $"Open tab command rejected. id={connectionNode.Id}, name={connectionNode.Name}");
        }
    }

    private void StopFlatSelectionDrag(ListBox listBox, IPointer? pointer)
    {
        if (pointer is not null)
        {
            pointer.Capture(null);
        }

        _isFlatSelectionDragPending = false;
        _isFlatSelectionDragging = false;
        _isFlatSelectionAdditive = false;
        _flatSelectionBaseline = [];
        HideAssetsSelectionMarquee();
    }

    private static bool IsSourceWithinListBoxItem(StyledElement source)
    {
        var current = source;
        while (current is not null)
        {
            if (current is ListBoxItem)
            {
                return true;
            }

            current = current.Parent as StyledElement;
        }

        return false;
    }

    private static Point ClampPointToControlBounds(Point source, Control control)
    {
        return new Point(
            Math.Clamp(source.X, 0d, control.Bounds.Width),
            Math.Clamp(source.Y, 0d, control.Bounds.Height));
    }

    private static Rect BuildNormalizedRect(Point start, Point end)
    {
        var left = Math.Min(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);
        var right = Math.Max(start.X, end.X);
        var bottom = Math.Max(start.Y, end.Y);
        return new Rect(left, top, Math.Max(1d, right - left), Math.Max(1d, bottom - top));
    }

    private IReadOnlyList<AssetNode> ResolveNodesInsideSelectionRect(ListBox listBox, Rect selectionRect)
    {
        var nodes = new List<AssetNode>();
        foreach (var item in listBox.GetVisualDescendants().OfType<ListBoxItem>())
        {
            if (item.DataContext is not AssetNode node || !item.IsVisible)
            {
                continue;
            }

            var topLeft = item.TranslatePoint(new Point(0d, 0d), listBox);
            if (!topLeft.HasValue)
            {
                continue;
            }

            var itemRect = new Rect(topLeft.Value, item.Bounds.Size);
            if (selectionRect.Intersects(itemRect))
            {
                nodes.Add(node);
            }
        }

        return nodes;
    }

    private void ApplyFlatSelection(ListBox listBox, IReadOnlyList<AssetNode> targetNodes)
    {
        if (listBox.SelectedItems is null)
        {
            return;
        }

        listBox.SelectedItems.Clear();
        foreach (var node in targetNodes)
        {
            listBox.SelectedItems.Add(node);
        }

        _boundViewModel?.SetSelectedAssets(listBox.SelectedItems.OfType<AssetNode>());
    }

    private void UpdateAssetsSelectionMarquee(Rect selectionRect)
    {
        var marquee = this.FindControl<Border>("AssetsSelectionMarquee");
        if (marquee is null)
        {
            return;
        }

        marquee.IsVisible = true;
        marquee.Width = selectionRect.Width;
        marquee.Height = selectionRect.Height;
        Canvas.SetLeft(marquee, selectionRect.X);
        Canvas.SetTop(marquee, selectionRect.Y);
    }

    private void HideAssetsSelectionMarquee()
    {
        var marquee = this.FindControl<Border>("AssetsSelectionMarquee");
        if (marquee is null)
        {
            return;
        }

        marquee.IsVisible = false;
        marquee.Width = 0d;
        marquee.Height = 0d;
        Canvas.SetLeft(marquee, 0d);
        Canvas.SetTop(marquee, 0d);
    }

    private void OnAssetRenameTextBoxAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _ = e;
        if (sender is not TextBox textBox ||
            textBox.DataContext is not AssetNode assetNode ||
            !assetNode.IsRenaming)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            textBox.Focus();
            textBox.SelectAll();
        }, DispatcherPriority.Background);
    }

    private void OnAssetRenameTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (_boundViewModel is null ||
            sender is not TextBox textBox ||
            textBox.DataContext is not AssetNode assetNode)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            _boundViewModel.CommitRenameAssetCommand.Execute(assetNode);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            _boundViewModel.CancelRenameAssetCommand.Execute(assetNode);
            e.Handled = true;
        }
    }

    private void OnAssetRenameTextBoxLostFocus(object? sender, RoutedEventArgs e)
    {
        _ = e;
        if (_boundViewModel is null ||
            sender is not TextBox textBox ||
            textBox.DataContext is not AssetNode assetNode ||
            !assetNode.IsRenaming)
        {
            return;
        }

        _boundViewModel.CommitRenameAssetCommand.Execute(assetNode);
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
