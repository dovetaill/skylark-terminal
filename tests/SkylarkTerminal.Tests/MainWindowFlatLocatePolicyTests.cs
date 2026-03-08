using Avalonia.Controls;
using SkylarkTerminal.Models;
using SkylarkTerminal.ViewModels;
using SkylarkTerminal.Views;

namespace SkylarkTerminal.Tests;

public class MainWindowFlatLocatePolicyTests
{
    [Fact]
    public void TryEnsureFlatLocateTargetVisible_WhenFlatModeAndPendingTarget_ShouldSelectAndScroll()
    {
        var vm = new MainWindowViewModel();
        vm.SetListViewModeCommand.Execute(null);
        var target = vm.CurrentAssetFlatList.OfType<ConnectionNode>().First();
        vm.PendingQuickStartLocateTarget = target;

        var listBox = new ListBox
        {
            IsVisible = true,
            SelectionMode = SelectionMode.Multiple,
            ItemsSource = vm.CurrentAssetFlatList,
        };

        var scrollCount = 0;
        AssetNode? scrolledTarget = null;
        var didScroll = MainWindowInteractionPolicy.TryEnsureFlatLocateTargetVisible(
            vm,
            listBox,
            static (lb, node) =>
            {
                _ = lb;
                _ = node;
            });

        _ = MainWindowInteractionPolicy.TryEnsureFlatLocateTargetVisible(
            vm,
            listBox,
            (lb, node) =>
            {
                _ = lb;
                scrollCount++;
                scrolledTarget = node;
            });

        Assert.True(didScroll);
        Assert.Equal(1, scrollCount);
        Assert.Same(target, scrolledTarget);
        if (listBox.SelectedItems is not null)
        {
            Assert.Single(listBox.SelectedItems);
            Assert.Same(target, listBox.SelectedItems[0]);
        }
    }

    [Fact]
    public void TryEnsureFlatLocateTargetVisible_WhenNotFlatMode_ShouldNotScroll()
    {
        var vm = new MainWindowViewModel();
        var target = vm.CurrentAssetFlatList.OfType<ConnectionNode>().First();
        vm.PendingQuickStartLocateTarget = target;

        var listBox = new ListBox
        {
            IsVisible = true,
            SelectionMode = SelectionMode.Multiple,
            ItemsSource = vm.CurrentAssetFlatList,
        };

        var scrollCount = 0;
        var didScroll = MainWindowInteractionPolicy.TryEnsureFlatLocateTargetVisible(
            vm,
            listBox,
            (lb, node) =>
            {
                _ = lb;
                _ = node;
                scrollCount++;
            });

        Assert.False(didScroll);
        Assert.Equal(0, scrollCount);
    }

    [Fact]
    public void TryEnsureFlatLocateTargetVisible_WhenTargetMissingOrListHidden_ShouldNotScroll()
    {
        var vm = new MainWindowViewModel();
        vm.SetListViewModeCommand.Execute(null);
        var target = vm.CurrentAssetFlatList.OfType<ConnectionNode>().First();

        var hiddenListBox = new ListBox
        {
            IsVisible = false,
            SelectionMode = SelectionMode.Multiple,
            ItemsSource = vm.CurrentAssetFlatList,
        };

        var scrollCount = 0;
        var hiddenDidScroll = MainWindowInteractionPolicy.TryEnsureFlatLocateTargetVisible(
            vm,
            hiddenListBox,
            (lb, node) =>
            {
                _ = lb;
                _ = node;
                scrollCount++;
            });

        vm.PendingQuickStartLocateTarget = target;
        var nullTargetDidScroll = MainWindowInteractionPolicy.TryEnsureFlatLocateTargetVisible(
            vm,
            null,
            (lb, node) =>
            {
                _ = lb;
                _ = node;
                scrollCount++;
            });

        Assert.False(hiddenDidScroll);
        Assert.False(nullTargetDidScroll);
        Assert.Equal(0, scrollCount);
    }
}
