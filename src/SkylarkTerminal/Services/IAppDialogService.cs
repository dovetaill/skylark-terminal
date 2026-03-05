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
}
