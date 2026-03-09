using SkylarkTerminal.Models;
using SkylarkTerminal.Services.Mock;
using SkylarkTerminal.ViewModels.RightPanelModes;
using System.Threading.Tasks;

namespace SkylarkTerminal.Tests;

public class SnippetsModeInteractionStateTests
{
    [Fact]
    public void BeginCreateCategoryCommand_ShouldEnterCreateState_AndEnableNewCategory()
    {
        var vm = new SnippetsModeViewModel(
            new MockSnippetRepository(),
            new MockClipboardService(),
            new MockTerminalCommandBridge(),
            new MockAppDialogService(),
            static () => null,
            static () => []);

        vm.BeginCreateCategoryCommand.Execute(null);

        Assert.Equal(SnippetPanelState.Create, vm.PanelState);
        Assert.True(vm.Draft.CreateNewCategory);
        Assert.Equal(string.Empty, vm.Draft.CategoryName);
    }

    [Fact]
    public async Task BeginCreateFromClipboardCommand_ShouldSeedDraftContent()
    {
        var clipboard = new MockClipboardService
        {
            Text = "echo hello"
        };

        var vm = new SnippetsModeViewModel(
            new MockSnippetRepository(),
            clipboard,
            new MockTerminalCommandBridge(),
            new MockAppDialogService(),
            static () => null,
            static () => []);

        await vm.BeginCreateFromClipboardCommand.ExecuteAsync(null);

        Assert.Equal(SnippetPanelState.Create, vm.PanelState);
        Assert.Equal("echo hello", vm.Draft.Content);
    }

    [Fact]
    public async Task CopyAsync_ShouldWriteSnippetContent_ToClipboardService()
    {
        var clipboard = new MockClipboardService();
        var vm = new SnippetsModeViewModel(
            new MockSnippetRepository(),
            clipboard,
            new MockTerminalCommandBridge(),
            new MockAppDialogService(),
            static () => null,
            static () => []);

        await vm.CopyAsync(new SnippetItem
        {
            Title = "Copy me",
            Content = "pwd"
        });

        Assert.Equal("pwd", clipboard.Text);
    }
}
