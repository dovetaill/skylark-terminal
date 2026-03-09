using SkylarkTerminal.Services;
using SkylarkTerminal.Services.Mock;
using SkylarkTerminal.ViewModels.RightPanelModes;

namespace SkylarkTerminal.Tests;

public class SftpAddressInteractionStateTests
{
    [Fact]
    public async Task CommitAddressCommand_ShouldKeepStateOwnedBySftpMode()
    {
        var vm = new SftpModeViewModel(
            new MockSftpService(),
            new SftpNavigationService("/var/log"),
            []);

        await vm.ActivateAsync("mock-conn-01");

        Assert.False(vm.IsAddressEditorExpanded);

        vm.ExpandAddressEditorCommand.Execute(null);
        Assert.True(vm.IsAddressEditorExpanded);

        vm.AddressInput = "/logs";

        await vm.CommitAddressAsync();

        Assert.False(vm.IsAddressEditorExpanded);
        Assert.Contains(vm.Items, item => item.FullPath.Contains("/logs", StringComparison.Ordinal));
    }
}
