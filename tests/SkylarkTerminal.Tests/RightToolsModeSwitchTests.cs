using SkylarkTerminal.Models;
using SkylarkTerminal.ViewModels;

namespace SkylarkTerminal.Tests;

public class RightToolsModeSwitchTests
{
    [Fact]
    public void RightToolsModeItems_ShouldContainSnippetsHistorySftp_InOrder()
    {
        var vm = new MainWindowViewModel();

        var kinds = vm.RightToolsModeItems
            .Select(item => item.Kind)
            .ToArray();

        Assert.Equal(
            [RightToolsViewKind.Snippets, RightToolsViewKind.History, RightToolsViewKind.Sftp],
            kinds);
    }

    [Fact]
    public void ShowHistoryToolsCommand_ShouldSyncSelectedModeItemAndContentNode()
    {
        var vm = new MainWindowViewModel();

        vm.ShowHistoryToolsCommand.Execute(null);

        Assert.Equal(RightToolsViewKind.History, vm.SelectedRightToolsModeItem!.Kind);
        Assert.IsType<HistoryRightToolsContent>(vm.CurrentRightToolsContent);
    }
}
