using System;
using System.IO;

namespace SkylarkTerminal.Tests;

public class RightSidebarContentTransitionTests
{
    [Fact]
    public void RightSidebarHostView_ShouldUseStaticContentHost_ForModeContent()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var path = Path.Combine(root, "src", "SkylarkTerminal", "Views", "RightSidebarHostView.axaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("<ContentControl Grid.Row=\"2\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<TransitioningContentControl", xaml, StringComparison.Ordinal);
    }
}
