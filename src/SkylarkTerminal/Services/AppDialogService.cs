using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SkylarkTerminal.Services;

public sealed class AppDialogService : IAppDialogService
{
    public async Task<bool?> ShowSettingsAsync(
        string themeMode,
        bool isLeftAssetsPaneOpen,
        bool isRightSidebarVisible,
        bool isShellTransparent)
    {
        var host = ResolveHostWindow();
        if (host is null)
        {
            return null;
        }

        var content = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = $"Theme: {themeMode}" },
                new TextBlock { Text = $"Left Assets Pane: {(isLeftAssetsPaneOpen ? "Open" : "Closed")}" },
                new TextBlock { Text = $"Right Sidebar: {(isRightSidebarVisible ? "Open" : "Closed")}" },
                new TextBlock { Text = $"Window Material: {(isShellTransparent ? "Transparent" : "Opaque")}" },
                new TextBlock
                {
                    Text = "Choose Transparent or Opaque shell style for this session.",
                    TextWrapping = TextWrapping.Wrap,
                },
            },
        };

        var dialog = new ContentDialog
        {
            Title = "Settings",
            Content = content,
            PrimaryButtonText = "Opaque",
            SecondaryButtonText = "Transparent",
            CloseButtonText = "Cancel",
            DefaultButton = isShellTransparent
                ? ContentDialogButton.Secondary
                : ContentDialogButton.Primary,
        };

        var result = await dialog.ShowAsync(host);
        return result switch
        {
            ContentDialogResult.Primary => false,
            ContentDialogResult.Secondary => true,
            _ => null,
        };
    }

    public async Task<string?> ShowLanguagePickerAsync(string currentLanguageCode)
    {
        var host = ResolveHostWindow();
        if (host is null)
        {
            return null;
        }

        var dialog = new ContentDialog
        {
            Title = "Language",
            Content = new TextBlock
            {
                Text = "Choose UI language for this session.",
                TextWrapping = TextWrapping.Wrap,
            },
            PrimaryButtonText = "中文",
            SecondaryButtonText = "English",
            CloseButtonText = "Cancel",
            DefaultButton = currentLanguageCode == "en-US"
                ? ContentDialogButton.Secondary
                : ContentDialogButton.Primary,
        };

        var result = await dialog.ShowAsync(host);
        return result switch
        {
            ContentDialogResult.Primary => "zh-CN",
            ContentDialogResult.Secondary => "en-US",
            _ => null,
        };
    }

    public Task ShowHelpAsync(string languageCode)
    {
        IReadOnlyList<string> tips = languageCode == "zh-CN"
            ?
            [
                "Ctrl+T: 新建终端标签",
                "Ctrl+W: 关闭当前标签",
                "Ctrl+F: 聚焦全局搜索",
                "右键标签页: 关闭其他/关闭右侧/复制标签",
            ]
            :
            [
                "Ctrl+T: create terminal tab",
                "Ctrl+W: close current tab",
                "Ctrl+F: focus global search",
                "Right click tab: close others/close right/duplicate tab",
            ];

        var panel = new StackPanel { Spacing = 6 };
        foreach (var tip in tips)
        {
            panel.Children.Add(new TextBlock { Text = $"• {tip}" });
        }

        return ShowSimpleDialogAsync("Help", panel);
    }

    public Task ShowAboutAsync(string appTitle, string appVersion)
    {
        var content = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = appTitle },
                new TextBlock { Text = $"Version: {appVersion}" },
                new TextBlock { Text = ".NET 10 + Avalonia 11 + FluentAvalonia 2.5" },
            },
        };

        return ShowSimpleDialogAsync("About", content);
    }

    public async Task<bool> ShowDeleteAssetConfirmAsync(string assetName, bool isFolder)
    {
        var host = ResolveHostWindow();
        if (host is null)
        {
            return false;
        }

        var dialog = new ContentDialog
        {
            Title = "删除确认",
            Content = new TextBlock
            {
                Text = isFolder
                    ? $"确定删除目录“{assetName}”及其子节点吗？"
                    : $"确定删除连接“{assetName}”吗？",
                TextWrapping = TextWrapping.Wrap,
            },
            PrimaryButtonText = "删除",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
        };

        var result = await dialog.ShowAsync(host);
        return result == ContentDialogResult.Primary;
    }

    public Task<bool> ShowRunSnippetInAllTabsConfirmAsync(string snippetTitle, int targetCount)
    {
        return ShowConfirmationAsync(
            "Run in all tabs",
            $"Run snippet \"{snippetTitle}\" in {targetCount} connected SSH tab(s)?");
    }

    public Task<bool> ShowDeleteSnippetConfirmAsync(string snippetTitle)
    {
        return ShowConfirmationAsync(
            "Delete snippet",
            $"Delete snippet \"{snippetTitle}\"?");
    }

    private static async Task ShowSimpleDialogAsync(string title, Control content)
    {
        var host = ResolveHostWindow();
        if (host is null)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Close,
        };

        await dialog.ShowAsync(host);
    }

    private static async Task<bool> ShowConfirmationAsync(string title, string message)
    {
        var host = ResolveHostWindow();
        if (host is null)
        {
            return false;
        }

        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
            },
            PrimaryButtonText = "Confirm",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };

        var result = await dialog.ShowAsync(host);
        return result == ContentDialogResult.Primary;
    }

    private static Window? ResolveHostWindow()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return null;
        }

        if (desktop.MainWindow is not null)
        {
            return desktop.MainWindow;
        }

        return desktop.Windows.Count > 0 ? desktop.Windows[0] : null;
    }
}
