using SkylarkTerminal.Models;
using SkylarkTerminal.Services;
using SkylarkTerminal.Services.Mock;
using SkylarkTerminal.ViewModels.RightPanelModes;

namespace SkylarkTerminal.Tests;

public class SftpHeaderInteractionStateTests
{
    [Fact]
    public async Task ToggleShowHiddenFiles_ShouldRebuildVisibleItems()
    {
        var vm = new SftpModeViewModel(
            new MockSftpService(),
            new SftpNavigationService("/"),
            []);

        await vm.ActivateAsync("mock-conn-01");

        Assert.DoesNotContain(vm.VisibleItems, item => item.IsHidden);

        vm.ToggleShowHiddenFilesCommand.Execute(null);

        Assert.Contains(vm.VisibleItems, item => item.IsHidden);
        Assert.True(vm.ShowHiddenFiles);
    }

    [Fact]
    public void OpenSearchOverlayCommand_ShouldSwitchOverlayMode_AndHideUtilityStrip()
    {
        var vm = new SftpModeViewModel(
            new MockSftpService(),
            new SftpNavigationService("/"),
            []);

        vm.OpenSearchOverlayCommand.Execute(null);

        Assert.Equal(SftpHeaderOverlayMode.Search, vm.HeaderOverlayMode);
        Assert.True(vm.IsHeaderOverlayVisible);
        Assert.False(vm.IsHeaderUtilityStripVisible);
    }
}
