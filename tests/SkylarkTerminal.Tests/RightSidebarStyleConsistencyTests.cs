using System;
using System.IO;

namespace SkylarkTerminal.Tests;

public class RightSidebarStyleConsistencyTests
{
    [Fact]
    public void MainWindow_ShouldDefineRightSidebarSharedStyleTokens()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var path = Path.Combine(root, "src", "SkylarkTerminal", "Views", "MainWindow.axaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("RightSidebarModeButton", xaml);
        Assert.Contains("RightSidebarActionButton", xaml);
    }
}
