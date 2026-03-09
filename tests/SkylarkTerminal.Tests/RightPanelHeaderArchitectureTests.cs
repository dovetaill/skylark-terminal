using SkylarkTerminal.Models;
using SkylarkTerminal.ViewModels;

namespace SkylarkTerminal.Tests;

public class RightPanelHeaderArchitectureTests
{
    [Fact]
    public async Task ActiveRightHeader_ShouldSwitchWithSelectedMode()
    {
        var vm = new MainWindowViewModel();

        Assert.IsType<ActionStripRightPanelHeader>(vm.ActiveRightHeader);

        vm.ShowSnippetsToolsCommand.Execute(null);
        Assert.IsType<ActionStripRightPanelHeader>(vm.ActiveRightHeader);

        vm.ShowHistoryToolsCommand.Execute(null);
        Assert.IsType<ActionStripRightPanelHeader>(vm.ActiveRightHeader);

        await vm.ShowSftpToolsCommand.ExecuteAsync(null);
        Assert.IsType<SftpToolbarRightPanelHeader>(vm.ActiveRightHeader);
    }
}
