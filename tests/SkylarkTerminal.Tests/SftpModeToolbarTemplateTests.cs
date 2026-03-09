using System;
using System.IO;

namespace SkylarkTerminal.Tests;

public class SftpModeToolbarTemplateTests
{
    [Fact]
    public void SftpHeader_ShouldUseCommandBar_And_SftpModeView_ShouldNotInlineToolbar()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var headerPath = Path.Combine(root, "src", "SkylarkTerminal", "Views", "RightHeaders", "SftpCommandBarHeaderView.axaml");
        var modePath = Path.Combine(root, "src", "SkylarkTerminal", "Views", "RightModes", "SftpModeView.axaml");

        var headerXaml = File.ReadAllText(headerPath);
        var modeXaml = File.ReadAllText(modePath);

        Assert.Contains("<ui:CommandBar", headerXaml, StringComparison.Ordinal);
        Assert.Contains("<ui:CommandBarElementContainer", headerXaml, StringComparison.Ordinal);
        Assert.Contains("BackCommand", headerXaml, StringComparison.Ordinal);
        Assert.Contains("ForwardCommand", headerXaml, StringComparison.Ordinal);
        Assert.Contains("RefreshCommand", headerXaml, StringComparison.Ordinal);
        Assert.Contains("UpCommand", headerXaml, StringComparison.Ordinal);

        Assert.DoesNotContain("ToolTip.Tip=\"Back\"", modeXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ToolTip.Tip=\"Forward\"", modeXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Watermark=\"/\"", modeXaml, StringComparison.Ordinal);
    }
}
