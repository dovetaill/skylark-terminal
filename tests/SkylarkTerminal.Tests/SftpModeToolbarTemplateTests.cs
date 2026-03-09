using System;
using System.IO;

namespace SkylarkTerminal.Tests;

public class SftpModeToolbarTemplateTests
{
    [Fact]
    public void SftpHeader_ShouldUseBrowseSurface_UtilitySlot_AndOverlayShell()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var path = Path.Combine(root, "src", "SkylarkTerminal", "Views", "RightHeaders", "SftpToolbarHeaderView.axaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("AddressHistoryButton", xaml, StringComparison.Ordinal);
        Assert.Contains("AddressSearchButton", xaml, StringComparison.Ordinal);
        Assert.Contains("AddressOverlayRoot", xaml, StringComparison.Ordinal);
        Assert.Contains("AddressOverlayTextBox", xaml, StringComparison.Ordinal);
        Assert.Contains("SearchOverlayTextBox", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("MinWidth=\"220\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void SftpHeader_ShouldUseFluentMenus_ForHistoryAndMore()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var path = Path.Combine(root, "src", "SkylarkTerminal", "Views", "RightHeaders", "SftpToolbarHeaderView.axaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("<ui:FAMenuFlyout", xaml, StringComparison.Ordinal);
        Assert.Contains("ToggleMenuFlyoutItem", xaml, StringComparison.Ordinal);
        Assert.Contains("OnHistoryFlyoutOpening", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<Flyout Placement", xaml, StringComparison.Ordinal);
    }
}
