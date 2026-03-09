using System;
using System.IO;

namespace SkylarkTerminal.Tests;

public class SnippetsModeContextMenuTemplateTests
{
    [Fact]
    public void SnippetsBrowseSurface_ShouldExposeRootContextFlyout()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var path = Path.Combine(root, "src", "SkylarkTerminal", "Views", "RightModes", "SnippetsModeView.axaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("snippets-root-context-flyout", xaml, StringComparison.Ordinal);
        Assert.Contains("BeginCreateCategoryCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("BeginCreateFromClipboardCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("ClearFilterCommand", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void SnippetsEditorLayouts_ShouldDifferentiateCreateAndEditActions()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var path = Path.Combine(root, "src", "SkylarkTerminal", "Views", "RightModes", "SnippetsModeView.axaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("x:Name=\"CreateEditorFooter\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"EditEditorFooter\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ViewMoreHeader\"", xaml, StringComparison.Ordinal);
    }
}
