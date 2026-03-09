using System;
using System.IO;

namespace SkylarkTerminal.Tests;

public class RightSidebarStyleConsistencyTests
{
    [Fact]
    public void MainWindow_ShouldDefineGhostTileAndSftpRowStyleTokens()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var path = Path.Combine(root, "src", "SkylarkTerminal", "Views", "MainWindow.axaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("RightSidebarModeSelectedBackgroundBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("RightSidebarModeSelectedForegroundBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("RightSidebarModeHoverBackgroundBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("RightSidebarSnippetCardBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("RightSidebarSnippetCardHoverBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("RightSidebarSnippetActionBandBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("RightSidebarSftpRowHoverBackgroundBrush", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Selector=\"ui|CommandBar.RightSidebarSftpCommandBar\"", xaml, StringComparison.Ordinal);
        Assert.Contains("CornerRadius\" Value=\"8\"", xaml, StringComparison.Ordinal);
    }
}
