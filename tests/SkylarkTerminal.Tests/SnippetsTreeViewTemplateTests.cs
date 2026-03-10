using System;
using System.IO;

namespace SkylarkTerminal.Tests;

public class SnippetsTreeViewTemplateTests
{
    [Fact]
    public void SnippetsBrowse_ShouldUseTreeView_AndDropCardLayout()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var path = Path.Combine(root, "src", "SkylarkTerminal", "Views", "RightModes", "SnippetsModeView.axaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("<TreeView", xaml, StringComparison.Ordinal);
        Assert.Contains("TreeDataTemplate DataType=\"models:SnippetCategory\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("RightSidebarSnippetCard", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("RightSidebarSnippetActionBand", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Items.Count", xaml, StringComparison.Ordinal);
    }
}
