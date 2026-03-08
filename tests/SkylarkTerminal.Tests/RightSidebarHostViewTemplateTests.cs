using System;
using System.IO;

namespace SkylarkTerminal.Tests;

public class RightSidebarHostViewTemplateTests
{
    [Fact]
    public void HostView_ShouldContainModeRail_ActionBar_AndTransitionContent()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var path = Path.Combine(root, "src", "SkylarkTerminal", "Views", "RightSidebarHostView.axaml");
        Assert.True(File.Exists(path));
        var xaml = File.ReadAllText(path);

        Assert.Contains("ItemsSource=\"{Binding RightToolsModeItems}\"", xaml);
        Assert.Contains("ItemsSource=\"{Binding ActiveRightMode.Actions}\"", xaml);
        Assert.Contains("<TransitioningContentControl", xaml);
    }
}
