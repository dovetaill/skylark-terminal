using System.Threading.Tasks;

namespace SkylarkTerminal.Services;

public interface IAppDialogService
{
    Task<bool?> ShowSettingsAsync(
        string themeMode,
        bool isLeftAssetsPaneOpen,
        bool isRightSidebarVisible,
        bool isShellTransparent);

    Task<string?> ShowLanguagePickerAsync(string currentLanguageCode);

    Task ShowHelpAsync(string languageCode);

    Task ShowAboutAsync(string appTitle, string appVersion);

    Task<bool> ShowDeleteAssetConfirmAsync(string assetName, bool isFolder);

    Task<bool> ShowRunSnippetInAllTabsConfirmAsync(string snippetTitle, int targetCount);

    Task<bool> ShowDeleteSnippetConfirmAsync(string snippetTitle);

    Task<bool> ShowDeleteSnippetCategoryConfirmAsync(string categoryName, int snippetCount);
}
