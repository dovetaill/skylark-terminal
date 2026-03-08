using SkylarkTerminal.Models;
using SkylarkTerminal.Services;
using System.Collections.Generic;

namespace SkylarkTerminal.ViewModels.RightPanelModes;

public sealed class SftpModeViewModel : IRightPanelModeViewModel
{
    private readonly ISftpNavigationService _navigationService;

    public SftpModeViewModel(
        ISftpNavigationService? navigationService = null,
        IReadOnlyList<ModeActionDescriptor>? actions = null)
    {
        _navigationService = navigationService ?? new SftpNavigationService("/");
        Actions = actions ?? [];
    }

    public RightToolsViewKind Kind => RightToolsViewKind.Sftp;

    public string Title => "SFTP";

    public string Glyph => "\uE8B7";

    public RightToolsContentNode ContentNode { get; } = new SftpRightToolsContent();

    public IReadOnlyList<ModeActionDescriptor> Actions { get; }

    public string CurrentPath => _navigationService.CurrentPath;

    public bool CanGoBack => _navigationService.CanGoBack;

    public bool CanGoForward => _navigationService.CanGoForward;

    public string NavigateTo(string path) => _navigationService.NavigateTo(path);

    public string GoBack() => _navigationService.GoBack();

    public string GoForward() => _navigationService.GoForward();

    public string GoUp() => _navigationService.GoUp();

    public string Refresh() => _navigationService.Refresh();

    public string CommitAddress(string input) => _navigationService.TryResolveAddressInput(input);
}
