using System;
using System.IO;

namespace SkylarkTerminal.Tests;

public class SftpModeStateTemplateTests
{
    [Fact]
    public void SftpModeView_ShouldDefineStateSections_And_DualRowItemTemplate()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var path = Path.Combine(root, "src", "SkylarkTerminal", "Views", "RightModes", "SftpModeView.axaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("LoadState", xaml, StringComparison.Ordinal);
        Assert.Contains("Idle", xaml, StringComparison.Ordinal);
        Assert.Contains("Loading", xaml, StringComparison.Ordinal);
        Assert.Contains("Empty", xaml, StringComparison.Ordinal);
        Assert.Contains("Error", xaml, StringComparison.Ordinal);
        Assert.Contains("FilteredEmptyStatePanel", xaml, StringComparison.Ordinal);
        Assert.Contains("VisibleItems", xaml, StringComparison.Ordinal);
        Assert.Contains("RowDefinitions=\"Auto,Auto\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ToolTip.Tip=\"{Binding FullPath}\"", xaml, StringComparison.Ordinal);
    }
}
