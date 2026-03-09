using System;
using System.IO;

namespace SkylarkTerminal.Tests;

public class RightSidebarHostViewTemplateTests
{
    [Fact]
    public void HostView_ShouldRenderActiveHeaderSlot_InsteadOfFixedActionItemsControl()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var path = Path.Combine(root, "src", "SkylarkTerminal", "Views", "RightSidebarHostView.axaml");
        Assert.True(File.Exists(path));
        var xaml = File.ReadAllText(path);

        Assert.Contains("Content=\"{Binding ActiveRightHeader}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("<rightHeaders:ActionStripHeaderView/>", xaml, StringComparison.Ordinal);
        Assert.Contains("<rightHeaders:SftpCommandBarHeaderView/>", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ItemsSource=\"{Binding ActiveRightMode.Actions}\"", xaml, StringComparison.Ordinal);
    }
}
