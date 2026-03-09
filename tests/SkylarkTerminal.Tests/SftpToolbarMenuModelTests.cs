using SkylarkTerminal.Services;
using SkylarkTerminal.Services.Mock;
using SkylarkTerminal.ViewModels.RightPanelModes;
using System;
using System.Linq;

namespace SkylarkTerminal.Tests;

public class SftpToolbarMenuModelTests
{
    [Fact]
    public async Task NavigateHistoryPathCommand_ShouldLoadRequestedDirectory()
    {
        var vm = new SftpModeViewModel(
            new MockSftpService(),
            new SftpNavigationService("/"),
            []);

        await vm.ActivateAsync("mock-conn-01");
        await vm.NavigateHistoryPathAsync("/var/log");

        Assert.Equal("/var/log", vm.CurrentPath);
        Assert.Contains(vm.VisibleItems, item => item.FullPath.Contains("/var/log", StringComparison.Ordinal));
    }
}
