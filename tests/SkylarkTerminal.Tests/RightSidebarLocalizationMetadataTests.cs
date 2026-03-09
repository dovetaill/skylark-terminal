using SkylarkTerminal.ViewModels;
using SkylarkTerminal.ViewModels.RightPanelModes;

namespace SkylarkTerminal.Tests;

public class RightSidebarLocalizationMetadataTests
{
    [Fact]
    public async Task RightSidebarCommands_ShouldExposeChineseTooltips()
    {
        var vm = new MainWindowViewModel();

        vm.ShowHistoryToolsCommand.Execute(null);
        Assert.Contains(vm.ActiveRightMode.Actions, a => a.Id == "history.search" && a.TooltipZh == "搜索历史");
        Assert.Contains(vm.ActiveRightMode.Actions, a => a.Id == "history.clear" && a.TooltipZh == "清空历史");

        await vm.ShowSftpToolsCommand.ExecuteAsync(null);
        var sftp = Assert.IsType<SftpModeViewModel>(vm.ActiveRightMode);
        Assert.Contains(sftp.LeadingCommands, a => a.Id == "sftp.back" && a.TooltipZh == "后退");
        Assert.Contains(sftp.TrailingCommands, a => a.Id == "sftp.refresh" && a.TooltipZh == "刷新");
        Assert.Contains(sftp.MoreCommands, a => a.Id == "sftp.copy-path" && a.LabelZh == "复制当前路径");
    }
}
