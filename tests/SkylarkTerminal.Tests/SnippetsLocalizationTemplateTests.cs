using System;
using System.IO;

namespace SkylarkTerminal.Tests;

public class SnippetsLocalizationTemplateTests
{
    [Fact]
    public void SnippetsModeView_ShouldReferenceSnippetsText_AndDropEnglishLabels()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var path = Path.Combine(root, "src", "SkylarkTerminal", "Views", "RightModes", "SnippetsModeView.axaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("SnippetsText.", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Create Snippet", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Edit Snippet", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("View More", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Run in all tabs", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void AppDialogService_ShouldUseChineseSnippetsCopy()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var path = Path.Combine(root, "src", "SkylarkTerminal", "Services", "AppDialogService.cs");
        var code = File.ReadAllText(path);

        Assert.Contains("SnippetsText.", code, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Delete snippet\"", code, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Run in all tabs\"", code, StringComparison.Ordinal);
    }
}
