using System.Threading.Tasks;

namespace SkylarkTerminal.Services.Mock;

public sealed class MockAppDialogService : IAppDialogService
{
    public Task<bool?> ShowSettingsAsync(
        string themeMode,
        bool isLeftAssetsPaneOpen,
        bool isRightSidebarVisible,
        bool isShellTransparent)
    {
        return Task.FromResult<bool?>(isShellTransparent);
    }

    public Task<string?> ShowLanguagePickerAsync(string currentLanguageCode)
    {
        return Task.FromResult<string?>(currentLanguageCode);
    }

    public Task ShowHelpAsync(string languageCode)
    {
        return Task.CompletedTask;
    }

    public Task ShowAboutAsync(string appTitle, string appVersion)
    {
        return Task.CompletedTask;
    }

    public Task<bool> ShowDeleteAssetConfirmAsync(string assetName, bool isFolder)
    {
        return Task.FromResult(true);
    }
}
