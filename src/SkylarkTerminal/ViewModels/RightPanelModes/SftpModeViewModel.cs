using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkylarkTerminal.Models;
using SkylarkTerminal.Services;
using System.Collections.Generic;

namespace SkylarkTerminal.ViewModels.RightPanelModes;

public sealed partial class SftpModeViewModel : ObservableObject, IRightPanelModeViewModel
{
    private readonly ISftpNavigationService _navigationService;
    private string _addressInput;
    
    [ObservableProperty]
    private bool isAddressEditorExpanded;

    public SftpModeViewModel(
        ISftpNavigationService? navigationService = null,
        IReadOnlyList<ModeActionDescriptor>? actions = null)
    {
        _navigationService = navigationService ?? new SftpNavigationService("/");
        _addressInput = _navigationService.CurrentPath;
        Actions = actions ?? [];

        BackCommand = new RelayCommand(() =>
        {
            GoBack();
            SyncAddressAndFlags();
        });
        ForwardCommand = new RelayCommand(() =>
        {
            GoForward();
            SyncAddressAndFlags();
        });
        RefreshCommand = new RelayCommand(() =>
        {
            Refresh();
            SyncAddressAndFlags();
        });
        UpCommand = new RelayCommand(() =>
        {
            GoUp();
            SyncAddressAndFlags();
        });
        ExpandAddressEditorCommand = new RelayCommand(() => IsAddressEditorExpanded = true);
        CollapseAddressEditorCommand = new RelayCommand(() =>
        {
            AddressInput = CurrentPath;
            IsAddressEditorExpanded = false;
        });
        AddressCommitCommand = new RelayCommand(() =>
        {
            CommitAddress(AddressInput);
            SyncAddressAndFlags();
            IsAddressEditorExpanded = false;
        });
    }

    public RightToolsViewKind Kind => RightToolsViewKind.Sftp;

    public string Title => "SFTP";

    public string Glyph => "\uE8B7";

    public RightPanelHeaderNode HeaderNode { get; } = new SftpCommandBarRightPanelHeader();

    public RightToolsContentNode ContentNode { get; } = new SftpRightToolsContent();

    public IReadOnlyList<ModeActionDescriptor> Actions { get; }

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

    partial void OnIsAddressEditorExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsAddressChipVisible));
    }

    private void SyncAddressAndFlags()
    {
        AddressInput = _navigationService.CurrentPath;
        OnPropertyChanged(nameof(CurrentPath));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoForward));
    }
}
