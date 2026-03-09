using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkylarkTerminal.Models;
using SkylarkTerminal.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace SkylarkTerminal.ViewModels.RightPanelModes;

public sealed partial class SftpModeViewModel : ObservableObject, IRightPanelModeViewModel
{
    private readonly ISftpService _sftpService;
    private readonly ISftpNavigationService _navigationService;
    private string? _activeConnectionId;
    private string _addressInput;

    [ObservableProperty]
    private bool isAddressEditorExpanded;

    [ObservableProperty]
    private SftpHeaderOverlayMode headerOverlayMode;

    [ObservableProperty]
    private SftpPanelLoadState loadState = SftpPanelLoadState.Idle;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool showHiddenFiles;

    [ObservableProperty]
    private string searchQuery = string.Empty;

    public SftpModeViewModel(
        ISftpService sftpService,
        ISftpNavigationService? navigationService = null,
        IReadOnlyList<ModeActionDescriptor>? actions = null)
    {
        _sftpService = sftpService;
        _navigationService = navigationService ?? new SftpNavigationService("/");
        _addressInput = _navigationService.CurrentPath;
        Actions = actions ?? [];

        BackCommand = new AsyncRelayCommand(GoBackAsync);
        ForwardCommand = new AsyncRelayCommand(GoForwardAsync);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        UpCommand = new AsyncRelayCommand(GoUpAsync);
        ExpandAddressEditorCommand = new RelayCommand(OpenAddressOverlay);
        CollapseAddressEditorCommand = new RelayCommand(() =>
        {
            AddressInput = CurrentPath;
            IsAddressEditorExpanded = false;
            HeaderOverlayMode = SftpHeaderOverlayMode.None;
        });
        AddressCommitCommand = new AsyncRelayCommand(CommitAddressAsync);
        OpenAddressOverlayCommand = new RelayCommand(OpenAddressOverlay);
        OpenSearchOverlayCommand = new RelayCommand(() =>
        {
            IsAddressEditorExpanded = false;
            HeaderOverlayMode = SftpHeaderOverlayMode.Search;
        });
        CloseHeaderOverlayCommand = new RelayCommand(() =>
        {
            HeaderOverlayMode = SftpHeaderOverlayMode.None;
            IsAddressEditorExpanded = false;
        });
        ToggleShowHiddenFilesCommand = new RelayCommand(() => ShowHiddenFiles = !ShowHiddenFiles);
        NavigateHistoryPathCommand = new AsyncRelayCommand<string?>(NavigateHistoryPathAsync);

        LeadingCommands =
        [
            new("sftp.back", "\uE72B", "后退", "后退", BackCommand),
            new("sftp.forward", "\uE72A", "前进", "前进", ForwardCommand),
        ];
        TrailingCommands =
        [
            new("sftp.refresh", "\uE72C", "刷新", "刷新", RefreshCommand),
            new("sftp.up", "\uE74A", "上一级", "上一级", UpCommand),
        ];
        MoreCommands =
        [
            new("sftp.copy-path", "\uE8C8", "复制当前路径", "复制当前路径", new RelayCommand(() => { })),
            new("sftp.open-in-tab", "\uE8A5", "在新标签打开", "在新标签打开", new RelayCommand(() => { })),
            new("sftp.show-hidden", "\uE890", "显示隐藏文件", "显示隐藏文件", new RelayCommand(() => { })),
        ];
    }

    public RightToolsViewKind Kind => RightToolsViewKind.Sftp;

    public string Title => "SFTP";

    public string Glyph => "\uE8B7";

    public RightPanelHeaderNode HeaderNode { get; } = new SftpToolbarRightPanelHeader();

    public RightToolsContentNode ContentNode { get; } = new SftpRightToolsContent();

    public IReadOnlyList<ModeActionDescriptor> Actions { get; }

    public IReadOnlyList<SftpToolbarActionDescriptor> LeadingCommands { get; }

    public IReadOnlyList<SftpToolbarActionDescriptor> TrailingCommands { get; }

    public IReadOnlyList<SftpToolbarActionDescriptor> MoreCommands { get; }

    public ObservableCollection<RemoteFileNode> Items { get; } = [];

    public ObservableCollection<RemoteFileNode> VisibleItems { get; } = [];

    public bool IsIdleState => LoadState == SftpPanelLoadState.Idle;

    public bool IsLoadingState => LoadState == SftpPanelLoadState.Loading;

    public bool IsLoadedState => LoadState == SftpPanelLoadState.Loaded;

    public bool IsEmptyState => LoadState == SftpPanelLoadState.Empty;

    public bool IsErrorState => LoadState == SftpPanelLoadState.Error;

    public IRelayCommand BackCommand { get; }

    public IRelayCommand ForwardCommand { get; }

    public IRelayCommand RefreshCommand { get; }

    public IRelayCommand UpCommand { get; }

    public IRelayCommand ExpandAddressEditorCommand { get; }

    public IRelayCommand CollapseAddressEditorCommand { get; }

    public IRelayCommand AddressCommitCommand { get; }

    public IRelayCommand OpenAddressOverlayCommand { get; }

    public IRelayCommand OpenSearchOverlayCommand { get; }

    public IRelayCommand CloseHeaderOverlayCommand { get; }

    public IRelayCommand ToggleShowHiddenFilesCommand { get; }

    public IAsyncRelayCommand<string?> NavigateHistoryPathCommand { get; }

    public bool IsAddressChipVisible => !IsAddressEditorExpanded;

    public bool IsHeaderOverlayVisible => HeaderOverlayMode != SftpHeaderOverlayMode.None;

    public bool IsHeaderUtilityStripVisible => HeaderOverlayMode == SftpHeaderOverlayMode.None;

    public bool IsAddressOverlayVisible => HeaderOverlayMode == SftpHeaderOverlayMode.Address;

    public bool IsSearchOverlayVisible => HeaderOverlayMode == SftpHeaderOverlayMode.Search;

    public string AddressInput
    {
        get => _addressInput;
        set => SetProperty(ref _addressInput, value);
    }

    public string CurrentPath => _navigationService.CurrentPath;

    public bool CanGoBack => _navigationService.CanGoBack;

    public bool CanGoForward => _navigationService.CanGoForward;

    public IReadOnlyList<string> RecentPaths => _navigationService.RecentPaths;

    public string NavigateTo(string path) => _navigationService.NavigateTo(path);

    public string GoBack() => _navigationService.GoBack();

    public string GoForward() => _navigationService.GoForward();

    public string GoUp() => _navigationService.GoUp();

    public string Refresh() => _navigationService.Refresh();

    public string CommitAddress(string input) => _navigationService.TryResolveAddressInput(input);

    public async Task ActivateAsync(string connectionId)
    {
        _activeConnectionId = connectionId;
        await LoadDirectoryAsync(_navigationService.CurrentPath);
    }

    public async Task CommitAddressAsync()
    {
        CommitAddress(AddressInput);
        await LoadDirectoryAsync(CurrentPath);
        IsAddressEditorExpanded = false;
        HeaderOverlayMode = SftpHeaderOverlayMode.None;
    }

    public async Task NavigateHistoryPathAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        NavigateTo(path);
        await LoadDirectoryAsync(CurrentPath);
    }

    partial void OnIsAddressEditorExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsAddressChipVisible));
    }

    partial void OnHeaderOverlayModeChanged(SftpHeaderOverlayMode value)
    {
        _ = value;
        OnPropertyChanged(nameof(IsHeaderOverlayVisible));
        OnPropertyChanged(nameof(IsHeaderUtilityStripVisible));
        OnPropertyChanged(nameof(IsAddressOverlayVisible));
        OnPropertyChanged(nameof(IsSearchOverlayVisible));
    }

    partial void OnLoadStateChanged(SftpPanelLoadState value)
    {
        _ = value;
        OnPropertyChanged(nameof(IsIdleState));
        OnPropertyChanged(nameof(IsLoadingState));
        OnPropertyChanged(nameof(IsLoadedState));
        OnPropertyChanged(nameof(IsEmptyState));
        OnPropertyChanged(nameof(IsErrorState));
    }

    partial void OnShowHiddenFilesChanged(bool value)
    {
        _ = value;
        RebuildVisibleItems();
    }

    partial void OnSearchQueryChanged(string value)
    {
        _ = value;
        RebuildVisibleItems();
    }

    private async Task GoBackAsync()
    {
        GoBack();
        await LoadDirectoryAsync(CurrentPath);
    }

    private async Task GoForwardAsync()
    {
        GoForward();
        await LoadDirectoryAsync(CurrentPath);
    }

    private async Task RefreshAsync()
    {
        Refresh();
        await LoadDirectoryAsync(CurrentPath);
    }

    private async Task GoUpAsync()
    {
        GoUp();
        await LoadDirectoryAsync(CurrentPath);
    }

    private async Task LoadDirectoryAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(_activeConnectionId))
        {
            Items.Clear();
            VisibleItems.Clear();
            ErrorMessage = "Missing connection context.";
            LoadState = SftpPanelLoadState.Error;
            SyncAddressAndFlags();
            return;
        }

        LoadState = SftpPanelLoadState.Loading;
        ErrorMessage = null;

        try
        {
            var nodes = await _sftpService.ListDirectoryAsync(_activeConnectionId, path);
            Items.Clear();

            foreach (var node in nodes)
            {
                Items.Add(node);
            }

            RebuildVisibleItems();

            LoadState = Items.Count == 0
                ? SftpPanelLoadState.Empty
                : SftpPanelLoadState.Loaded;
        }
        catch (Exception ex)
        {
            Items.Clear();
            VisibleItems.Clear();
            ErrorMessage = ex.Message;
            LoadState = SftpPanelLoadState.Error;
        }
        finally
        {
            SyncAddressAndFlags();
        }
    }

    private void SyncAddressAndFlags()
    {
        AddressInput = _navigationService.CurrentPath;
        OnPropertyChanged(nameof(CurrentPath));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoForward));
        OnPropertyChanged(nameof(RecentPaths));
    }

    private void OpenAddressOverlay()
    {
        IsAddressEditorExpanded = true;
        HeaderOverlayMode = SftpHeaderOverlayMode.Address;
    }

    private void RebuildVisibleItems()
    {
        VisibleItems.Clear();

        foreach (var item in Items.Where(ShouldIncludeItem))
        {
            VisibleItems.Add(item);
        }
    }

    private bool ShouldIncludeItem(RemoteFileNode item)
    {
        if (!ShowHiddenFiles && item.IsHidden)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            return true;
        }

        return item.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)
            || item.FullPath.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase);
    }
}
