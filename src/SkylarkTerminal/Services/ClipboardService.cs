using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using System.Threading.Tasks;

namespace SkylarkTerminal.Services;

public sealed class ClipboardService : IClipboardService
{
    public async Task SetTextAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var clipboard = ResolveClipboard();
        if (clipboard is null)
        {
            return;
        }

        await clipboard.SetTextAsync(text);
    }

    public async Task<string?> GetTextAsync()
    {
        var clipboard = ResolveClipboard();
        if (clipboard is null)
        {
            return null;
        }

#pragma warning disable CS0618
        return await clipboard.GetTextAsync();
#pragma warning restore CS0618
    }

    private static IClipboard? ResolveClipboard()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return null;
        }

        if (desktop.MainWindow is null)
        {
            return null;
        }

        var topLevel = TopLevel.GetTopLevel(desktop.MainWindow) ?? desktop.MainWindow;
        return topLevel.Clipboard;
    }
}
