using System;
using System.IO;

namespace SkylarkTerminal.Tests;

public class SnippetsModeViewContextTemplateTests
{
    [Fact]
    public void RightSidebarHostView_ShouldForwardActiveRightMode_ToSnippetsView()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var path = Path.Combine(root, "src", "SkylarkTerminal", "Views", "RightSidebarHostView.axaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("<rightModes:SnippetsModeView", xaml, StringComparison.Ordinal);
        Assert.Contains(
            "DataContext=\"{Binding DataContext.ActiveRightMode, RelativeSource={RelativeSource AncestorType=views:RightSidebarHostView}}\"",
            xaml,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SnippetsModeViewCodeBehind_ShouldReadSnippetsModeViewModel_Directly()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var path = Path.Combine(root, "src", "SkylarkTerminal", "Views", "RightModes", "SnippetsModeView.axaml.cs");
        var code = File.ReadAllText(path);

        Assert.Contains("DataContext is not SnippetsModeViewModel", code, StringComparison.Ordinal);
        Assert.DoesNotContain("DataContext is not MainWindowViewModel", code, StringComparison.Ordinal);
    }
}
