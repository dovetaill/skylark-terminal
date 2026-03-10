using System;
using System.IO;

namespace SkylarkTerminal.Tests;

public class SnippetsTreeContextMenuTemplateTests
{
    [Fact]
    public void SnippetsTree_ShouldDefineRootCategoryAndItemMenus()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var path = Path.Combine(root, "src", "SkylarkTerminal", "Views", "RightModes", "SnippetsModeView.axaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("snippets-root-context-flyout", xaml, StringComparison.Ordinal);
        Assert.Contains("snippets-category-context-flyout", xaml, StringComparison.Ordinal);
        Assert.Contains("snippets-item-context-flyout", xaml, StringComparison.Ordinal);
        Assert.Contains("Tag=\"delete-snippet\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Tag=\"delete-category\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void SnippetsTree_CodeBehind_ShouldKeepDoubleTapAsPaste()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var path = Path.Combine(root, "src", "SkylarkTerminal", "Views", "RightModes", "SnippetsModeView.axaml.cs");
        var code = File.ReadAllText(path);

        Assert.Contains("await vm.PasteAsync(item);", code, StringComparison.Ordinal);
    }
}
