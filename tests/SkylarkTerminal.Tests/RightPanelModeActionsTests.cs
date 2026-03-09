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
        Assert.Contains(vm.ActiveRightMode.Actions, a => a.Id == "snippet.search-focus");

        vm.ShowHistoryToolsCommand.Execute(null);
        Assert.Contains(vm.ActiveRightMode.Actions, a => a.Id == "history.search");
        Assert.Contains(vm.ActiveRightMode.Actions, a => a.Id == "history.sort");
    }

    [Fact]
    public async Task SftpMode_ShouldUseCustomHeader_InsteadOfGenericActionStrip()
    {
        var vm = new MainWindowViewModel();

        await vm.ShowSftpToolsCommand.ExecuteAsync(null);

        Assert.Empty(vm.ActiveRightMode.Actions);
    }
}
