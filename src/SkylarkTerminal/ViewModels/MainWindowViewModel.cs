using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkylarkTerminal.Models;
using SkylarkTerminal.ViewModels.RightPanelModes;
using SkylarkTerminal.Services;
using SkylarkTerminal.Services.Mock;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace SkylarkTerminal.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private enum AssetClipboardMode
    {
        None,
        Copy,
        Cut,
    }

    public const double ExpandedLeftAssetsPaneWidth = 300d;
    public const double ExpandedRightSidebarWidth = 340d;
    public const double DefaultAssetsPanelWidth = 320d;
    public const int MaxQuickStartRecentConnections = 10;
    public const int DefaultMaxWorkspacePaneCount = 8;
    public const double DefaultWorkspaceMinPaneSize = 340d;

    private static readonly Color[] WorkspaceAccentPalette =
    [
        Color.Parse("#0F6CBD"),
        Color.Parse("#2E7D32"),
        Color.Parse("#9C27B0"),
        Color.Parse("#C77800"),
        Color.Parse("#0B8F8F"),
    ];

    private static readonly IRightPanelModeViewModel FallbackRightPanelMode = new SnippetsModeViewModel();

    private readonly ISshConnectionService _sshConnectionService;
    private readonly ISessionRegistryService _sessionRegistryService;
    private readonly IWorkspaceLayoutService _workspaceLayoutService;
    private readonly IDragSessionService _dragSessionService;
    private readonly ISftpService _sftpService;
    private readonly ISftpNavigationService _sftpNavigationService;
    private readonly IAppDialogService _appDialogService;
    private readonly IClipboardService _clipboardService;
    private readonly Dictionary<AssetsPaneKind, ObservableCollection<AssetNode>> _assetsByPane;
    private int _newFolderSeed = 1;
    private int _newConnectionSeed = 1;
    private int _newWorkspaceTabSeed = 1;
    private AssetNode? _assetClipboardNode;
    private AssetClipboardMode _assetClipboardMode;
    private CancellationTokenSource? _quickLocateHighlightCts;

    public MainWindowViewModel()
        : this(
            new MockAssetCatalogService(),
            new MockSshConnectionService(),
            new MockSftpService(),
            new MockAppDialogService(),
            new MockClipboardService())
    {
    }

    public MainWindowViewModel(
        IAssetCatalogService assetCatalogService,
        ISshConnectionService sshConnectionService,
        ISftpService sftpService,
        IAppDialogService appDialogService)
        : this(
            assetCatalogService,
            sshConnectionService,
            sftpService,
            appDialogService,
            new MockClipboardService())
    {
    }

    public MainWindowViewModel(
        IAssetCatalogService assetCatalogService,
        ISshConnectionService sshConnectionService,
        ISftpService sftpService,
        IAppDialogService appDialogService,
        IClipboardService clipboardService)
        : this(
            assetCatalogService,
            sshConnectionService,
            sftpService,
            appDialogService,
            clipboardService,
            new SessionRegistryService(sshConnectionService),
            new WorkspaceLayoutService(),
            new DragSessionService())
    {
    }

    public MainWindowViewModel(
        IAssetCatalogService assetCatalogService,
        ISshConnectionService sshConnectionService,
        ISftpService sftpService,
        IAppDialogService appDialogService,
        IClipboardService clipboardService,
        ISessionRegistryService sessionRegistryService)
        : this(
            assetCatalogService,
            sshConnectionService,
            sftpService,
            appDialogService,
            clipboardService,
            sessionRegistryService,
            new WorkspaceLayoutService(),
            new DragSessionService())
    {
    }

    public MainWindowViewModel(
        IAssetCatalogService assetCatalogService,
        ISshConnectionService sshConnectionService,
        ISftpService sftpService,
        IAppDialogService appDialogService,
        IClipboardService clipboardService,
        ISessionRegistryService sessionRegistryService,
        IWorkspaceLayoutService workspaceLayoutService)
        : this(
            assetCatalogService,
            sshConnectionService,
            sftpService,
            appDialogService,
            clipboardService,
            sessionRegistryService,
            workspaceLayoutService,
            new DragSessionService())
    {
    }

    public MainWindowViewModel(
        IAssetCatalogService assetCatalogService,
        ISshConnectionService sshConnectionService,
        ISftpService sftpService,
        IAppDialogService appDialogService,
        IClipboardService clipboardService,
        ISessionRegistryService sessionRegistryService,
        IWorkspaceLayoutService workspaceLayoutService,
        IDragSessionService dragSessionService,
        ISftpNavigationService? sftpNavigationService = null)
    {
        _sshConnectionService = sshConnectionService;
        _sessionRegistryService = sessionRegistryService;
        _workspaceLayoutService = workspaceLayoutService;
        _dragSessionService = dragSessionService;
        _sftpService = sftpService;
        _sftpNavigationService = sftpNavigationService ?? new SftpNavigationService("/");
        _appDialogService = appDialogService;
        _clipboardService = clipboardService;
        _assetsByPane = BuildAssetsMap(assetCatalogService.GetAssets());
        ApplyAssetsSearchFilter();
        RebuildCurrentFlatList();
        InitializeWorkspacePanes();
        InitializeWorkspaceTabs();
        InitializeQuickStartRecentConnections();
        InitializeRightToolsData();
        InitializeRightPanelModes();
        SelectedRightToolsModeItem = RightToolsModeItems.FirstOrDefault(item => item.Kind == SelectedRightToolsView);
        SyncActiveWorkspaceTabs();
        TopStatusBar = new TopStatusBarViewModel(this);
        RuntimeLogger.Info(
            "vm-init",
            $"MainWindowViewModel initialized. ssh_service={_sshConnectionService.GetType().Name}, tabs={WorkspaceTabs.Count}, selected={SelectedWorkspaceTab?.Header ?? "<null>"}");
    }

    public string WindowTitle { get; } = "Skylark Terminal";

    public string SearchPlaceholder { get; } = "搜索主机、命令或会话";

    public TopStatusBarViewModel TopStatusBar { get; }

    [ObservableProperty]
    private bool isAssetsPanelVisible = true;

    [ObservableProperty]
    private bool isRightSidebarVisible;

    [ObservableProperty]
    private AssetsPaneKind selectedAssetsPane = AssetsPaneKind.Hosts;

    [ObservableProperty]
    private AssetsViewMode assetsViewMode = AssetsViewMode.Tree;

    [ObservableProperty]
    private AssetNode? selectedAssetNode;

    [ObservableProperty]
    private string lastAssetActionMessage = "Ready";

    [ObservableProperty]
    private WorkspaceTabItemViewModel? selectedWorkspaceTab;

    [ObservableProperty]
    private WorkspacePaneViewModel? selectedWorkspacePane;

    [ObservableProperty]
    private RightToolsViewKind selectedRightToolsView = RightToolsViewKind.Snippets;

    [ObservableProperty]
    private RightToolsModeItem? selectedRightToolsModeItem;

    [ObservableProperty]
    private string currentLanguageCode = "zh-CN";

    [ObservableProperty]
    private bool isShellTransparent;

    [ObservableProperty]
    private string assetsSearchText = string.Empty;

    [ObservableProperty]
    private bool isAssetsSearchOpen;

    [ObservableProperty]
    private bool areAllTreeFoldersExpanded;

    [ObservableProperty]
    private string quickStartSearchText = string.Empty;

    [ObservableProperty]
    private ConnectionNode? pendingQuickStartLocateTarget;

    [ObservableProperty]
    private bool isWorkspaceDragOverlayVisible;

    [ObservableProperty]
    private string? workspaceDragHoverPaneId;

    [ObservableProperty]
    private WorkspaceDropDirection? workspaceDragHoverDirection;

    public double LeftAssetsPaneWidth => IsAssetsPanelVisible ? ExpandedLeftAssetsPaneWidth : 0d;

    // Backward-compatible alias for existing bindings/tests during transition.
    public bool IsLeftAssetsPaneOpen
    {
        get => IsAssetsPanelVisible;
        set => IsAssetsPanelVisible = value;
    }

    public bool IsTreeViewMode => AssetsViewMode == AssetsViewMode.Tree;

    public bool IsFlatViewMode => AssetsViewMode == AssetsViewMode.Flat;

    public string AssetsViewModeGlyph => IsTreeViewMode ? "\uE8D2" : "\uE8FD";

    public string AssetsViewModeToolTip => IsTreeViewMode
        ? "当前为树形视图，点击切换为列表视图"
        : "当前为列表视图，点击切换为树形视图";

    public string AssetsPanelTitle => SelectedAssetsPane switch
    {
        AssetsPaneKind.Hosts => "Hosts Assets",
        AssetsPaneKind.Sftp => "SFTP Assets",
        AssetsPaneKind.Keys => "Keys Assets",
        AssetsPaneKind.Tools => "Tools Assets",
        _ => "Assets",
    };

    public string ThemeModeLabel => IsDarkTheme ? "Dark" : "Light";

    public string ThemeIconGlyph => IsDarkTheme ? "\uE708" : "\uE706";

    public string ThemeToggleToolTip => IsDarkTheme
        ? "当前为深色主题，点击切换为浅色主题"
        : "当前为浅色主题，点击切换为深色主题";

    public string CurrentLanguageLabel => CurrentLanguageCode == "zh-CN" ? "中文" : "English";

    public string LeftAssetsPaneToggleGlyph => IsAssetsPanelVisible ? "\uE76B" : "\uE76C";

    public string LeftAssetsPaneToggleToolTip => IsAssetsPanelVisible
        ? "收起资产列"
        : "展开资产列";

    public string FlatConnectionsToolTip => "平铺连接视图";

    public string TreeConnectionsToolTip => "树形连接视图";

    public string AssetsSearchPlaceholder => "搜索资产";

    public string QuickStartSearchPlaceholder => "搜索最近连接";

    public string AssetsExpandCollapseGlyph => AreAllTreeFoldersExpanded ? "\uE70D" : "\uE70E";

    public string AssetsExpandCollapseToolTip => AreAllTreeFoldersExpanded
        ? "收起全部"
        : "展开全部";

    public bool HasAssetClipboardData => _assetClipboardNode is not null && _assetClipboardMode != AssetClipboardMode.None;

    public bool IsSnippetsView => SelectedRightToolsView == RightToolsViewKind.Snippets;

    public bool IsHistoryView => SelectedRightToolsView == RightToolsViewKind.History;

    public bool IsSftpView => SelectedRightToolsView == RightToolsViewKind.Sftp;

    public bool HasQuickStartRecentConnections => FilteredQuickStartRecentConnections.Count > 0;

    public bool IsQuickStartRecentConnectionsEmpty => !HasQuickStartRecentConnections;

    public bool IsWorkspacePaneHoverActive => IsWorkspaceDragOverlayVisible && !string.IsNullOrWhiteSpace(WorkspaceDragHoverPaneId);

    public bool IsWorkspaceDropSlotLeftHot => WorkspaceDragHoverDirection == WorkspaceDropDirection.Left;

    public bool IsWorkspaceDropSlotRightHot => WorkspaceDragHoverDirection == WorkspaceDropDirection.Right;

    public bool IsWorkspaceDropSlotTopHot => WorkspaceDragHoverDirection == WorkspaceDropDirection.Top;

    public bool IsWorkspaceDropSlotBottomHot => WorkspaceDragHoverDirection == WorkspaceDropDirection.Bottom;

    public bool HasWorkspaceDragSession => _dragSessionService.IsActive;

    public int MaxWorkspacePaneCount => DefaultMaxWorkspacePaneCount;

    public double WorkspaceMinPaneSize => DefaultWorkspaceMinPaneSize;

    public ObservableCollection<AssetNode> CurrentAssetFlatList { get; } = [];

    public ObservableCollection<AssetNode> SelectedAssetNodes { get; } = [];

    public ObservableCollection<AssetNode> CurrentAssetTree => _assetsByPane[SelectedAssetsPane];

    public ObservableCollection<QuickStartRecentConnection> QuickStartRecentConnections { get; } = [];

    public ObservableCollection<QuickStartRecentConnection> FilteredQuickStartRecentConnections { get; } = [];

    public ObservableCollection<WorkspacePaneViewModel> WorkspacePanes { get; } = [];

    public ObservableCollection<RightToolsModeItem> RightToolsModeItems { get; } =
    [
        new(RightToolsViewKind.Snippets, "Snippets", "\uE8D2"),
        new(RightToolsViewKind.History, "History", "\uE81C"),
        new(RightToolsViewKind.Sftp, "SFTP", "\uE8B7"),
    ];

    public ObservableCollection<IRightPanelModeViewModel> RightPanelModes { get; } = [];

    public IRightPanelModeViewModel ActiveRightMode => ResolveActiveRightMode(SelectedRightToolsView);

    public RightPanelHeaderNode ActiveRightHeader => ActiveRightMode.HeaderNode;

    public WorkspaceLayoutNode WorkspaceLayoutRoot => _workspaceLayoutService.Root;

    public ObservableCollection<WorkspaceTabItemViewModel> WorkspaceTabs => ResolveActiveWorkspacePane().Tabs;

    public ObservableCollection<WorkspaceTabItemViewModel> Tabs => WorkspaceTabs;

    public WorkspaceTabItemViewModel? SelectedTab
    {
        get => SelectedWorkspaceTab;
        set => SelectedWorkspaceTab = value;
    }

    public ObservableCollection<CommandSnippet> SnippetItems { get; } = [];

    public ObservableCollection<CommandHistoryEntry> HistoryItems { get; } = [];

    public ObservableCollection<RemoteFileNode> SftpItems { get; } = [];

    public RightToolsContentNode CurrentRightToolsContent => SelectedRightToolsView switch
    {
        RightToolsViewKind.Snippets => new SnippetsRightToolsContent(),
        RightToolsViewKind.History => new HistoryRightToolsContent(),
        _ => new SftpRightToolsContent(),
    };

    public bool IsKeysAssetsPane => SelectedAssetsPane == AssetsPaneKind.Keys;

    public bool IsGeneralAssetsPane => !IsKeysAssetsPane;

    [RelayCommand]
    private void ToggleTheme()
    {
        if (Application.Current is null)
        {
            return;
        }

        Application.Current.RequestedThemeVariant = IsDarkTheme ? ThemeVariant.Light : ThemeVariant.Dark;
        OnPropertyChanged(nameof(ThemeModeLabel));
        OnPropertyChanged(nameof(ThemeIconGlyph));
        OnPropertyChanged(nameof(ThemeToggleToolTip));
    }

    [RelayCommand]
    private void ToggleAssetsPanel()
    {
        IsAssetsPanelVisible = !IsAssetsPanelVisible;
    }

    [RelayCommand]
    private void ToggleLeftAssetsPane()
    {
        ToggleAssetsPanel();
    }

    [RelayCommand]
    private void ToggleRightSidebar()
    {
        IsRightSidebarVisible = !IsRightSidebarVisible;
    }

    [RelayCommand]
    private void ShowSnippetsTools()
    {
        IsRightSidebarVisible = true;
        SelectedRightToolsView = RightToolsViewKind.Snippets;
    }

    [RelayCommand]
    private void ShowHistoryTools()
    {
        IsRightSidebarVisible = true;
        SelectedRightToolsView = RightToolsViewKind.History;
    }

    [RelayCommand]
    private async Task ShowSftpTools()
    {
        IsRightSidebarVisible = true;
        SelectedRightToolsView = RightToolsViewKind.Sftp;
        SftpItems.Clear();

        var files = await _sftpService.ListDirectoryAsync("mock-conn-01", "/");
        foreach (var file in files)
        {
            SftpItems.Add(file);
        }
    }

    [RelayCommand]
    private void CreateSnippet()
    {
    }

    [RelayCommand]
    private void SearchSnippet()
    {
    }

    [RelayCommand]
    private void SortSnippet()
    {
    }

    [RelayCommand]
    private void ToggleSnippetLayout()
    {
    }

    [RelayCommand]
    private void SearchHistory()
    {
    }

    [RelayCommand]
    private void SortHistory()
    {
    }

    [RelayCommand]
    private void ToggleHistoryLayout()
    {
    }

    [RelayCommand]
    private void ClearHistory()
    {
    }

    [RelayCommand]
    private void NavigateSftpBack()
    {
    }

    [RelayCommand]
    private void NavigateSftpForward()
    {
    }

    [RelayCommand]
    private void RefreshSftp()
    {
    }

    [RelayCommand]
    private void NavigateSftpUp()
    {
    }

    [RelayCommand]
    private void CommitSftpAddress()
    {
    }

    [RelayCommand]
    private async Task OpenSettings()
    {
        var selectedTransparency = await _appDialogService.ShowSettingsAsync(
            ThemeModeLabel,
            IsAssetsPanelVisible,
            IsRightSidebarVisible,
            IsShellTransparent);

        if (!selectedTransparency.HasValue || selectedTransparency.Value == IsShellTransparent)
        {
            return;
        }

        IsShellTransparent = selectedTransparency.Value;
        LastAssetActionMessage = IsShellTransparent
            ? "窗口已切换为透明材质"
            : "窗口已切换为不透明样式";
    }

    [RelayCommand]
    private async Task OpenLanguage()
    {
        var selectedLanguage = await _appDialogService.ShowLanguagePickerAsync(CurrentLanguageCode);
        if (string.IsNullOrWhiteSpace(selectedLanguage) ||
            string.Equals(selectedLanguage, CurrentLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        CurrentLanguageCode = selectedLanguage;
        LastAssetActionMessage = CurrentLanguageCode == "zh-CN"
            ? "已切换为中文界面"
            : "Switched to English UI";
    }

    [RelayCommand]
    private async Task OpenHelp()
    {
        await _appDialogService.ShowHelpAsync(CurrentLanguageCode);
    }

    [RelayCommand]
    private async Task OpenAbout()
    {
        await _appDialogService.ShowAboutAsync(WindowTitle, GetAppVersion());
    }

    [RelayCommand]
    private void ShowHostsAssets()
    {
        ActivateHostsAssetsPane();
    }

    [RelayCommand]
    private void ShowSftpAssets()
    {
        SelectedAssetsPane = AssetsPaneKind.Sftp;
    }

    [RelayCommand]
    private void ShowKeysAssets()
    {
        SelectedAssetsPane = AssetsPaneKind.Keys;
    }

    [RelayCommand]
    private void ShowToolsAssets()
    {
        SelectedAssetsPane = AssetsPaneKind.Tools;
    }

    [RelayCommand]
    private void SetTreeConnectionsMode()
    {
        AssetsViewMode = AssetsViewMode.Tree;
    }

    [RelayCommand]
    private void SetFlatConnectionsMode()
    {
        AssetsViewMode = AssetsViewMode.Flat;
    }

    [RelayCommand]
    private void SetTreeViewMode()
    {
        SetTreeConnectionsMode();
    }

    [RelayCommand]
    private void SetListViewMode()
    {
        SetFlatConnectionsMode();
    }

    [RelayCommand]
    private void ToggleAssetsViewMode()
    {
        AssetsViewMode = AssetsViewMode == AssetsViewMode.Tree
            ? AssetsViewMode.Flat
            : AssetsViewMode.Tree;
    }

    [RelayCommand]
    private void ToggleAssetsSearch()
    {
        if (IsAssetsSearchOpen)
        {
            if (string.IsNullOrWhiteSpace(AssetsSearchText))
            {
                IsAssetsSearchOpen = false;
            }

            return;
        }

        IsAssetsSearchOpen = true;
    }

    [RelayCommand]
    private void CloseAssetsSearchIfEmpty()
    {
        if (!string.IsNullOrWhiteSpace(AssetsSearchText))
        {
            return;
        }

        IsAssetsSearchOpen = false;
    }

    [RelayCommand]
    private void ToggleAssetsExpandCollapse()
    {
        if (!IsTreeViewMode)
        {
            return;
        }

        var shouldExpand = !AreAllTreeFoldersExpanded;
        SetFolderExpansion(CurrentAssetTree, shouldExpand);
        AreAllTreeFoldersExpanded = shouldExpand;
        LastAssetActionMessage = shouldExpand
            ? "已展开全部节点"
            : "已收起全部节点";
    }

    [RelayCommand]
    private void ExpandAllAssets()
    {
        if (!IsTreeViewMode)
        {
            return;
        }

        SetFolderExpansion(CurrentAssetTree, true);
        AreAllTreeFoldersExpanded = true;
        LastAssetActionMessage = "已展开全部节点";
    }

    [RelayCommand]
    private void CollapseAllAssets()
    {
        if (!IsTreeViewMode)
        {
            return;
        }

        SetFolderExpansion(CurrentAssetTree, false);
        AreAllTreeFoldersExpanded = false;
        LastAssetActionMessage = "已收起全部节点";
    }

    [RelayCommand]
    private void CreateFolderAsset()
    {
        CreateFolderCore(null);
    }

    [RelayCommand]
    private void CreateSshConnectionAsset()
    {
        CreateSshConnectionCore(null);
    }

    [RelayCommand]
    private void CreateFolderFromContext(AssetNode? sourceNode)
    {
        CreateFolderCore(sourceNode);
    }

    [RelayCommand]
    private void CreateConnectionFromContext(AssetNode? sourceNode)
    {
        CreateSshConnectionCore(sourceNode);
    }

    [RelayCommand]
    private void CreatePasswordAsset(AssetNode? sourceNode)
    {
        CreateCredentialAssetCore(sourceNode, "Password");
    }

    [RelayCommand]
    private void CreateKeyFileAsset(AssetNode? sourceNode)
    {
        CreateCredentialAssetCore(sourceNode, "Key File");
    }

    [RelayCommand]
    private void ImportAssets(AssetNode? sourceNode)
    {
        if (IsKeysAssetsPane)
        {
            CreateCredentialAssetCore(sourceNode, "Imported Key");
            LastAssetActionMessage = "已导入密钥资产";
        }
        else
        {
            CreateSshConnectionCore(sourceNode);
            LastAssetActionMessage = "已导入连接资产";
        }
    }

    [RelayCommand(CanExecute = nameof(CanExportAssets))]
    private void ExportAssets(AssetNode? sourceNode)
    {
        var targets = ResolveAssetOperationTargets(sourceNode).ToList();
        if (targets.Count == 0)
        {
            LastAssetActionMessage = "没有可导出的资产";
            return;
        }

        LastAssetActionMessage = $"已导出 {targets.Count} 项资产";
    }

    [RelayCommand(CanExecute = nameof(CanOpenAssetInNewTab))]
    private void OpenAssetInNewTab(AssetNode? sourceNode)
    {
        var connections = ResolveAssetOperationTargets(sourceNode)
            .OfType<ConnectionNode>()
            .ToList();
        if (connections.Count == 0)
        {
            LastAssetActionMessage = "没有可打开的连接资产";
            RuntimeLogger.Warn("open-tab", "No connection node can be opened.");
            return;
        }

        RuntimeLogger.Info("open-tab", $"Opening terminal tabs. count={connections.Count}");
        foreach (var connection in connections)
        {
            var newTab = BuildWorkspaceTab(connection);
            WorkspaceTabs.Add(newTab);
            SelectedWorkspaceTab = newTab;
            RegisterRecentConnection(connection);
            RuntimeLogger.Info(
                "open-tab",
                $"Tab created. tab_id={newTab.Id}, header={newTab.Header}, host={connection.Host}, port={connection.Port}");
        }

        LastAssetActionMessage = $"已打开 {connections.Count} 个会话标签";
    }

    [RelayCommand]
    private void CopyAsset(AssetNode? sourceNode)
    {
        var targetNode = ResolveTargetNode(sourceNode);
        if (targetNode is null)
        {
            return;
        }

        _assetClipboardNode = targetNode;
        _assetClipboardMode = AssetClipboardMode.Copy;
        NotifyAssetCommandStateChanged();
        LastAssetActionMessage = $"Copied {targetNode.Name}";
    }

    [RelayCommand]
    private void CutAsset(AssetNode? sourceNode)
    {
        var targetNode = ResolveTargetNode(sourceNode);
        if (targetNode is null)
        {
            return;
        }

        _assetClipboardNode = targetNode;
        _assetClipboardMode = AssetClipboardMode.Cut;
        NotifyAssetCommandStateChanged();
        LastAssetActionMessage = $"Cut {targetNode.Name}";
    }

    [RelayCommand(CanExecute = nameof(CanPasteAsset))]
    private void PasteAsset(AssetNode? sourceNode)
    {
        if (_assetClipboardNode is null || _assetClipboardMode == AssetClipboardMode.None)
        {
            LastAssetActionMessage = "Clipboard is empty";
            return;
        }

        var destinationParent = ResolvePasteParent(sourceNode);

        if (_assetClipboardMode == AssetClipboardMode.Cut)
        {
            if (destinationParent is not null && ContainsNode(_assetClipboardNode, destinationParent))
            {
                LastAssetActionMessage = "Cannot move a folder into itself";
                return;
            }

            if (!TryRemoveNode(CurrentAssetTree, _assetClipboardNode))
            {
                LastAssetActionMessage = "Cut source no longer exists";
                ClearAssetClipboard();
                return;
            }

            InsertNode(destinationParent, _assetClipboardNode);
            LastAssetActionMessage = $"Moved {_assetClipboardNode.Name}";
            ClearAssetClipboard();
            return;
        }

        var occupiedNames = BuildNameRegistry();
        var clonedNode = CloneNode(_assetClipboardNode, occupiedNames);
        InsertNode(destinationParent, clonedNode);
        LastAssetActionMessage = $"Pasted {clonedNode.Name}";
    }

    [RelayCommand]
    private void RefreshAssets(AssetNode? sourceNode)
    {
        _ = sourceNode;
        ApplyAssetsSearchFilter();
        LastAssetActionMessage = "Assets refreshed";
    }

    [RelayCommand]
    private void CloneAsset(AssetNode? sourceNode)
    {
        var targetNode = ResolveTargetNode(sourceNode);
        if (targetNode is null)
        {
            return;
        }

        var occupiedNames = BuildNameRegistry();
        var clonedNode = CloneNode(targetNode, occupiedNames);

        var parentNode = FindParentFolder(CurrentAssetTree, targetNode);
        InsertNode(parentNode, clonedNode);
        LastAssetActionMessage = $"Cloned {targetNode.Name} -> {clonedNode.Name}";
    }

    [RelayCommand]
    private void CreateAsset(AssetNode? sourceNode)
    {
        CreateFolderCore(sourceNode);
    }

    [RelayCommand]
    private void EditAsset(AssetNode? sourceNode)
    {
        var targetNode = ResolveTargetNode(sourceNode);
        if (targetNode is null)
        {
            return;
        }

        if (!targetNode.Name.Contains("(edited)", StringComparison.Ordinal))
        {
            targetNode.Name = $"{targetNode.Name} (edited)";
        }

        LastAssetActionMessage = $"Edited {targetNode.Name}";
        ApplyAssetsSearchFilter();
    }

    [RelayCommand]
    private void RenameAsset(AssetNode? sourceNode)
    {
        var targetNode = ResolveTargetNode(sourceNode);
        if (targetNode is null)
        {
            return;
        }

        CancelAllRenameStates(targetNode);
        targetNode.EditName = targetNode.Name;
        targetNode.IsRenaming = true;
        SelectedAssetNode = targetNode;
        NotifyAssetCommandStateChanged();
        LastAssetActionMessage = $"Renaming {targetNode.Name}";
    }

    [RelayCommand(CanExecute = nameof(CanCommitRenameAsset))]
    private void CommitRenameAsset(AssetNode? sourceNode)
    {
        var targetNode = ResolveTargetNode(sourceNode);
        if (targetNode is null || !targetNode.IsRenaming)
        {
            return;
        }

        var requestedName = targetNode.EditName.Trim();
        if (string.IsNullOrWhiteSpace(requestedName))
        {
            requestedName = targetNode.Name;
        }

        var occupiedNames = BuildNameRegistry(targetNode);
        var uniqueName = ReserveUniqueName(requestedName, occupiedNames);
        targetNode.Name = uniqueName;
        targetNode.EditName = uniqueName;
        targetNode.IsRenaming = false;
        NotifyAssetCommandStateChanged();
        LastAssetActionMessage = $"Renamed to {uniqueName}";
        ApplyAssetsSearchFilter();
    }

    [RelayCommand(CanExecute = nameof(CanCommitRenameAsset))]
    private void CancelRenameAsset(AssetNode? sourceNode)
    {
        var targetNode = ResolveTargetNode(sourceNode);
        if (targetNode is null || !targetNode.IsRenaming)
        {
            return;
        }

        targetNode.EditName = targetNode.Name;
        targetNode.IsRenaming = false;
        NotifyAssetCommandStateChanged();
        LastAssetActionMessage = $"Cancelled rename {targetNode.Name}";
    }

    [RelayCommand(CanExecute = nameof(CanCopyHost))]
    private async Task CopyHost(AssetNode? sourceNode)
    {
        var targetNode = ResolveTargetNode(sourceNode);
        if (targetNode is not ConnectionNode connectionNode)
        {
            LastAssetActionMessage = "Current item has no host";
            return;
        }

        await _clipboardService.SetTextAsync(connectionNode.Host);
        LastAssetActionMessage = $"Copied host {connectionNode.Host}";
    }

    [RelayCommand]
    private async Task DeleteAsset(AssetNode? sourceNode)
    {
        var targetNode = ResolveTargetNode(sourceNode);
        if (targetNode is null)
        {
            return;
        }

        var confirmed = await _appDialogService.ShowDeleteAssetConfirmAsync(targetNode.Name, targetNode.IsFolder);
        if (!confirmed)
        {
            return;
        }

        if (TryRemoveNode(CurrentAssetTree, targetNode))
        {
            LastAssetActionMessage = $"Deleted {targetNode.Name}";
            if (ReferenceEquals(SelectedAssetNode, targetNode))
            {
                SelectedAssetNode = null;
            }

            if (ReferenceEquals(_assetClipboardNode, targetNode))
            {
                ClearAssetClipboard();
            }

            ApplyAssetsSearchFilter();
        }
    }

    [RelayCommand]
    private void CreateWorkspaceTab()
    {
        var newTab = BuildWorkspaceTab(
            header: "Quick Start",
            connectionLabel: "quick-start");
        WorkspaceTabs.Add(newTab);
        SelectedWorkspaceTab = newTab;
    }

    [RelayCommand]
    private void OpenQuickStartConnection(QuickStartRecentConnection? recent)
    {
        if (recent is null)
        {
            return;
        }

        var connection = ResolveConnectionNodeByRecent(recent);
        if (connection is null)
        {
            LastAssetActionMessage = $"无法定位连接 {recent.DisplayName}";
            return;
        }

        var newTab = BuildWorkspaceTab(connection);
        WorkspaceTabs.Add(newTab);
        SelectedWorkspaceTab = newTab;
        RegisterRecentConnection(connection);
        LastAssetActionMessage = $"已从 Quick Start 打开 {connection.Name}";
    }

    [RelayCommand]
    private void LocateHostFromQuickStart()
    {
        ActivateHostsAssetsPane();
        PendingQuickStartLocateTarget = null;

        var target = ResolveQuickStartLocateTarget();

        if (target is null)
        {
            LastAssetActionMessage = "已打开 Hosts 资产，Quick Start 暂无可定位目标";
            return;
        }

        if (IsTreeViewMode && TryLocateTargetInTree(target))
        {
            PendingQuickStartLocateTarget = target;
            LastAssetActionMessage = $"Quick Start 已定位到 Host：{target.Name}";
            return;
        }

        if (IsFlatViewMode && TryLocateTargetInFlatList(target))
        {
            PendingQuickStartLocateTarget = target;
            LastAssetActionMessage = $"Quick Start 已定位到 Host：{target.Name}";
            return;
        }

        PendingQuickStartLocateTarget = target;
        LastAssetActionMessage = $"Quick Start 已解析目标 Host：{target.Name}";
    }

    [RelayCommand]
    private void CloseTab(WorkspaceTabItemViewModel? sourceTab)
    {
        var targetTab = sourceTab ?? SelectedWorkspaceTab;
        if (targetTab is null)
        {
            return;
        }

        var targetIndex = WorkspaceTabs.IndexOf(targetTab);
        if (targetIndex < 0)
        {
            return;
        }

        DisposeTabConnection(targetTab);
        WorkspaceTabs.RemoveAt(targetIndex);

        if (WorkspaceTabs.Count == 0)
        {
            SelectedWorkspaceTab = null;
            return;
        }

        var nextIndex = Math.Clamp(targetIndex - 1, 0, WorkspaceTabs.Count - 1);
        SelectedWorkspaceTab = WorkspaceTabs[nextIndex];
    }

    [RelayCommand]
    private void DuplicateTab(WorkspaceTabItemViewModel? sourceTab)
    {
        var targetTab = sourceTab ?? SelectedWorkspaceTab;
        if (targetTab is null)
        {
            return;
        }

        var targetIndex = WorkspaceTabs.IndexOf(targetTab);
        if (targetIndex < 0)
        {
            return;
        }

        var duplicatedTab = targetTab.DuplicateAs($"tab-{Guid.NewGuid():N}");
        WorkspaceTabs.Insert(targetIndex + 1, duplicatedTab);
        SelectedWorkspaceTab = duplicatedTab;
    }

    [RelayCommand]
    private void CloseOtherTabs(WorkspaceTabItemViewModel? sourceTab)
    {
        var targetTab = sourceTab ?? SelectedWorkspaceTab;
        if (targetTab is null)
        {
            return;
        }

        var targetIndex = WorkspaceTabs.IndexOf(targetTab);
        if (targetIndex < 0)
        {
            return;
        }

        var tabsToClose = WorkspaceTabs
            .Where(tab => !ReferenceEquals(tab, targetTab))
            .ToArray();
        foreach (var tab in tabsToClose)
        {
            DisposeTabConnection(tab);
        }

        WorkspaceTabs.Clear();
        WorkspaceTabs.Add(targetTab);
        SelectedWorkspaceTab = targetTab;
    }

    [RelayCommand]
    private void CloseTabsToLeft(WorkspaceTabItemViewModel? sourceTab)
    {
        var targetTab = sourceTab ?? SelectedWorkspaceTab;
        if (targetTab is null)
        {
            return;
        }

        var targetIndex = WorkspaceTabs.IndexOf(targetTab);
        if (targetIndex < 0)
        {
            return;
        }

        for (var i = targetIndex - 1; i >= 0; i--)
        {
            DisposeTabConnection(WorkspaceTabs[i]);
            WorkspaceTabs.RemoveAt(i);
        }

        SelectedWorkspaceTab = targetTab;
    }

    [RelayCommand]
    private void CloseTabsToRight(WorkspaceTabItemViewModel? sourceTab)
    {
        var targetTab = sourceTab ?? SelectedWorkspaceTab;
        if (targetTab is null)
        {
            return;
        }

        var targetIndex = WorkspaceTabs.IndexOf(targetTab);
        if (targetIndex < 0)
        {
            return;
        }

        for (var i = WorkspaceTabs.Count - 1; i > targetIndex; i--)
        {
            DisposeTabConnection(WorkspaceTabs[i]);
            WorkspaceTabs.RemoveAt(i);
        }

        SelectedWorkspaceTab = targetTab;
    }

    [RelayCommand]
    private void CloseAllTabs()
    {
        foreach (var tab in WorkspaceTabs)
        {
            DisposeTabConnection(tab);
        }

        WorkspaceTabs.Clear();
        SelectedWorkspaceTab = null;
    }

    partial void OnIsAssetsPanelVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(IsLeftAssetsPaneOpen));
        OnPropertyChanged(nameof(LeftAssetsPaneWidth));
        OnPropertyChanged(nameof(LeftAssetsPaneToggleGlyph));
        OnPropertyChanged(nameof(LeftAssetsPaneToggleToolTip));
    }

    partial void OnIsRightSidebarVisibleChanged(bool value)
    { }

    partial void OnSelectedAssetsPaneChanged(AssetsPaneKind value)
    {
        ClearAssetClipboard();
        ClearSelectedAssets();
        CancelAllRenameStates();
        NotifyAssetCommandStateChanged();
        OnPropertyChanged(nameof(CurrentAssetTree));
        OnPropertyChanged(nameof(AssetsPanelTitle));
        OnPropertyChanged(nameof(IsKeysAssetsPane));
        OnPropertyChanged(nameof(IsGeneralAssetsPane));
        ApplyAssetsSearchFilter();
    }

    partial void OnAssetsViewModeChanged(AssetsViewMode value)
    {
        OnPropertyChanged(nameof(IsTreeViewMode));
        OnPropertyChanged(nameof(IsFlatViewMode));
        OnPropertyChanged(nameof(AssetsViewModeGlyph));
        OnPropertyChanged(nameof(AssetsViewModeToolTip));
        if (value == AssetsViewMode.Tree)
        {
            ClearSelectedAssets();
        }
    }

    partial void OnSelectedRightToolsViewChanged(RightToolsViewKind value)
    {
        OnPropertyChanged(nameof(IsSnippetsView));
        OnPropertyChanged(nameof(IsHistoryView));
        OnPropertyChanged(nameof(IsSftpView));
        OnPropertyChanged(nameof(CurrentRightToolsContent));
        OnPropertyChanged(nameof(ActiveRightMode));
        OnPropertyChanged(nameof(ActiveRightHeader));

        var target = RightToolsModeItems.FirstOrDefault(item => item.Kind == value);
        if (target is not null && !ReferenceEquals(target, SelectedRightToolsModeItem))
        {
            SelectedRightToolsModeItem = target;
        }
    }

    partial void OnSelectedRightToolsModeItemChanged(RightToolsModeItem? value)
    {
        if (value is not null && value.Kind != SelectedRightToolsView)
        {
            SelectedRightToolsView = value.Kind;
        }
    }

    partial void OnSelectedWorkspacePaneChanged(WorkspacePaneViewModel? value)
    {
        OnPropertyChanged(nameof(WorkspaceTabs));
        OnPropertyChanged(nameof(Tabs));

        if (value is null)
        {
            return;
        }

        if (value.Tabs.Count == 0)
        {
            if (SelectedWorkspaceTab is not null &&
                ResolvePaneByTab(SelectedWorkspaceTab) is null)
            {
                SelectedWorkspaceTab = null;
            }

            return;
        }

        if (value.SelectedTab is null || !value.Tabs.Contains(value.SelectedTab))
        {
            value.SelectedTab = value.Tabs[0];
        }

        if (!ReferenceEquals(SelectedWorkspaceTab, value.SelectedTab))
        {
            SelectedWorkspaceTab = value.SelectedTab;
        }
    }

    partial void OnSelectedWorkspaceTabChanged(WorkspaceTabItemViewModel? value)
    {
        if (value is not null)
        {
            var owningPane = ResolvePaneByTab(value);
            if (owningPane is not null && !ReferenceEquals(SelectedWorkspacePane, owningPane))
            {
                SelectedWorkspacePane = owningPane;
            }

            if (owningPane is not null && !ReferenceEquals(owningPane.SelectedTab, value))
            {
                owningPane.SelectedTab = value;
            }
        }
        else if (SelectedWorkspacePane is not null && SelectedWorkspacePane.SelectedTab is not null)
        {
            SelectedWorkspacePane.SelectedTab = null;
        }

        OnPropertyChanged(nameof(SelectedTab));
        SyncActiveWorkspaceTabs();
        RuntimeLogger.Info(
            "tab-selection",
            $"selectedType={value?.GetType().Name ?? "<null>"}, header={value?.Header ?? "<null>"}, id={value?.Id ?? "<null>"}, placeholder_chars={value?.PlaceholderText.Length ?? 0}");
    }

    partial void OnCurrentLanguageCodeChanged(string value)
    {
        OnPropertyChanged(nameof(CurrentLanguageLabel));
    }

    partial void OnSelectedAssetNodeChanged(AssetNode? value)
    {
        _ = value;
        NotifyAssetCommandStateChanged();
    }

    partial void OnAssetsSearchTextChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            IsAssetsSearchOpen = true;
        }

        ApplyAssetsSearchFilter();
    }

    partial void OnQuickStartSearchTextChanged(string value)
    {
        _ = value;
        ApplyQuickStartRecentConnectionsFilter();
    }

    partial void OnAreAllTreeFoldersExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(AssetsExpandCollapseGlyph));
        OnPropertyChanged(nameof(AssetsExpandCollapseToolTip));
    }

    partial void OnIsWorkspaceDragOverlayVisibleChanged(bool value)
    {
        _ = value;
        OnPropertyChanged(nameof(IsWorkspacePaneHoverActive));
    }

    partial void OnWorkspaceDragHoverPaneIdChanged(string? value)
    {
        _ = value;
        OnPropertyChanged(nameof(IsWorkspacePaneHoverActive));
    }

    partial void OnWorkspaceDragHoverDirectionChanged(WorkspaceDropDirection? value)
    {
        _ = value;
        OnPropertyChanged(nameof(IsWorkspaceDropSlotLeftHot));
        OnPropertyChanged(nameof(IsWorkspaceDropSlotRightHot));
        OnPropertyChanged(nameof(IsWorkspaceDropSlotTopHot));
        OnPropertyChanged(nameof(IsWorkspaceDropSlotBottomHot));
    }

    private bool IsDarkTheme
    {
        get
        {
            if (Application.Current is null)
            {
                return true;
            }

            var requested = Application.Current.RequestedThemeVariant;
            if (requested == ThemeVariant.Default)
            {
                return Application.Current.ActualThemeVariant == ThemeVariant.Dark;
            }

            return requested == ThemeVariant.Dark;
        }
    }

    private void InitializeWorkspacePanes()
    {
        WorkspacePanes.Clear();

        var rootPaneId = _workspaceLayoutService.Root is PaneNode rootPane
            ? rootPane.PaneId
            : "pane-1";
        _workspaceLayoutService.InitializeRootPane(rootPaneId);

        var pane = GetOrCreateWorkspacePane(rootPaneId);
        SelectedWorkspacePane = pane;
    }

    private void InitializeWorkspaceTabs()
    {
        var quickStart = BuildWorkspaceTab(
            header: "Quick Start",
            connectionLabel: "quick-start");

        quickStart.PlaceholderText = "双击左侧 Hosts 资产打开终端会话";
        quickStart.SessionStatusMessage = "Ready";

        WorkspaceTabs.Add(quickStart);
        SelectedWorkspaceTab = quickStart;
        RuntimeLogger.Info("tab-init", $"Initialized default tabs. count={WorkspaceTabs.Count}");
    }

    private WorkspacePaneViewModel ResolveActiveWorkspacePane()
    {
        if (SelectedWorkspacePane is not null)
        {
            return SelectedWorkspacePane;
        }

        if (WorkspacePanes.Count == 0)
        {
            GetOrCreateWorkspacePane("pane-1");
        }

        SelectedWorkspacePane = WorkspacePanes[0];
        return WorkspacePanes[0];
    }

    public WorkspacePaneViewModel GetOrCreateWorkspacePane(string paneId)
    {
        if (string.IsNullOrWhiteSpace(paneId))
        {
            throw new ArgumentException("Pane id cannot be null or whitespace.", nameof(paneId));
        }

        foreach (var pane in WorkspacePanes)
        {
            if (string.Equals(pane.PaneId, paneId, StringComparison.Ordinal))
            {
                return pane;
            }
        }

        var created = new WorkspacePaneViewModel(paneId);
        WorkspacePanes.Add(created);
        return created;
    }

    private WorkspacePaneViewModel? ResolvePaneByTab(WorkspaceTabItemViewModel tab)
    {
        foreach (var pane in WorkspacePanes)
        {
            if (pane.Tabs.Contains(tab))
            {
                return pane;
            }
        }

        return null;
    }

    private WorkspacePaneViewModel? ResolvePaneById(string paneId)
    {
        if (string.IsNullOrWhiteSpace(paneId))
        {
            return null;
        }

        foreach (var pane in WorkspacePanes)
        {
            if (string.Equals(pane.PaneId, paneId, StringComparison.Ordinal))
            {
                return pane;
            }
        }

        return null;
    }

    private WorkspaceTabItemViewModel? ResolveTabFromDragSession(WorkspaceDragSession session)
    {
        if (session.TabReference is WorkspaceTabItemViewModel tabFromReference)
        {
            return tabFromReference;
        }

        foreach (var pane in WorkspacePanes)
        {
            var matched = pane.Tabs.FirstOrDefault(tab =>
                string.Equals(tab.Id, session.TabId, StringComparison.Ordinal));
            if (matched is not null)
            {
                return matched;
            }
        }

        return null;
    }

    private static int NormalizeInsertionIndex(int? index, int itemCount)
    {
        if (!index.HasValue)
        {
            return itemCount;
        }

        if (index.Value <= 0)
        {
            return 0;
        }

        return index.Value >= itemCount ? itemCount : index.Value;
    }

    private bool TryRecycleWorkspacePaneIfEmpty(WorkspacePaneViewModel pane)
    {
        if (pane.Tabs.Count > 0)
        {
            return false;
        }

        if (!_workspaceLayoutService.RecyclePaneIfEmpty(pane.PaneId))
        {
            return false;
        }

        WorkspacePanes.Remove(pane);
        if (ReferenceEquals(SelectedWorkspacePane, pane))
        {
            SelectedWorkspacePane = WorkspacePanes.FirstOrDefault();
        }

        return true;
    }

    public bool CompleteWorkspaceDragDrop(string targetPaneId, WorkspaceDropDirection? dropDirection, int? targetIndex = null)
    {
        var session = CommitWorkspaceDragPreview();
        if (session is null)
        {
            return false;
        }

        var draggedTab = ResolveTabFromDragSession(session);
        if (draggedTab is null)
        {
            RuntimeLogger.Warn(
                "workspace-drag",
                $"Drop failed: cannot resolve dragged tab. tab_id={session.TabId}");
            return false;
        }

        var sourcePane = ResolvePaneById(session.SourcePaneId) ?? ResolvePaneByTab(draggedTab);
        var targetPane = ResolvePaneById(targetPaneId);
        if (sourcePane is null || targetPane is null)
        {
            RuntimeLogger.Warn(
                "workspace-drag",
                $"Drop failed: pane missing. source={session.SourcePaneId}, target={targetPaneId}, tab_id={session.TabId}");
            return false;
        }

        var layoutChanged = false;
        if (dropDirection is null)
        {
            if (!_workspaceLayoutService.MoveTab(sourcePane.PaneId, targetPane.PaneId, draggedTab.Id, targetIndex))
            {
                RuntimeLogger.Warn(
                    "workspace-drag",
                    $"Drop move rejected by layout service. source={sourcePane.PaneId}, target={targetPane.PaneId}, tab_id={draggedTab.Id}");
                return false;
            }

            if (ReferenceEquals(sourcePane, targetPane))
            {
                var currentIndex = sourcePane.Tabs.IndexOf(draggedTab);
                if (currentIndex >= 0)
                {
                    sourcePane.Tabs.RemoveAt(currentIndex);
                    var insertionIndex = NormalizeInsertionIndex(targetIndex, sourcePane.Tabs.Count);
                    sourcePane.Tabs.Insert(insertionIndex, draggedTab);
                }
            }
            else
            {
                sourcePane.Tabs.Remove(draggedTab);
                var insertionIndex = NormalizeInsertionIndex(targetIndex, targetPane.Tabs.Count);
                targetPane.Tabs.Insert(insertionIndex, draggedTab);
                layoutChanged = TryRecycleWorkspacePaneIfEmpty(sourcePane);
            }

            SelectedWorkspacePane = targetPane;
            SelectedWorkspaceTab = draggedTab;
            if (layoutChanged)
            {
                OnPropertyChanged(nameof(WorkspaceLayoutRoot));
            }

            return true;
        }

        if (_workspaceLayoutService.PaneIds.Count >= MaxWorkspacePaneCount)
        {
            LastAssetActionMessage = $"已达到最多 {MaxWorkspacePaneCount} 个分屏，无法继续分屏";
            RuntimeLogger.Warn(
                "workspace-drag",
                $"Split rejected by pane limit. limit={MaxWorkspacePaneCount}, current={_workspaceLayoutService.PaneIds.Count}");
            return false;
        }

        var movedForSplitPreparation = false;
        var sourceOriginalIndex = -1;
        if (!ReferenceEquals(sourcePane, targetPane))
        {
            sourceOriginalIndex = sourcePane.Tabs.IndexOf(draggedTab);
            if (!_workspaceLayoutService.MoveTab(sourcePane.PaneId, targetPane.PaneId, draggedTab.Id))
            {
                RuntimeLogger.Warn(
                    "workspace-drag",
                    $"Pre-split move rejected by layout service. source={sourcePane.PaneId}, target={targetPane.PaneId}, tab_id={draggedTab.Id}");
                return false;
            }

            sourcePane.Tabs.Remove(draggedTab);
            targetPane.Tabs.Add(draggedTab);
            movedForSplitPreparation = true;
        }

        var knownPaneIds = _workspaceLayoutService.PaneIds.ToHashSet(StringComparer.Ordinal);
        if (!_workspaceLayoutService.SplitAndMove(targetPane.PaneId, draggedTab.Id, dropDirection.Value))
        {
            RuntimeLogger.Warn(
                "workspace-drag",
                $"Split drop rejected by layout service. pane={targetPane.PaneId}, tab_id={draggedTab.Id}, direction={dropDirection.Value}");
            if (movedForSplitPreparation)
            {
                int? rollbackIndexHint = sourceOriginalIndex >= 0 ? sourceOriginalIndex : null;
                var rollbackIndex = NormalizeInsertionIndex(rollbackIndexHint, sourcePane.Tabs.Count);
                _workspaceLayoutService.MoveTab(targetPane.PaneId, sourcePane.PaneId, draggedTab.Id, rollbackIndex);
                targetPane.Tabs.Remove(draggedTab);
                sourcePane.Tabs.Insert(rollbackIndex, draggedTab);
                SelectedWorkspacePane = sourcePane;
                SelectedWorkspaceTab = draggedTab;
            }

            return false;
        }

        var createdPaneId = _workspaceLayoutService.PaneIds.FirstOrDefault(id => !knownPaneIds.Contains(id));
        if (string.IsNullOrWhiteSpace(createdPaneId))
        {
            RuntimeLogger.Warn(
                "workspace-drag",
                $"Split drop missing created pane id. pane={targetPane.PaneId}, tab_id={draggedTab.Id}, direction={dropDirection.Value}");
            return false;
        }

        targetPane.Tabs.Remove(draggedTab);
        var createdPane = GetOrCreateWorkspacePane(createdPaneId);
        createdPane.Tabs.Add(draggedTab);
        SelectedWorkspacePane = createdPane;
        SelectedWorkspaceTab = draggedTab;

        if (!ReferenceEquals(sourcePane, targetPane))
        {
            layoutChanged = TryRecycleWorkspacePaneIfEmpty(sourcePane);
        }

        OnPropertyChanged(nameof(WorkspaceLayoutRoot));
        return true;
    }

    public void BeginWorkspaceDragPreview(string sourcePaneId, string tabId, WorkspaceTabItemViewModel? tabReference = null)
    {
        _dragSessionService.Start(sourcePaneId, tabId, tabReference);
        IsWorkspaceDragOverlayVisible = true;
        WorkspaceDragHoverPaneId = sourcePaneId;
        WorkspaceDragHoverDirection = null;
    }

    public void UpdateWorkspaceDragPreview(string targetPaneId, WorkspaceDropDirection? dropDirection)
    {
        if (!_dragSessionService.IsActive)
        {
            return;
        }

        _dragSessionService.UpdateHover(targetPaneId, dropDirection);
        IsWorkspaceDragOverlayVisible = true;
        WorkspaceDragHoverPaneId = targetPaneId;
        WorkspaceDragHoverDirection = dropDirection;
    }

    public WorkspaceDragSession? CommitWorkspaceDragPreview()
    {
        var session = _dragSessionService.Commit();
        ClearWorkspaceDragOverlayState();
        return session;
    }

    public void CancelWorkspaceDragPreview()
    {
        _dragSessionService.Cancel();
        ClearWorkspaceDragOverlayState();
    }

    private void ClearWorkspaceDragOverlayState()
    {
        IsWorkspaceDragOverlayVisible = false;
        WorkspaceDragHoverPaneId = null;
        WorkspaceDragHoverDirection = null;
    }

    private void InitializeQuickStartRecentConnections()
    {
        var seededConnections = Flatten(_assetsByPane[AssetsPaneKind.Hosts])
            .OfType<ConnectionNode>()
            .Take(MaxQuickStartRecentConnections)
            .ToList();

        var now = DateTimeOffset.Now;
        for (var i = 0; i < seededConnections.Count; i++)
        {
            var connection = seededConnections[i];
            QuickStartRecentConnections.Add(
                new QuickStartRecentConnection(
                    connection.Id,
                    connection.Name,
                    connection.Host,
                    connection.User,
                    connection.Port,
                    now.AddMinutes(-(i + 1) * 7)));
        }

        ApplyQuickStartRecentConnectionsFilter();
    }

    private void InitializeRightToolsData()
    {
        SnippetItems.Add(new CommandSnippet
        {
            Name = "Restart Service",
            Command = "sudo systemctl restart nginx",
        });
        SnippetItems.Add(new CommandSnippet
        {
            Name = "Tail Logs",
            Command = "tail -f /var/log/syslog",
        });
        SnippetItems.Add(new CommandSnippet
        {
            Name = "Disk Usage",
            Command = "df -h",
        });

        HistoryItems.Add(new CommandHistoryEntry
        {
            Timestamp = DateTime.Now.AddMinutes(-8),
            Command = "kubectl get pods -A",
        });
        HistoryItems.Add(new CommandHistoryEntry
        {
            Timestamp = DateTime.Now.AddMinutes(-2),
            Command = "sudo journalctl -u ssh --since '10 min ago'",
        });
    }

    private void InitializeRightPanelModes()
    {
        RightPanelModes.Clear();
        RightPanelModes.Add(new SnippetsModeViewModel(BuildSnippetModeActions()));
        RightPanelModes.Add(new HistoryModeViewModel(BuildHistoryModeActions()));
        RightPanelModes.Add(new SftpModeViewModel(_sftpNavigationService, BuildSftpModeActions()));
    }

    private IRightPanelModeViewModel ResolveActiveRightMode(RightToolsViewKind kind)
    {
        var mode = RightPanelModes.FirstOrDefault(item => item.Kind == kind);
        if (mode is not null)
        {
            return mode;
        }

        return RightPanelModes.Count > 0 ? RightPanelModes[0] : FallbackRightPanelMode;
    }

    private IReadOnlyList<ModeActionDescriptor> BuildSnippetModeActions()
    {
        return
        [
            new("snippet.new", "\uE710", "New snippet", CreateSnippetCommand),
            new("snippet.search", "\uE721", "Search snippets", SearchSnippetCommand),
            new("snippet.sort", "\uE8CB", "Sort snippets", SortSnippetCommand),
            new("snippet.layout", "\uE8A4", "Toggle layout", ToggleSnippetLayoutCommand, IsToggle: true),
        ];
    }

    private IReadOnlyList<ModeActionDescriptor> BuildHistoryModeActions()
    {
        return
        [
            new("history.search", "\uE721", "Search history", SearchHistoryCommand),
            new("history.sort", "\uE8CB", "Sort history", SortHistoryCommand),
            new("history.layout", "\uE8A4", "Toggle layout", ToggleHistoryLayoutCommand, IsToggle: true),
            new("history.clear", "\uE74D", "Clear history", ClearHistoryCommand),
        ];
    }

    private IReadOnlyList<ModeActionDescriptor> BuildSftpModeActions()
    {
        return [];
    }

    private WorkspaceTabItemViewModel BuildWorkspaceTab(ConnectionNode connectionNode)
    {
        var scopedConnectionId = $"{connectionNode.Id}-{Guid.NewGuid():N}";
        return BuildWorkspaceTab(
            connectionNode.Name,
            connectionNode.Name,
            new ConnectionConfig
            {
                ConnectionId = scopedConnectionId,
                Host = connectionNode.Host,
                Port = connectionNode.Port,
                Username = connectionNode.User,
                Password = connectionNode.Password,
            });
    }

    private WorkspaceTabItemViewModel BuildWorkspaceTab(string connectionLabel)
    {
        return BuildWorkspaceTab(connectionLabel, connectionLabel);
    }

    private WorkspaceTabItemViewModel BuildWorkspaceTab(
        string header,
        string connectionLabel,
        ConnectionConfig? connectionConfig = null)
    {
        var index = _newWorkspaceTabSeed++;
        var accentBrush = new SolidColorBrush(WorkspaceAccentPalette[(index - 1) % WorkspaceAccentPalette.Length]);

        var tab = new WorkspaceTabItemViewModel(
            $"tab-{Guid.NewGuid():N}",
            header,
            connectionLabel,
            $"Terminal placeholder for {connectionLabel}",
            accentBrush,
            connectionConfig);
        RuntimeLogger.Info(
            "tab-build",
            $"Built tab. id={tab.Id}, header={tab.Header}, has_connection={(connectionConfig is not null).ToString().ToLowerInvariant()}, host={connectionConfig?.Host ?? "<none>"}");
        return tab;
    }

    public void MarkConnectionAsRecentlyUsed(ConnectionConfig? config)
    {
        if (config is null ||
            string.IsNullOrWhiteSpace(config.Host) ||
            string.IsNullOrWhiteSpace(config.Username))
        {
            return;
        }

        var matched = ResolveConnectionNodeByConfig(config);

        if (matched is not null)
        {
            RegisterRecentConnection(matched);
            return;
        }

        var fallback = new ConnectionNode(
            $"quick-{Guid.NewGuid():N}",
            config.Host,
            config.Host,
            config.Username,
            config.Port,
            "SSH Connection",
            config.Password);
        RegisterRecentConnection(fallback);
    }

    private ConnectionNode? ResolveQuickStartLocateTarget()
    {
        var currentTabTarget = ResolveConnectionNodeBySelectedTab();
        if (currentTabTarget is not null)
        {
            return currentTabTarget;
        }

        var firstRecent = FilteredQuickStartRecentConnections.FirstOrDefault();
        if (firstRecent is not null)
        {
            return ResolveConnectionNodeByRecent(firstRecent);
        }

        return null;
    }

    private void ActivateHostsAssetsPane()
    {
        IsAssetsPanelVisible = true;
        SelectedAssetsPane = AssetsPaneKind.Hosts;
    }

    private ConnectionNode? ResolveConnectionNodeBySelectedTab()
    {
        var config = SelectedWorkspaceTab?.ConnectionConfig;
        if (config is null)
        {
            return null;
        }

        return ResolveConnectionNodeByConfig(config);
    }

    private ConnectionNode? ResolveConnectionNodeByConfig(ConnectionConfig config)
    {
        return Flatten(_assetsByPane[AssetsPaneKind.Hosts])
            .OfType<ConnectionNode>()
            .FirstOrDefault(node =>
                node.Port == config.Port &&
                string.Equals(node.Host, config.Host, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(node.User, config.Username, StringComparison.OrdinalIgnoreCase));
    }

    private bool TryLocateTargetInTree(ConnectionNode target)
    {
        var treeTarget = FindConnectionNodeInCurrentHostsTree(target);
        if (treeTarget is null)
        {
            return false;
        }

        ExpandTreeAncestors(treeTarget);
        SelectedAssetNode = treeTarget;
        StartQuickLocateHighlight(treeTarget);
        return true;
    }

    private bool TryLocateTargetInFlatList(ConnectionNode target)
    {
        var flatTarget = FindConnectionNodeInCurrentFlatHosts(target);
        if (flatTarget is null)
        {
            return false;
        }

        SelectedAssetNode = flatTarget;
        SetSelectedAssets([flatTarget]);
        StartQuickLocateHighlight(flatTarget);
        return true;
    }

    private ConnectionNode? FindConnectionNodeInCurrentHostsTree(ConnectionNode target)
    {
        return Flatten(CurrentAssetTree)
            .OfType<ConnectionNode>()
            .FirstOrDefault(node =>
                string.Equals(node.Id, target.Id, StringComparison.Ordinal) ||
                (string.Equals(node.Host, target.Host, StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(node.User, target.User, StringComparison.OrdinalIgnoreCase) &&
                 node.Port == target.Port));
    }

    private ConnectionNode? FindConnectionNodeInCurrentFlatHosts(ConnectionNode target)
    {
        return CurrentAssetFlatList
            .OfType<ConnectionNode>()
            .FirstOrDefault(node =>
                string.Equals(node.Id, target.Id, StringComparison.Ordinal) ||
                (string.Equals(node.Host, target.Host, StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(node.User, target.User, StringComparison.OrdinalIgnoreCase) &&
                 node.Port == target.Port));
    }

    private void ExpandTreeAncestors(AssetNode targetNode)
    {
        var ancestors = new List<FolderNode>();
        if (!TryCollectAncestorFolders(CurrentAssetTree, targetNode, ancestors))
        {
            return;
        }

        foreach (var folder in ancestors)
        {
            folder.IsExpanded = true;
        }

        AreAllTreeFoldersExpanded = AreAllFoldersExpanded(CurrentAssetTree);
    }

    private void StartQuickLocateHighlight(AssetNode targetNode)
    {
        _quickLocateHighlightCts?.Cancel();
        _quickLocateHighlightCts?.Dispose();
        _quickLocateHighlightCts = new CancellationTokenSource();

        foreach (var node in Flatten(CurrentAssetTree))
        {
            if (!ReferenceEquals(node, targetNode) && node.QuickLocateHighlightOpacity > 0d)
            {
                node.QuickLocateHighlightOpacity = 0d;
            }
        }

        targetNode.QuickLocateHighlightOpacity = 1d;
        _ = RunQuickLocateHighlightAsync(targetNode, _quickLocateHighlightCts.Token);
    }

    private static async Task RunQuickLocateHighlightAsync(AssetNode targetNode, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(1200), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!cancellationToken.IsCancellationRequested)
        {
            targetNode.QuickLocateHighlightOpacity = 0d;
        }
    }

    private void RegisterRecentConnection(ConnectionNode connection)
    {
        var snapshot = new QuickStartRecentConnection(
            connection.Id,
            connection.Name,
            connection.Host,
            connection.User,
            connection.Port,
            DateTimeOffset.Now);

        var existingIndex = -1;
        for (var i = 0; i < QuickStartRecentConnections.Count; i++)
        {
            var item = QuickStartRecentConnections[i];
            if (string.Equals(item.AssetId, snapshot.AssetId, StringComparison.Ordinal) ||
                (string.Equals(item.Host, snapshot.Host, StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(item.Username, snapshot.Username, StringComparison.OrdinalIgnoreCase) &&
                 item.Port == snapshot.Port))
            {
                existingIndex = i;
                break;
            }
        }

        if (existingIndex >= 0)
        {
            QuickStartRecentConnections.RemoveAt(existingIndex);
        }

        QuickStartRecentConnections.Insert(0, snapshot);
        while (QuickStartRecentConnections.Count > MaxQuickStartRecentConnections)
        {
            QuickStartRecentConnections.RemoveAt(QuickStartRecentConnections.Count - 1);
        }

        ApplyQuickStartRecentConnectionsFilter();
    }

    private ConnectionNode? ResolveConnectionNodeByRecent(QuickStartRecentConnection recent)
    {
        var matched = Flatten(_assetsByPane[AssetsPaneKind.Hosts])
            .OfType<ConnectionNode>()
            .FirstOrDefault(node =>
                string.Equals(node.Id, recent.AssetId, StringComparison.Ordinal) ||
                (string.Equals(node.Host, recent.Host, StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(node.User, recent.Username, StringComparison.OrdinalIgnoreCase) &&
                 node.Port == recent.Port));

        if (matched is not null)
        {
            return matched;
        }

        return new ConnectionNode(
            $"quick-{Guid.NewGuid():N}",
            recent.DisplayName,
            recent.Host,
            recent.Username,
            recent.Port,
            "SSH Connection");
    }

    private void ApplyQuickStartRecentConnectionsFilter()
    {
        var keyword = QuickStartSearchText.Trim();
        var filtered = QuickStartRecentConnections
            .Where(item => item.Matches(keyword))
            .OrderByDescending(item => item.LastUsedAt)
            .ToList();

        FilteredQuickStartRecentConnections.Clear();
        foreach (var item in filtered)
        {
            FilteredQuickStartRecentConnections.Add(item);
        }

        OnPropertyChanged(nameof(HasQuickStartRecentConnections));
        OnPropertyChanged(nameof(IsQuickStartRecentConnectionsEmpty));
    }

    private void DisposeTabConnection(WorkspaceTabItemViewModel tab)
    {
        _ = _sessionRegistryService.DisposeAsync(tab.Id);
        RuntimeLogger.Info("tab-dispose", $"Requested session dispose. tab_id={tab.Id}");
    }

    private static string GetAppVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version is null ? "dev" : $"{version.Major}.{version.Minor}.{version.Build}";
    }

    private AssetNode? ResolveTargetNode(AssetNode? sourceNode)
    {
        return sourceNode ?? SelectedAssetNode;
    }

    private void ClearAssetClipboard()
    {
        _assetClipboardNode = null;
        _assetClipboardMode = AssetClipboardMode.None;
        NotifyAssetCommandStateChanged();
    }

    private bool CanPasteAsset(AssetNode? sourceNode)
    {
        _ = sourceNode;
        return HasAssetClipboardData;
    }

    private bool CanCopyHost(AssetNode? sourceNode)
    {
        var targetNode = ResolveTargetNode(sourceNode);
        return targetNode is ConnectionNode;
    }

    private bool CanCommitRenameAsset(AssetNode? sourceNode)
    {
        var targetNode = ResolveTargetNode(sourceNode);
        return targetNode?.IsRenaming == true;
    }

    private bool CanExportAssets(AssetNode? sourceNode)
    {
        return ResolveAssetOperationTargets(sourceNode).Any();
    }

    private bool CanOpenAssetInNewTab(AssetNode? sourceNode)
    {
        if (IsKeysAssetsPane)
        {
            return false;
        }

        return ResolveAssetOperationTargets(sourceNode).OfType<ConnectionNode>().Any();
    }

    private void NotifyAssetCommandStateChanged()
    {
        OnPropertyChanged(nameof(HasAssetClipboardData));
        PasteAssetCommand.NotifyCanExecuteChanged();
        CopyHostCommand.NotifyCanExecuteChanged();
        CommitRenameAssetCommand.NotifyCanExecuteChanged();
        CancelRenameAssetCommand.NotifyCanExecuteChanged();
        ExportAssetsCommand.NotifyCanExecuteChanged();
        OpenAssetInNewTabCommand.NotifyCanExecuteChanged();
    }

    public void SetSelectedAssets(IEnumerable<AssetNode> selectedNodes)
    {
        var snapshot = selectedNodes.Distinct().ToList();
        SelectedAssetNodes.Clear();
        foreach (var node in snapshot)
        {
            SelectedAssetNodes.Add(node);
        }

        NotifyAssetCommandStateChanged();
    }

    private IEnumerable<AssetNode> ResolveAssetOperationTargets(AssetNode? sourceNode)
    {
        if (sourceNode is not null)
        {
            if (IsFlatViewMode &&
                SelectedAssetNodes.Count > 1 &&
                SelectedAssetNodes.Contains(sourceNode))
            {
                return SelectedAssetNodes;
            }

            return [sourceNode];
        }

        if (IsFlatViewMode && SelectedAssetNodes.Count > 0)
        {
            return SelectedAssetNodes;
        }

        var selected = ResolveTargetNode(null);
        return selected is null ? [] : [selected];
    }

    private AssetNode? ResolvePasteParent(AssetNode? sourceNode)
    {
        if (IsFlatViewMode)
        {
            return null;
        }

        var targetNode = ResolveTargetNode(sourceNode);
        if (targetNode is FolderNode)
        {
            return targetNode;
        }

        return targetNode is null ? null : FindParentFolder(CurrentAssetTree, targetNode);
    }

    private HashSet<string> BuildNameRegistry(AssetNode? excludeNode = null)
    {
        return Flatten(CurrentAssetTree)
            .Where(node => !ReferenceEquals(node, excludeNode))
            .Select(node => node.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static AssetNode CloneNode(AssetNode sourceNode, ISet<string> occupiedNames)
    {
        var cloneName = ResolveCloneName(sourceNode.Name, occupiedNames);

        if (sourceNode is ConnectionNode connectionNode)
        {
            return new ConnectionNode(
                $"conn-{Guid.NewGuid():N}",
                cloneName,
                connectionNode.Host,
                connectionNode.User,
                connectionNode.Port,
                sourceNode.Kind,
                connectionNode.Password);
        }

        var childClones = sourceNode.Children
            .Select(child => CloneNode(child, occupiedNames))
            .ToList();

        return new FolderNode(
            $"folder-{Guid.NewGuid():N}",
            cloneName,
            sourceNode.Kind,
            childClones);
    }

    private static string ResolveCloneName(string sourceName, ISet<string> occupiedNames)
    {
        var baseName = ExtractCloneBaseName(sourceName);
        var normalized = string.IsNullOrWhiteSpace(baseName) ? "Asset" : baseName;

        var usedSuffixes = new HashSet<int>();
        foreach (var name in occupiedNames)
        {
            if (!name.StartsWith($"{normalized}-", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var suffixToken = name[(normalized.Length + 1)..];
            if (int.TryParse(suffixToken, out var suffixValue) && suffixValue > 0)
            {
                usedSuffixes.Add(suffixValue);
            }
        }

        var candidateIndex = 1;
        while (usedSuffixes.Contains(candidateIndex))
        {
            candidateIndex++;
        }

        var candidateName = $"{normalized}-{candidateIndex}";
        occupiedNames.Add(candidateName);
        return candidateName;
    }

    private static string ReserveUniqueName(string preferredName, ISet<string> occupiedNames)
    {
        var normalized = string.IsNullOrWhiteSpace(preferredName)
            ? "Asset"
            : preferredName.Trim();

        if (!occupiedNames.Contains(normalized))
        {
            occupiedNames.Add(normalized);
            return normalized;
        }

        var usedSuffixes = new HashSet<int>();
        foreach (var name in occupiedNames)
        {
            if (!name.StartsWith($"{normalized}-", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var suffixToken = name[(normalized.Length + 1)..];
            if (int.TryParse(suffixToken, out var suffixValue) && suffixValue > 0)
            {
                usedSuffixes.Add(suffixValue);
            }
        }

        var candidateIndex = 1;
        while (usedSuffixes.Contains(candidateIndex))
        {
            candidateIndex++;
        }

        var candidateName = $"{normalized}-{candidateIndex}";
        occupiedNames.Add(candidateName);
        return candidateName;
    }

    private static string ExtractCloneBaseName(string sourceName)
    {
        var normalized = sourceName.Trim();
        var separatorIndex = normalized.LastIndexOf("-", StringComparison.Ordinal);
        if (separatorIndex <= 0 || separatorIndex >= normalized.Length - 1)
        {
            return normalized;
        }

        var suffixToken = normalized[(separatorIndex + 1)..];
        return int.TryParse(suffixToken, out _) ? normalized[..separatorIndex] : normalized;
    }

    private static bool ContainsNode(AssetNode root, AssetNode targetNode)
    {
        if (ReferenceEquals(root, targetNode))
        {
            return true;
        }

        foreach (var child in root.Children)
        {
            if (ContainsNode(child, targetNode))
            {
                return true;
            }
        }

        return false;
    }

    private static FolderNode? FindParentFolder(IEnumerable<AssetNode> nodes, AssetNode targetNode)
    {
        foreach (var node in nodes)
        {
            if (node.Children.Contains(targetNode))
            {
                return node as FolderNode;
            }

            var found = FindParentFolder(node.Children, targetNode);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static bool TryCollectAncestorFolders(
        IEnumerable<AssetNode> nodes,
        AssetNode targetNode,
        ICollection<FolderNode> ancestors)
    {
        foreach (var node in nodes)
        {
            if (ReferenceEquals(node, targetNode))
            {
                return true;
            }

            if (!TryCollectAncestorFolders(node.Children, targetNode, ancestors))
            {
                continue;
            }

            if (node is FolderNode folder)
            {
                ancestors.Add(folder);
            }

            return true;
        }

        return false;
    }

    private void CancelAllRenameStates(AssetNode? exceptNode = null)
    {
        foreach (var node in Flatten(CurrentAssetTree))
        {
            if (ReferenceEquals(node, exceptNode))
            {
                continue;
            }

            if (!node.IsRenaming)
            {
                continue;
            }

            node.EditName = node.Name;
            node.IsRenaming = false;
        }
    }

    private void CreateFolderCore(AssetNode? sourceNode)
    {
        var targetNode = ResolveInsertionParent(sourceNode);
        var newNode = new FolderNode(
            $"folder-{Guid.NewGuid():N}",
            $"New Folder {_newFolderSeed++}",
            "Folder");

        InsertNode(targetNode, newNode);
        LastAssetActionMessage = $"Created folder {newNode.Name}";
    }

    private void CreateSshConnectionCore(AssetNode? sourceNode)
    {
        var targetNode = ResolveInsertionParent(sourceNode);
        var seed = _newConnectionSeed++;
        var newNode = new ConnectionNode(
            $"conn-{Guid.NewGuid():N}",
            $"ssh-{seed:D2}",
            $"10.0.0.{seed + 20}",
            "root",
            22,
            "SSH Connection");

        InsertNode(targetNode, newNode);
        LastAssetActionMessage = $"Created SSH connection {newNode.Name}";
    }

    private void CreateCredentialAssetCore(AssetNode? sourceNode, string credentialType)
    {
        var targetNode = ResolveInsertionParent(sourceNode);
        var seed = _newConnectionSeed++;
        var keyLabelPrefix = credentialType.Equals("Password", StringComparison.OrdinalIgnoreCase)
            ? "password"
            : credentialType.Contains("Key", StringComparison.OrdinalIgnoreCase) ? "key" : "credential";
        var newNode = new ConnectionNode(
            $"credential-{Guid.NewGuid():N}",
            $"{keyLabelPrefix}-{seed:D2}",
            "vault-local",
            "security",
            443,
            credentialType);

        InsertNode(targetNode, newNode);
        LastAssetActionMessage = $"已创建 {credentialType} 资产";
    }

    private void InsertNode(AssetNode? parentNode, AssetNode newNode)
    {
        if (parentNode is null)
        {
            CurrentAssetTree.Add(newNode);
        }
        else
        {
            parentNode.Children.Add(newNode);
            parentNode.IsExpanded = true;
        }

        SelectedAssetNode = newNode;
        ApplyAssetsSearchFilter();
    }

    private AssetNode? ResolveInsertionParent(AssetNode? sourceNode)
    {
        var targetNode = ResolveTargetNode(sourceNode);
        return targetNode is FolderNode ? targetNode : null;
    }

    private void ApplyAssetsSearchFilter()
    {
        var keyword = AssetsSearchText.Trim();

        foreach (var node in CurrentAssetTree)
        {
            ApplyNodeVisibility(node, keyword);
        }

        AreAllTreeFoldersExpanded = AreAllFoldersExpanded(CurrentAssetTree);
        RebuildCurrentFlatList();
    }

    private static bool ApplyNodeVisibility(AssetNode node, string keyword)
    {
        var isFilteredSearch = !string.IsNullOrWhiteSpace(keyword);
        var isSelfMatch = node.Matches(keyword);
        var hasMatchedChild = false;

        foreach (var child in node.Children)
        {
            if (ApplyNodeVisibility(child, keyword))
            {
                hasMatchedChild = true;
            }
        }

        node.IsVisible = !isFilteredSearch || isSelfMatch || hasMatchedChild;
        if (isFilteredSearch && node.IsFolder)
        {
            node.IsExpanded = hasMatchedChild;
        }

        return node.IsVisible;
    }

    private void RebuildCurrentFlatList()
    {
        CurrentAssetFlatList.Clear();
        foreach (var node in Flatten(CurrentAssetTree).Where(node => node.IsVisible))
        {
            CurrentAssetFlatList.Add(node);
        }

        if (SelectedAssetNodes.Count == 0)
        {
            return;
        }

        var visibleNodes = CurrentAssetFlatList.ToHashSet();
        var hasRemovedSelection = false;
        for (var i = SelectedAssetNodes.Count - 1; i >= 0; i--)
        {
            if (visibleNodes.Contains(SelectedAssetNodes[i]))
            {
                continue;
            }

            SelectedAssetNodes.RemoveAt(i);
            hasRemovedSelection = true;
        }

        if (hasRemovedSelection)
        {
            NotifyAssetCommandStateChanged();
        }
    }

    private static void SetFolderExpansion(IEnumerable<AssetNode> nodes, bool isExpanded)
    {
        foreach (var node in nodes)
        {
            if (node.IsFolder)
            {
                node.IsExpanded = isExpanded;
            }

            SetFolderExpansion(node.Children, isExpanded);
        }
    }

    private static bool AreAllFoldersExpanded(IEnumerable<AssetNode> nodes)
    {
        var folders = Flatten(nodes).Where(node => node.IsFolder).ToList();
        if (folders.Count == 0)
        {
            return false;
        }

        return folders.All(node => node.IsExpanded);
    }

    private static IEnumerable<AssetNode> Flatten(IEnumerable<AssetNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in Flatten(node.Children))
            {
                yield return child;
            }
        }
    }

    private static Dictionary<AssetsPaneKind, ObservableCollection<AssetNode>> BuildAssetsMap(
        Dictionary<AssetsPaneKind, List<AssetNode>> source)
    {
        var result = source.ToDictionary(
            pair => pair.Key,
            pair => new ObservableCollection<AssetNode>(pair.Value));

        foreach (var paneKind in Enum.GetValues<AssetsPaneKind>())
        {
            if (!result.ContainsKey(paneKind))
            {
                result[paneKind] = [];
            }
        }

        return result;
    }

    private static bool TryRemoveNode(ICollection<AssetNode> nodes, AssetNode targetNode)
    {
        if (nodes.Remove(targetNode))
        {
            return true;
        }

        foreach (var node in nodes.ToList())
        {
            if (TryRemoveNode(node.Children, targetNode))
            {
                return true;
            }
        }

        return false;
    }

    private void ClearSelectedAssets()
    {
        if (SelectedAssetNodes.Count == 0)
        {
            return;
        }

        SelectedAssetNodes.Clear();
        NotifyAssetCommandStateChanged();
    }

    private void SyncActiveWorkspaceTabs()
    {
        foreach (var pane in WorkspacePanes)
        {
            foreach (var tab in pane.Tabs)
            {
                tab.IsActive = ReferenceEquals(tab, SelectedWorkspaceTab);
            }
        }
    }
}
