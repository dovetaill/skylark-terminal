using SkylarkTerminal.Services;
using SkylarkTerminal.ViewModels.RightPanelModes;

namespace SkylarkTerminal.Tests;

public class SftpAddressInteractionStateTests
{
    [Fact]
    public void AddressEditor_ShouldExpandAndCollapse_AroundCommit()
    {
        var vm = new SftpModeViewModel(new SftpNavigationService("/var/log"), []);

        Assert.False(vm.IsAddressEditorExpanded);

        vm.ExpandAddressEditorCommand.Execute(null);
        Assert.True(vm.IsAddressEditorExpanded);

        vm.AddressInput = "/var";
        vm.AddressCommitCommand.Execute(null);

        Assert.False(vm.IsAddressEditorExpanded);
        Assert.Equal("/var", vm.CurrentPath);
        Assert.Equal("/var", vm.AddressInput);
    }
}
