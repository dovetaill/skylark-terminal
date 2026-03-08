namespace SkylarkTerminal.Services;

public interface ISftpNavigationService
{
    string CurrentPath { get; }

    bool CanGoBack { get; }

    bool CanGoForward { get; }

    string NavigateTo(string path);

    string GoBack();

    string GoForward();

    string GoUp();

    string Refresh();

    string TryResolveAddressInput(string input);
}
