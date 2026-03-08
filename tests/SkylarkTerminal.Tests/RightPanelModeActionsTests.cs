using SkylarkTerminal.ViewModels;

namespace SkylarkTerminal.Tests;

public class RightPanelModeActionsTests
{
    [Fact]
    public async Task EachMode_ShouldExposeExpectedActions()
    {
        var vm = new MainWindowViewModel();

        vm.ShowSnippetsToolsCommand.Execute(null);
        Assert.Contains(vm.ActiveRightMode.Actions, a => a.Id == "snippet.new");
        Assert.Contains(vm.ActiveRightMode.Actions, a => a.Id == "snippet.search");

        vm.ShowHistoryToolsCommand.Execute(null);
        Assert.Contains(vm.ActiveRightMode.Actions, a => a.Id == "history.search");
        Assert.Contains(vm.ActiveRightMode.Actions, a => a.Id == "history.sort");

        await vm.ShowSftpToolsCommand.ExecuteAsync(null);
        Assert.Contains(vm.ActiveRightMode.Actions, a => a.Id == "sftp.back");
        Assert.Contains(vm.ActiveRightMode.Actions, a => a.Id == "sftp.up");
    }
}
