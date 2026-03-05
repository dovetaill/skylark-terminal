using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkylarkTerminal.Models;
using SkylarkTerminal.Services;
using SkylarkTerminal.Services.Mock;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace SkylarkTerminal.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public const double ExpandedLeftAssetsPaneWidth = 300d;
    public const double ExpandedRightSidebarWidth = 340d;
    public const double DefaultAssetsPanelWidth = 320d;

    private static readonly Color[] WorkspaceAccentPalette =
    [
        Color.Parse("#0F6CBD"),
        Color.Parse("#2E7D32"),
        Color.Parse("#9C27B0"),
        Color.Parse("#C77800"),
        Color.Parse("#0B8F8F"),
    ];

    private readonly ISshConnectionService _sshConnectionService;
    private readonly ISftpService _sftpService;
    private readonly IAppDialogService _appDialogService;
    private readonly Dictionary<AssetsPaneKind, ObservableCollection<AssetNode>> _assetsByPane;
    private int _newFolderSeed = 1;
    private int _newConnectionSeed = 1;
    private int _newWorkspaceTabSeed = 1;

    public MainWindowViewModel()
        : this(
            new MockAssetCatalogService(),
            new MockSshConnectionService(),
            new MockSftpService(),
            new MockAppDialogService())
    {
    }

    public MainWindowViewModel(
        IAssetCatalogService assetCatalogService,
        ISshConnectionService sshConnectionService,
        ISftpService sftpService,
        IAppDialogService appDialogService)
    {
        _sshConnectionService = sshConnectionService;
        _sftpService = sftpService;
        _appDialogService = appDialogService;
        _assetsByPane = BuildAssetsMap(assetCatalogService.GetAssets());
        ApplyAssetsSearchFilter();
        RebuildCurrentFlatList();
        InitializeWorkspaceTabs();
        InitializeRightToolsData();
        SyncActiveWorkspaceTabs();
        TopStatusBar = new TopStatusBarViewModel(this);
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
    private RightToolsViewKind selectedRightToolsView = RightToolsViewKind.Snippets;

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

    public string AssetsExpandCollapseGlyph => AreAllTreeFoldersExpanded ? "\uE70D" : "\uE70E";

    public string AssetsExpandCollapseToolTip => AreAllTreeFoldersExpanded
        ? "收起全部"
        : "展开全部";

    public bool IsSnippetsView => SelectedRightToolsView == RightToolsViewKind.Snippets;

    public bool IsHistoryView => SelectedRightToolsView == RightToolsViewKind.History;

    public bool IsSftpView => SelectedRightToolsView == RightToolsViewKind.Sftp;

    public ObservableCollection<AssetNode> CurrentAssetFlatList { get; } = [];

    public ObservableCollection<AssetNode> CurrentAssetTree => _assetsByPane[SelectedAssetsPane];

    public ObservableCollection<WorkspaceTabItemViewModel> WorkspaceTabs { get; } = [];

    public ObservableCollection<CommandSnippet> SnippetItems { get; } = [];

    public ObservableCollection<CommandHistoryEntry> HistoryItems { get; } = [];

    public ObservableCollection<RemoteFileNode> SftpItems { get; } = [];

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
        SelectedAssetsPane = AssetsPaneKind.Hosts;
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
        Console.WriteLine($"[Assets] Create folder: {LastAssetActionMessage}");
    }

    [RelayCommand]
    private void CreateSshConnectionAsset()
    {
        CreateSshConnectionCore(null);
        Console.WriteLine($"[Assets] Create SSH connection: {LastAssetActionMessage}");
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
    private void DeleteAsset(AssetNode? sourceNode)
    {
        var targetNode = ResolveTargetNode(sourceNode);
        if (targetNode is null)
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
            ApplyAssetsSearchFilter();
        }
    }

    [RelayCommand]
    private void CreateWorkspaceTab()
    {
        var newTab = BuildWorkspaceTab("new-session");
        WorkspaceTabs.Add(newTab);
        SelectedWorkspaceTab = newTab;
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

        WorkspaceTabs.Clear();
        WorkspaceTabs.Add(targetTab);
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
            WorkspaceTabs.RemoveAt(i);
        }

        SelectedWorkspaceTab = targetTab;
    }

    [RelayCommand]
    private void CloseAllTabs()
    {
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
        OnPropertyChanged(nameof(CurrentAssetTree));
        OnPropertyChanged(nameof(AssetsPanelTitle));
        ApplyAssetsSearchFilter();
    }

    partial void OnAssetsViewModeChanged(AssetsViewMode value)
    {
        OnPropertyChanged(nameof(IsTreeViewMode));
        OnPropertyChanged(nameof(IsFlatViewMode));
        OnPropertyChanged(nameof(AssetsViewModeGlyph));
        OnPropertyChanged(nameof(AssetsViewModeToolTip));
    }

    partial void OnSelectedRightToolsViewChanged(RightToolsViewKind value)
    {
        OnPropertyChanged(nameof(IsSnippetsView));
        OnPropertyChanged(nameof(IsHistoryView));
        OnPropertyChanged(nameof(IsSftpView));
    }

    partial void OnSelectedWorkspaceTabChanged(WorkspaceTabItemViewModel? value)
    {
        SyncActiveWorkspaceTabs();
    }

    partial void OnCurrentLanguageCodeChanged(string value)
    {
        OnPropertyChanged(nameof(CurrentLanguageLabel));
    }

    partial void OnAssetsSearchTextChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            IsAssetsSearchOpen = true;
        }

        ApplyAssetsSearchFilter();
    }

    partial void OnAreAllTreeFoldersExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(AssetsExpandCollapseGlyph));
        OnPropertyChanged(nameof(AssetsExpandCollapseToolTip));
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

    private void InitializeWorkspaceTabs()
    {
        var first = BuildWorkspaceTab("prod-bastion");
        var second = BuildWorkspaceTab("stage-api");

        WorkspaceTabs.Add(first);
        WorkspaceTabs.Add(second);
        SelectedWorkspaceTab = first;
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

    private WorkspaceTabItemViewModel BuildWorkspaceTab(string connectionLabel)
    {
        var index = _newWorkspaceTabSeed++;
        var accentBrush = new SolidColorBrush(WorkspaceAccentPalette[(index - 1) % WorkspaceAccentPalette.Length]);

        return new WorkspaceTabItemViewModel(
            $"tab-{Guid.NewGuid():N}",
            $"Session {index}",
            connectionLabel,
            $"Terminal placeholder for {connectionLabel}",
            accentBrush);
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

    private void SyncActiveWorkspaceTabs()
    {
        foreach (var tab in WorkspaceTabs)
        {
            tab.IsActive = ReferenceEquals(tab, SelectedWorkspaceTab);
        }
    }
}
