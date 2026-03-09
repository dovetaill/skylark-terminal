using System.Threading.Tasks;

namespace SkylarkTerminal.Services.Mock;

public sealed class MockAppDialogService : IAppDialogService
{
    public bool DeleteAssetConfirmResult { get; set; } = true;

    public bool RunSnippetInAllTabsConfirmResult { get; set; } = true;

    public bool DeleteSnippetConfirmResult { get; set; } = true;

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
        _ = assetName;
        _ = isFolder;
        return Task.FromResult(DeleteAssetConfirmResult);
    }

    public Task<bool> ShowRunSnippetInAllTabsConfirmAsync(string snippetTitle, int targetCount)
    {
        _ = snippetTitle;
        _ = targetCount;
        return Task.FromResult(RunSnippetInAllTabsConfirmResult);
    }

    public Task<bool> ShowDeleteSnippetConfirmAsync(string snippetTitle)
    {
        _ = snippetTitle;
        return Task.FromResult(DeleteSnippetConfirmResult);
    }
}
