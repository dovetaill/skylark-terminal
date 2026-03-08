using System;
using System.IO;

namespace SkylarkTerminal.Tests;

public class SftpModeToolbarTemplateTests
{
    [Fact]
    public void SftpModeView_ShouldContainBackForwardAddressRefreshUp()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var path = Path.Combine(root, "src", "SkylarkTerminal", "Views", "RightModes", "SftpModeView.axaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("ToolTip.Tip=\"Back\"", xaml);
        Assert.Contains("ToolTip.Tip=\"Forward\"", xaml);
        Assert.Contains("Watermark=\"/\"", xaml);
        Assert.Contains("ToolTip.Tip=\"Refresh\"", xaml);
        Assert.Contains("ToolTip.Tip=\"Up\"", xaml);
    }
}
