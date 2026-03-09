using SkylarkTerminal.Models;
using SkylarkTerminal.ViewModels;

namespace SkylarkTerminal.Tests;

public class RightSidebarModeMetadataTests
{
    [Fact]
    public void RightToolsModeItems_ShouldExposeChineseTooltip_AndSemanticIconKey()
    {
        var vm = new MainWindowViewModel();

        Assert.Collection(
            vm.RightToolsModeItems,
            item =>
            {
                Assert.Equal(RightToolsViewKind.Snippets, item.Kind);
                Assert.Equal("代码块", item.TooltipZh);
                Assert.Equal(RightModeIconKey.Snippets, item.IconKey);
            },
            item =>
            {
                Assert.Equal(RightToolsViewKind.History, item.Kind);
                Assert.Equal("历史记录", item.TooltipZh);
                Assert.Equal(RightModeIconKey.History, item.IconKey);
            },
            item =>
            {
                Assert.Equal(RightToolsViewKind.Sftp, item.Kind);
                Assert.Equal("SFTP 文件", item.TooltipZh);
                Assert.Equal(RightModeIconKey.RemoteFiles, item.IconKey);
            });
    }
}
