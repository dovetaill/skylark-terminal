using System.Threading.Tasks;

namespace SkylarkTerminal.Services;

public interface IAppDialogService
{
    Task ShowSettingsAsync(string themeMode, bool isLeftAssetsPaneOpen, bool isRightToolsPaneOpen);

    Task<string?> ShowLanguagePickerAsync(string currentLanguageCode);

    Task ShowHelpAsync(string languageCode);

    Task ShowAboutAsync(string appTitle, string appVersion);
}
