using SkylarkTerminal.Models;
using SkylarkTerminal.ViewModels;
using SkylarkTerminal.ViewModels.RightPanelModes;

namespace SkylarkTerminal.Tests;

public class RightPanelModeArchitectureTests
{
    [Fact]
    public void MainWindow_ShouldExposeActiveRightMode_AndThreeModes()
    {
        var vm = new MainWindowViewModel();

        Assert.NotNull(vm.ActiveRightMode);
        Assert.Equal(3, vm.RightPanelModes.Count);
        Assert.Contains(vm.RightPanelModes, m => m.Kind == RightToolsViewKind.Snippets);
        Assert.Contains(vm.RightPanelModes, m => m.Kind == RightToolsViewKind.History);
        Assert.Contains(vm.RightPanelModes, m => m.Kind == RightToolsViewKind.Sftp);
    }
}
