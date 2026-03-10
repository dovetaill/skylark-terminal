using SkylarkTerminal.Models;
using SkylarkTerminal.Services.Mock;
using SkylarkTerminal.ViewModels.RightPanelModes;
using System;
using System.Collections.ObjectModel;
using System.IO;

namespace SkylarkTerminal.Tests;

public class SnippetsEditorFormTemplateTests
{
    [Fact]
    public void SnippetsEditor_ShouldUseEditableCategoryPicker_AndDropTagsField()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var path = Path.Combine(root, "src", "SkylarkTerminal", "Views", "RightModes", "SnippetsModeView.axaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("<ComboBox", xaml, StringComparison.Ordinal);
        Assert.Contains("IsEditable=\"True\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Draft.TagsText", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("TagsWatermark", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CategoryOptions_ShouldExposeExistingCategoryNames()
    {
        var repo = new MockSnippetRepository(
        [
            new SnippetCategory { Name = "Ops", Items = new ObservableCollection<SnippetItem>() },
            new SnippetCategory { Name = "Web", Items = new ObservableCollection<SnippetItem>() }
        ]);
        var vm = new SnippetsModeViewModel(
            repo,
            new MockClipboardService(),
            new MockTerminalCommandBridge(),
            new MockAppDialogService(),
            static () => null,
            static () => []);

        await vm.LoadAsync();

        Assert.Equal(["Ops", "Web"], vm.CategoryOptions);
    }
}
