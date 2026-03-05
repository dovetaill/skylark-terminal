using System.Threading.Tasks;

namespace SkylarkTerminal.Services.Mock;

public sealed class MockAppDialogService : IAppDialogService
{
    public Task ShowSettingsAsync(string themeMode, bool isLeftAssetsPaneOpen, bool isRightToolsPaneOpen)
    {
        return Task.CompletedTask;
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
}
