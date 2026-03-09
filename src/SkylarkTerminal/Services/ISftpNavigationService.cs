namespace SkylarkTerminal.Services;

using System.Collections.Generic;

public interface ISftpNavigationService
{
    string CurrentPath { get; }

    bool CanGoBack { get; }

    bool CanGoForward { get; }

    IReadOnlyList<string> RecentPaths { get; }

    string NavigateTo(string path);

    string GoBack();

    string GoForward();

    string GoUp();

    string Refresh();

    string TryResolveAddressInput(string input);
}
