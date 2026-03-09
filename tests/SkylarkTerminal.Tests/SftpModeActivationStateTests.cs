using SkylarkTerminal.Models;
using SkylarkTerminal.Services;
using SkylarkTerminal.Services.Mock;
using SkylarkTerminal.ViewModels.RightPanelModes;

namespace SkylarkTerminal.Tests;

public class SftpModeActivationStateTests
{
    [Fact]
    public async Task ActivateAsync_ShouldLoadItems_AndTransitionToLoaded()
    {
        var vm = new SftpModeViewModel(
            new MockSftpService(),
            new SftpNavigationService("/"),
            actions: []);

        Assert.Equal(SftpPanelLoadState.Idle, vm.LoadState);

        await vm.ActivateAsync("mock-conn-01");

        Assert.Equal(SftpPanelLoadState.Loaded, vm.LoadState);
        Assert.NotEmpty(vm.Items);
        Assert.Null(vm.ErrorMessage);
    }
}
