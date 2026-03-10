using SkylarkTerminal.Models;
using SkylarkTerminal.Services.Mock;
using SkylarkTerminal.ViewModels.RightPanelModes;
using System.Collections.ObjectModel;

namespace SkylarkTerminal.Tests;

public class SnippetsCategoryDeletionTests
{
    [Fact]
    public async Task DeleteCategoryAsync_ShouldDeleteNonEmptyCategory_AfterCascadeConfirm()
    {
        var repo = new MockSnippetRepository(
        [
            new SnippetCategory
            {
                Name = "Ops",
                Items = new ObservableCollection<SnippetItem>
                {
                    new() { Title = "Restart", Content = "systemctl restart app" }
                }
            }
        ]);
        var dialog = new MockAppDialogService
        {
            DeleteSnippetCategoryConfirmResult = true
        };
        var vm = new SnippetsModeViewModel(
            repo,
            new MockClipboardService(),
            new MockTerminalCommandBridge(),
            dialog,
            static () => null,
            static () => []);

        await vm.LoadAsync();
        await vm.DeleteCategoryAsync(vm.Categories[0]);

        Assert.Empty(vm.Categories);
        Assert.True(dialog.LastDeleteCategoryIncludesChildren);
    }

    [Fact]
    public async Task RebuildVisibleCategories_ShouldIgnoreTags_WhenFiltering()
    {
        var repo = new MockSnippetRepository(
        [
            new SnippetCategory
            {
                Name = "Ops",
                Items = new ObservableCollection<SnippetItem>
                {
                    new()
                    {
                        Title = "Restart",
                        Content = "systemctl restart app",
                        Tags = ["prod-only"]
                    }
                }
            }
        ]);

        var vm = new SnippetsModeViewModel(
            repo,
            new MockClipboardService(),
            new MockTerminalCommandBridge(),
            new MockAppDialogService(),
            static () => null,
            static () => []);

        await vm.LoadAsync();
        vm.FilterText = "prod-only";

        Assert.Empty(vm.VisibleCategories);
    }
}
