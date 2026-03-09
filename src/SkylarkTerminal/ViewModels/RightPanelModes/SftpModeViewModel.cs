using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkylarkTerminal.Models;
using SkylarkTerminal.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    private SftpPanelLoadState loadState = SftpPanelLoadState.Idle;

    [ObservableProperty]
    private string? errorMessage;

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
        ExpandAddressEditorCommand = new RelayCommand(() => IsAddressEditorExpanded = true);
        CollapseAddressEditorCommand = new RelayCommand(() =>
        {
            AddressInput = CurrentPath;
            IsAddressEditorExpanded = false;
        });
        AddressCommitCommand = new AsyncRelayCommand(CommitAddressAsync);
    }

    public RightToolsViewKind Kind => RightToolsViewKind.Sftp;

    public string Title => "SFTP";

    public string Glyph => "\uE8B7";

    public RightPanelHeaderNode HeaderNode { get; } = new SftpCommandBarRightPanelHeader();

    public RightToolsContentNode ContentNode { get; } = new SftpRightToolsContent();

    public IReadOnlyList<ModeActionDescriptor> Actions { get; }

    public ObservableCollection<RemoteFileNode> Items { get; } = [];

    public IRelayCommand BackCommand { get; }

    public IRelayCommand ForwardCommand { get; }

    public IRelayCommand RefreshCommand { get; }

    public IRelayCommand UpCommand { get; }

    public IRelayCommand ExpandAddressEditorCommand { get; }

    public IRelayCommand CollapseAddressEditorCommand { get; }

    public IRelayCommand AddressCommitCommand { get; }

    public bool IsAddressChipVisible => !IsAddressEditorExpanded;

    public string AddressInput
    {
        get => _addressInput;
        set => SetProperty(ref _addressInput, value);
    }

    public string CurrentPath => _navigationService.CurrentPath;

    public bool CanGoBack => _navigationService.CanGoBack;

    public bool CanGoForward => _navigationService.CanGoForward;

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
    }

    partial void OnIsAddressEditorExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsAddressChipVisible));
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

            LoadState = Items.Count == 0
                ? SftpPanelLoadState.Empty
                : SftpPanelLoadState.Loaded;
        }
        catch (Exception ex)
        {
            Items.Clear();
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
    }
}
