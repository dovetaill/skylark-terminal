using System;
using System.IO;

namespace SkylarkTerminal.Tests;

public class RightModeViewsBindingTests
{
    [Theory]
    [InlineData("SnippetsModeView.axaml", "ActiveRightMode.VisibleCategories")]
    [InlineData("HistoryModeView.axaml", "HistoryItems")]
    [InlineData("SftpModeView.axaml", "ActiveRightMode.VisibleItems")]
    public void ModeViews_ShouldBindExpectedCollections(string file, string bindingKey)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var path = Path.Combine(root, "src", "SkylarkTerminal", "Views", "RightModes", file);
        Assert.True(File.Exists(path));
        Assert.Contains(bindingKey, File.ReadAllText(path));
    }
}
