using System;
using System.IO;

namespace SkylarkTerminal.Tests;

public class SftpModeToolbarTemplateTests
{
    [Fact]
    public void SftpHeader_ShouldUseGridToolbar_And_NotDependOnCommandBar()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var path = Path.Combine(root, "src", "SkylarkTerminal", "Views", "RightHeaders", "SftpToolbarHeaderView.axaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("<Grid", xaml, StringComparison.Ordinal);
        Assert.Contains("Button.Flyout", xaml, StringComparison.Ordinal);
        Assert.Contains("ToolTip.Tip=\"{Binding TooltipZh}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<ui:CommandBar", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("CommandBarElementContainer", xaml, StringComparison.Ordinal);
    }
}
