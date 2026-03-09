# RightSidebar Layout Bugfix3 Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 在不触碰 `SshTerminalPane`、`RowStripedTerminalView` 与 `SSH.NET` 主链路的前提下，完成 `RightSidebar` 第三轮 `SFTP` 体验修复：地址栏改为覆盖式编辑层，`历史路径 / 搜索` 收进 `PathChip` 内部 utility slot，`More` 切换为 `FAMenuFlyout` 勾选菜单，并移除右栏无收益内容过渡以消除模式切换残影。

**Architecture:** 保留当前 `RightSidebarHostView + IRightPanelModeViewModel + header slot` 架构，不回退到 `MainWindow` 内联实现。`SftpModeViewModel` 继续作为模式级状态归属点，但需要扩展为同时拥有 `HeaderOverlayMode`、`RecentPaths`、`SearchQuery`、`ShowHiddenFiles` 与 `VisibleItems` 过滤逻辑；`SFTP` 头部改造成“浏览态 browse surface + 编辑态 overlay shell”的双层结构，历史路径 flyout 与 `More` menu 都采用 Fluent 风格菜单而不是普通 `Flyout` 内容拼装。

**Tech Stack:** `.NET 10`, `Avalonia 11.3.12`, `FluentAvaloniaUI 2.5.0`, `CommunityToolkit.Mvvm 8.4.0`, `Microsoft.Extensions.DependencyInjection 10.0.0`, `xUnit`

---

**Design Input:** `docs/plans/2026-03-09-right-sidebar-layout-bugfix3-design.md`

## 开发前约束

1. 当前任务只允许修改右栏相关 View / ViewModel / 样式 / Mock 数据 / 测试 / 文档；不要改 `src/ThirdParty/Iciclecreek.Avalonia.Terminal.Fork`。
2. `More` 菜单只承载轻动作与勾选项，禁止把 `TextBox` 放进 popup。
3. 地址编辑 overlay 只覆盖中间路径区域，不能推动或遮挡左右命令按钮。
4. `历史路径` 与 `搜索` 的入口必须在 `PathChip` 内部 utility slot，不再外置到外围按钮位。
5. 右栏内容区去掉无收益过渡，不保留“为了动画而动画”的结构。
6. 每个任务必须遵守 TDD：先写失败测试，再最小实现，再回归，再提交。
7. 每个任务单独 commit，保持可回滚边界。

## Task Map

1. 移除右栏内容过渡并锁定残影回归测试
2. 扩展 `SftpModeViewModel` / `SftpNavigationService` 状态契约，承接 overlay、搜索、隐藏文件与历史路径
3. 重建 `SFTP` header 为 `A2 + C1`：browse surface + utility slot + overlay shell
4. 用 Fluent 菜单替换历史路径与 `More` 弹层
5. 将 `SFTP` 内容区切换到 `VisibleItems` 过滤绑定，并补足搜索空结果表达
6. 同步文档并完成全量回归验证

---

### Task 1: 移除右栏内容过渡并锁定残影回归测试

**Files:**
- Modify: `src/SkylarkTerminal/Views/RightSidebarHostView.axaml`
- Modify: `tests/SkylarkTerminal.Tests/RightSidebarHostViewTemplateTests.cs`
- Create: `tests/SkylarkTerminal.Tests/RightSidebarContentTransitionTests.cs`

**Step 1: Write the failing test**

```csharp
using System;
using System.IO;

namespace SkylarkTerminal.Tests;

public class RightSidebarContentTransitionTests
{
    [Fact]
    public void RightSidebarHostView_ShouldUseStaticContentHost_ForModeContent()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var path = Path.Combine(root, "src", "SkylarkTerminal", "Views", "RightSidebarHostView.axaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("<ContentControl Grid.Row=\"2\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<TransitioningContentControl", xaml, StringComparison.Ordinal);
    }
}
```

并在 `RightSidebarHostViewTemplateTests.cs` 追加约束：

```csharp
Assert.Contains("Content=\"{Binding ActiveRightMode.ContentNode}\"", xaml, StringComparison.Ordinal);
Assert.DoesNotContain("TransitioningContentControl", xaml, StringComparison.Ordinal);
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~RightSidebarHostViewTemplateTests|FullyQualifiedName~RightSidebarContentTransitionTests" -v minimal`  
Expected: FAIL，当前仍然存在 `TransitioningContentControl`。

**Step 3: Write minimal implementation**

```xml
<ContentControl Grid.Row="2"
                Margin="0"
                Content="{Binding ActiveRightMode.ContentNode}"/>
```

删除旧的：

```xml
<TransitioningContentControl Grid.Row="2"
                             Margin="0"
                             Content="{Binding ActiveRightMode.ContentNode}"/>
```

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~RightSidebarHostViewTemplateTests|FullyQualifiedName~RightSidebarContentTransitionTests" -v minimal`  
Expected: PASS。

**Step 5: Commit**

```bash
git add src/SkylarkTerminal/Views/RightSidebarHostView.axaml \
        tests/SkylarkTerminal.Tests/RightSidebarHostViewTemplateTests.cs \
        tests/SkylarkTerminal.Tests/RightSidebarContentTransitionTests.cs
git commit -m "fix: remove right sidebar content transition"
```

---

### Task 2: 扩展 `SFTP` 模式状态契约，承接 overlay、搜索、隐藏文件与历史路径

**Files:**
- Create: `src/SkylarkTerminal/Models/SftpHeaderOverlayMode.cs`
- Modify: `src/SkylarkTerminal/Models/RemoteFileNode.cs`
- Modify: `src/SkylarkTerminal/Services/ISftpNavigationService.cs`
- Modify: `src/SkylarkTerminal/Services/SftpNavigationService.cs`
- Modify: `src/SkylarkTerminal/Services/Mock/MockSftpService.cs`
- Modify: `src/SkylarkTerminal/ViewModels/RightPanelModes/SftpModeViewModel.cs`
- Modify: `tests/SkylarkTerminal.Tests/SftpNavigationServiceTests.cs`
- Create: `tests/SkylarkTerminal.Tests/SftpHeaderInteractionStateTests.cs`

**Step 1: Write the failing test**

```csharp
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
```

并扩展 `SftpNavigationServiceTests.cs`：

```csharp
[Fact]
public void NavigateTo_ShouldTrackRecentPaths_WithoutDuplicates()
{
    var nav = new SftpNavigationService("/");

    nav.NavigateTo("/var");
    nav.NavigateTo("/var/log");
    nav.NavigateTo("/var");

    Assert.Equal(["/var", "/var/log", "/"], nav.RecentPaths.Take(3).ToArray());
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~SftpHeaderInteractionStateTests|FullyQualifiedName~SftpNavigationServiceTests" -v minimal`  
Expected: FAIL，`HeaderOverlayMode` / `VisibleItems` / `ShowHiddenFiles` / `RecentPaths` / `IsHidden` 尚不存在。

**Step 3: Write minimal implementation**

```csharp
namespace SkylarkTerminal.Models;

public enum SftpHeaderOverlayMode
{
    None,
    Address,
    Search
}
```

```csharp
public sealed class RemoteFileNode
{
    public string Name { get; init; } = string.Empty;
    public string FullPath { get; init; } = string.Empty;
    public bool IsDirectory { get; init; }
    public long Size { get; init; }
    public bool IsHidden { get; init; }
}
```

```csharp
public interface ISftpNavigationService
{
    string CurrentPath { get; }
    bool CanGoBack { get; }
    bool CanGoForward { get; }
    IReadOnlyList<string> RecentPaths { get; }
    string NavigateTo(string path);
    string GoBack();
    string GoForward();
    string GoUp();
    string Refresh();
    string TryResolveAddressInput(string input);
}
```

```csharp
public sealed partial class SftpModeViewModel : ObservableObject, IRightPanelModeViewModel
{
    [ObservableProperty]
    private SftpHeaderOverlayMode headerOverlayMode;

    [ObservableProperty]
    private bool showHiddenFiles;

    [ObservableProperty]
    private string searchQuery = string.Empty;

    public ObservableCollection<RemoteFileNode> VisibleItems { get; } = [];

    public IReadOnlyList<string> RecentPaths => _navigationService.RecentPaths;
    public bool IsHeaderOverlayVisible => HeaderOverlayMode != SftpHeaderOverlayMode.None;
    public bool IsHeaderUtilityStripVisible => HeaderOverlayMode == SftpHeaderOverlayMode.None;

    public IRelayCommand OpenAddressOverlayCommand { get; }
    public IRelayCommand OpenSearchOverlayCommand { get; }
    public IRelayCommand CloseHeaderOverlayCommand { get; }
    public IRelayCommand ToggleShowHiddenFilesCommand { get; }

    private void RebuildVisibleItems()
    {
        VisibleItems.Clear();

        foreach (var item in Items.Where(ShouldIncludeItem))
        {
            VisibleItems.Add(item);
        }

        OnPropertyChanged(nameof(IsFilteredEmptyState));
        OnPropertyChanged(nameof(HasVisibleItems));
    }
}
```

`MockSftpService` 中新增至少一个隐藏节点，例如 `.env`；`SftpNavigationService` 在导航动作后维护 `RecentPaths`。

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~SftpHeaderInteractionStateTests|FullyQualifiedName~SftpNavigationServiceTests" -v minimal`  
Expected: PASS。

**Step 5: Commit**

```bash
git add src/SkylarkTerminal/Models/SftpHeaderOverlayMode.cs \
        src/SkylarkTerminal/Models/RemoteFileNode.cs \
        src/SkylarkTerminal/Services/ISftpNavigationService.cs \
        src/SkylarkTerminal/Services/SftpNavigationService.cs \
        src/SkylarkTerminal/Services/Mock/MockSftpService.cs \
        src/SkylarkTerminal/ViewModels/RightPanelModes/SftpModeViewModel.cs \
        tests/SkylarkTerminal.Tests/SftpNavigationServiceTests.cs \
        tests/SkylarkTerminal.Tests/SftpHeaderInteractionStateTests.cs
git commit -m "feat: add sftp overlay and filter state model"
```

---

### Task 3: 重建 `SFTP` header 为 browse surface + utility slot + overlay shell

**Files:**
- Modify: `src/SkylarkTerminal/Views/RightHeaders/SftpToolbarHeaderView.axaml`
- Modify: `src/SkylarkTerminal/Views/RightHeaders/SftpToolbarHeaderView.axaml.cs`
- Modify: `src/SkylarkTerminal/Views/MainWindow.axaml`
- Modify: `tests/SkylarkTerminal.Tests/SftpModeToolbarTemplateTests.cs`
- Modify: `tests/SkylarkTerminal.Tests/SftpAddressInteractionStateTests.cs`

**Step 1: Write the failing test**

在 `SftpModeToolbarTemplateTests.cs` 中将结构约束升级为：

```csharp
[Fact]
public void SftpHeader_ShouldUseBrowseSurface_UtilitySlot_AndOverlayShell()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    var path = Path.Combine(root, "src", "SkylarkTerminal", "Views", "RightHeaders", "SftpToolbarHeaderView.axaml");
    var xaml = File.ReadAllText(path);

    Assert.Contains("AddressHistoryButton", xaml, StringComparison.Ordinal);
    Assert.Contains("AddressSearchButton", xaml, StringComparison.Ordinal);
    Assert.Contains("AddressOverlayRoot", xaml, StringComparison.Ordinal);
    Assert.Contains("AddressOverlayTextBox", xaml, StringComparison.Ordinal);
    Assert.Contains("SearchOverlayTextBox", xaml, StringComparison.Ordinal);
    Assert.DoesNotContain("MinWidth=\"220\"", xaml, StringComparison.Ordinal);
}
```

并扩展 `SftpAddressInteractionStateTests.cs`：

```csharp
[Fact]
public void CloseHeaderOverlayCommand_ShouldRestoreBrowseState()
{
    var vm = new SftpModeViewModel(
        new MockSftpService(),
        new SftpNavigationService("/"),
        []);

    vm.OpenAddressOverlayCommand.Execute(null);
    Assert.True(vm.IsHeaderOverlayVisible);

    vm.CloseHeaderOverlayCommand.Execute(null);

    Assert.Equal(SftpHeaderOverlayMode.None, vm.HeaderOverlayMode);
    Assert.True(vm.IsHeaderUtilityStripVisible);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~SftpModeToolbarTemplateTests|FullyQualifiedName~SftpAddressInteractionStateTests" -v minimal`  
Expected: FAIL，header 仍是旧的 `chip + TextBox` 原位切换结构。

**Step 3: Write minimal implementation**

`SftpToolbarHeaderView.axaml` 中间区域改成单个 browse surface，utility slot 内嵌历史路径和搜索入口，再在同层叠一个 overlay root：

```xml
<Grid Grid.Column="1" MinWidth="0">
    <Button x:Name="AddressChipButton"
            Classes="TitleBarButton RightSidebarAddressChip"
            HorizontalAlignment="Stretch"
            HorizontalContentAlignment="Stretch"
            IsVisible="{Binding DataContext.ActiveRightMode.IsHeaderUtilityStripVisible, RelativeSource={RelativeSource AncestorType=views:RightSidebarHostView}}"
            Click="OnAddressChipClick">
        <Grid ColumnDefinitions="*,Auto,Auto"
              ColumnSpacing="6">
            <TextBlock Grid.Column="0"
                       Text="{Binding DataContext.ActiveRightMode.CurrentPath, RelativeSource={RelativeSource AncestorType=views:RightSidebarHostView}}"
                       TextTrimming="CharacterEllipsis"/>
            <Button x:Name="AddressHistoryButton"
                    Grid.Column="1"
                    Classes="TitleBarButton RightSidebarInlineUtilityButton"/>
            <Button x:Name="AddressSearchButton"
                    Grid.Column="2"
                    Classes="TitleBarButton RightSidebarInlineUtilityButton"
                    Command="{Binding DataContext.ActiveRightMode.OpenSearchOverlayCommand, RelativeSource={RelativeSource AncestorType=views:RightSidebarHostView}}"/>
        </Grid>
    </Button>

    <Border x:Name="AddressOverlayRoot"
            Classes="RightSidebarHeaderOverlay"
            IsVisible="{Binding DataContext.ActiveRightMode.IsHeaderOverlayVisible, RelativeSource={RelativeSource AncestorType=views:RightSidebarHostView}}">
        <Grid ColumnDefinitions="*,Auto">
            <TextBox x:Name="AddressOverlayTextBox"
                     IsVisible="{Binding DataContext.ActiveRightMode.IsAddressOverlayVisible, RelativeSource={RelativeSource AncestorType=views:RightSidebarHostView}}"/>
            <TextBox x:Name="SearchOverlayTextBox"
                     IsVisible="{Binding DataContext.ActiveRightMode.IsSearchOverlayVisible, RelativeSource={RelativeSource AncestorType=views:RightSidebarHostView}}"/>
        </Grid>
    </Border>
</Grid>
```

`MainWindow.axaml` 中新增 overlay/inline utility button 样式：

```xml
<Style Selector="Button.RightSidebarInlineUtilityButton">
    <Setter Property="MinWidth" Value="24"/>
    <Setter Property="MinHeight" Value="24"/>
    <Setter Property="Padding" Value="0"/>
</Style>

<Style Selector="Border.RightSidebarHeaderOverlay">
    <Setter Property="Padding" Value="4"/>
    <Setter Property="CornerRadius" Value="8"/>
    <Setter Property="BorderThickness" Value="1"/>
</Style>
```

`SftpToolbarHeaderView.axaml.cs` 中保留焦点控制，但根据 overlay mode 聚焦 `AddressOverlayTextBox` 或 `SearchOverlayTextBox`。

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~SftpModeToolbarTemplateTests|FullyQualifiedName~SftpAddressInteractionStateTests" -v minimal`  
Expected: PASS。

**Step 5: Commit**

```bash
git add src/SkylarkTerminal/Views/RightHeaders/SftpToolbarHeaderView.axaml \
        src/SkylarkTerminal/Views/RightHeaders/SftpToolbarHeaderView.axaml.cs \
        src/SkylarkTerminal/Views/MainWindow.axaml \
        tests/SkylarkTerminal.Tests/SftpModeToolbarTemplateTests.cs \
        tests/SkylarkTerminal.Tests/SftpAddressInteractionStateTests.cs
git commit -m "style: add sftp browse surface and overlay header"
```

---

### Task 4: 用 Fluent 菜单替换历史路径与 `More` 弹层

**Files:**
- Modify: `src/SkylarkTerminal/Views/RightHeaders/SftpToolbarHeaderView.axaml`
- Modify: `src/SkylarkTerminal/Views/RightHeaders/SftpToolbarHeaderView.axaml.cs`
- Modify: `src/SkylarkTerminal/ViewModels/RightPanelModes/SftpModeViewModel.cs`
- Create: `tests/SkylarkTerminal.Tests/SftpToolbarMenuModelTests.cs`
- Modify: `tests/SkylarkTerminal.Tests/SftpModeToolbarTemplateTests.cs`

**Step 1: Write the failing test**

`SftpModeToolbarTemplateTests.cs` 中追加：

```csharp
[Fact]
public void SftpHeader_ShouldUseFluentMenus_ForHistoryAndMore()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    var path = Path.Combine(root, "src", "SkylarkTerminal", "Views", "RightHeaders", "SftpToolbarHeaderView.axaml");
    var xaml = File.ReadAllText(path);

    Assert.Contains("<ui:FAMenuFlyout", xaml, StringComparison.Ordinal);
    Assert.Contains("ToggleMenuFlyoutItem", xaml, StringComparison.Ordinal);
    Assert.Contains("OnHistoryFlyoutOpening", xaml, StringComparison.Ordinal);
    Assert.DoesNotContain("<Flyout Placement", xaml, StringComparison.Ordinal);
}
```

新建 `SftpToolbarMenuModelTests.cs`：

```csharp
using SkylarkTerminal.Services;
using SkylarkTerminal.Services.Mock;
using SkylarkTerminal.ViewModels.RightPanelModes;

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
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~SftpModeToolbarTemplateTests|FullyQualifiedName~SftpToolbarMenuModelTests" -v minimal`  
Expected: FAIL，当前 `More` 仍为普通 `Flyout`，历史路径也没有 Fluent 菜单宿主。

**Step 3: Write minimal implementation**

`SftpToolbarHeaderView.axaml` 中：

```xml
<Button x:Name="AddressHistoryButton"
        Grid.Column="1"
        Classes="TitleBarButton RightSidebarInlineUtilityButton"
        ToolTip.Tip="历史路径">
    <TextBlock FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets"
               Text="&#xE81C;"/>
    <Button.Flyout>
        <ui:FAMenuFlyout x:Name="HistoryFlyout"
                         Opening="OnHistoryFlyoutOpening"/>
    </Button.Flyout>
</Button>
```

```xml
<Button Classes="TitleBarButton RightSidebarCommandButton"
        ToolTip.Tip="更多操作">
    <TextBlock FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets"
               Text="&#xE712;"/>
    <Button.Flyout>
        <ui:FAMenuFlyout>
            <ui:ToggleMenuFlyoutItem Text="显示隐藏文件"
                                     IsChecked="{Binding DataContext.ActiveRightMode.ShowHiddenFiles, RelativeSource={RelativeSource AncestorType=views:RightSidebarHostView}}"
                                     Command="{Binding DataContext.ActiveRightMode.ToggleShowHiddenFilesCommand, RelativeSource={RelativeSource AncestorType=views:RightSidebarHostView}}"/>
        </ui:FAMenuFlyout>
    </Button.Flyout>
</Button>
```

`SftpToolbarHeaderView.axaml.cs` 中按 opening 动态重建 `HistoryFlyout`：

```csharp
private void OnHistoryFlyoutOpening(object? sender, EventArgs e)
{
    if (!TryGetSftpModeViewModel(out var vm))
    {
        return;
    }

    HistoryFlyout.Items.Clear();
    foreach (var path in vm.RecentPaths.Take(8))
    {
        HistoryFlyout.Items.Add(new MenuFlyoutItem
        {
            Text = path,
            Command = vm.NavigateHistoryPathCommand,
            CommandParameter = path,
        });
    }
}
```

`SftpModeViewModel` 中增加：

```csharp
public IAsyncRelayCommand<string?> NavigateHistoryPathCommand { get; }

public async Task NavigateHistoryPathAsync(string? path)
{
    if (string.IsNullOrWhiteSpace(path))
    {
        return;
    }

    NavigateTo(path);
    await LoadDirectoryAsync(CurrentPath);
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~SftpModeToolbarTemplateTests|FullyQualifiedName~SftpToolbarMenuModelTests" -v minimal`  
Expected: PASS。

**Step 5: Commit**

```bash
git add src/SkylarkTerminal/Views/RightHeaders/SftpToolbarHeaderView.axaml \
        src/SkylarkTerminal/Views/RightHeaders/SftpToolbarHeaderView.axaml.cs \
        src/SkylarkTerminal/ViewModels/RightPanelModes/SftpModeViewModel.cs \
        tests/SkylarkTerminal.Tests/SftpToolbarMenuModelTests.cs \
        tests/SkylarkTerminal.Tests/SftpModeToolbarTemplateTests.cs
git commit -m "refactor: replace sftp popup actions with fluent menus"
```

---

### Task 5: 将内容区切换到 `VisibleItems` 并补足搜索空结果表达

**Files:**
- Modify: `src/SkylarkTerminal/Views/RightModes/SftpModeView.axaml`
- Modify: `src/SkylarkTerminal/ViewModels/RightPanelModes/SftpModeViewModel.cs`
- Modify: `tests/SkylarkTerminal.Tests/RightModeViewsBindingTests.cs`
- Modify: `tests/SkylarkTerminal.Tests/SftpModeStateTemplateTests.cs`
- Modify: `tests/SkylarkTerminal.Tests/SftpHeaderInteractionStateTests.cs`

**Step 1: Write the failing test**

在 `SftpHeaderInteractionStateTests.cs` 中追加：

```csharp
[Fact]
public async Task SearchQuery_ShouldFilterVisibleItems_AndExposeFilteredEmptyState()
{
    var vm = new SftpModeViewModel(
        new MockSftpService(),
        new SftpNavigationService("/"),
        []);

    await vm.ActivateAsync("mock-conn-01");

    vm.SearchQuery = "deploy";
    Assert.Contains(vm.VisibleItems, item => item.Name == "deploy.sh");

    vm.SearchQuery = "missing-keyword";
    Assert.Empty(vm.VisibleItems);
    Assert.True(vm.IsFilteredEmptyState);
}
```

`RightModeViewsBindingTests.cs` 更新为：

```csharp
[Theory]
[InlineData("SnippetsModeView.axaml", "SnippetItems")]
[InlineData("HistoryModeView.axaml", "HistoryItems")]
[InlineData("SftpModeView.axaml", "ActiveRightMode.VisibleItems")]
public void ModeViews_ShouldBindExpectedCollections(string file, string bindingKey)
{
    ...
}
```

`SftpModeStateTemplateTests.cs` 追加：

```csharp
Assert.Contains("FilteredEmptyStatePanel", xaml, StringComparison.Ordinal);
Assert.Contains("VisibleItems", xaml, StringComparison.Ordinal);
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~SftpHeaderInteractionStateTests|FullyQualifiedName~RightModeViewsBindingTests|FullyQualifiedName~SftpModeStateTemplateTests" -v minimal`  
Expected: FAIL，当前内容区仍绑定 `Items`，也没有筛选空状态。

**Step 3: Write minimal implementation**

`SftpModeViewModel` 中补齐：

```csharp
public bool HasVisibleItems => VisibleItems.Count > 0;
public bool IsFilteredEmptyState => IsLoadedState && Items.Count > 0 && VisibleItems.Count == 0;

partial void OnSearchQueryChanged(string value)
{
    _ = value;
    RebuildVisibleItems();
}

partial void OnShowHiddenFilesChanged(bool value)
{
    _ = value;
    RebuildVisibleItems();
}
```

`SftpModeView.axaml` 中将列表绑定改为：

```xml
<Border x:Name="FilteredEmptyStatePanel"
        Classes="RightSidebarSftpStateCard"
        IsVisible="{Binding DataContext.ActiveRightMode.IsFilteredEmptyState, RelativeSource={RelativeSource AncestorType=views:RightSidebarHostView}}">
    <StackPanel Spacing="6">
        <TextBlock FontSize="16"
                   FontWeight="SemiBold"
                   Text="没有匹配结果"/>
        <TextBlock Classes="RightSidebarSftpSecondaryText"
                   Text="请调整搜索关键字或关闭隐藏文件过滤。"/>
    </StackPanel>
</Border>

<ListBox Classes="RightSidebarSftpFileList"
         IsVisible="{Binding DataContext.ActiveRightMode.HasVisibleItems, RelativeSource={RelativeSource AncestorType=views:RightSidebarHostView}}"
         ItemsSource="{Binding DataContext.ActiveRightMode.VisibleItems, RelativeSource={RelativeSource AncestorType=views:RightSidebarHostView}}">
```

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~SftpHeaderInteractionStateTests|FullyQualifiedName~RightModeViewsBindingTests|FullyQualifiedName~SftpModeStateTemplateTests" -v minimal`  
Expected: PASS。

**Step 5: Commit**

```bash
git add src/SkylarkTerminal/Views/RightModes/SftpModeView.axaml \
        src/SkylarkTerminal/ViewModels/RightPanelModes/SftpModeViewModel.cs \
        tests/SkylarkTerminal.Tests/RightModeViewsBindingTests.cs \
        tests/SkylarkTerminal.Tests/SftpModeStateTemplateTests.cs \
        tests/SkylarkTerminal.Tests/SftpHeaderInteractionStateTests.cs
git commit -m "feat: filter sftp content through visible items"
```

---

### Task 6: 同步文档并完成回归验证

**Files:**
- Modify: `README.md`
- Modify: `docs/plans/2026-03-09-right-sidebar-layout-bugfix3-design.md`
- Modify: `docs/plans/2026-03-09-right-sidebar-layout-bugfix3-implementation-plan.md`

**Step 1: Update docs**

在 `README.md` 的右栏/SFTP 描述中同步以下事实：

- 右栏内容切换不再使用 `TransitioningContentControl`
- `SFTP` 地址栏采用 browse surface + overlay editor
- `历史路径 / 搜索` 位于 `PathChip` 内部 utility slot
- `More` 使用 Fluent 菜单，`显示隐藏文件` 为勾选项

同时如实现中对 design 的交互细节有微调（例如搜索 overlay 的具体打开方式），回写到 `bugfix3-design.md`，保持设计与实现一致。

**Step 2: Run targeted regression suite**

Run:

```bash
dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj \
  --filter "FullyQualifiedName~RightSidebar|FullyQualifiedName~RightPanel|FullyQualifiedName~Sftp" -v minimal
```

Expected:

- 所有右栏模板测试 PASS
- `SftpNavigationServiceTests` PASS
- `SftpHeaderInteractionStateTests` PASS
- `SftpModeToolbarTemplateTests` PASS
- `SftpModeStateTemplateTests` PASS

**Step 3: Run broader safety net**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj -v minimal`  
Expected: 全部 PASS；若存在与当前任务无关的历史失败，记录到实现备注中，不要静默忽略。

**Step 4: Manual verification checklist**

1. 打开 `SFTP` 模式，点击地址栏后，左侧 `Back / Forward` 不移动也不被遮挡。
2. 地址编辑 overlay 打开后，`历史路径 / 搜索` utility 按钮消失；关闭后恢复。
3. 点击 `历史路径`，看到最近路径菜单；选中某路径后能切换到对应目录。
4. 点击 `搜索`，能进入搜索 overlay；输入关键字后文件列表实时筛选。
5. 点击 `More`，出现 Fluent 风格菜单而不是黑框；`显示隐藏文件` 可勾选/反选。
6. 从 `SFTP` 切到 `Snippets / History` 时，不再闪出 `SFTP` 状态卡片或 `重试` 按钮。

**Step 5: Commit docs / final polish**

```bash
git add README.md \
        docs/plans/2026-03-09-right-sidebar-layout-bugfix3-design.md \
        docs/plans/2026-03-09-right-sidebar-layout-bugfix3-implementation-plan.md
git commit -m "docs: sync right sidebar bugfix3 implementation notes"
```

## 关键实现提醒

- `历史路径` 的菜单构建允许放在 `SftpToolbarHeaderView.axaml.cs` 的 opening 事件中完成，但“导航到某路径”的行为必须最终走 `SftpModeViewModel.NavigateHistoryPathCommand`，不要把业务导航逻辑写死在 View。
- `SearchQuery` 与 `ShowHiddenFiles` 的过滤逻辑都要走 `RebuildVisibleItems()`，不要在 View 层做集合裁切。
- `AddressOverlayTextBox` 和 `SearchOverlayTextBox` 只允许在 header overlay shell 内出现；不要回退为普通 `Flyout` 输入框。
- 如果 `RecentPaths` 的维护方式需要额外去重，优先在 `SftpNavigationService` 里统一处理，而不是让 ViewModel 或 View 重复整理。

## 最终验收标准

1. `SFTP` 头部在 340px 右栏宽度下仍保持单行稳定，不遮挡左右命令按钮。
2. `More` 不再是黑色空框，而是可维护的 Fluent 菜单。
3. `历史路径 / 搜索` 已从 `More` 挪到 `PathChip` 内部 utility slot。
4. `显示隐藏文件` 为真实勾选项，能影响文件列表结果。
5. 模式切换时不再出现 `SFTP` 状态卡片残影。

## 实施结果（2026-03-09）

### 已完成提交

- `ca8fcc4` `fix: remove right sidebar content transition`
- `d90f1d8` `feat: add sftp overlay and filter state model`
- `7f7a99a` `style: add sftp browse surface and overlay header`
- `2cc17c9` `refactor: replace sftp popup actions with fluent menus`
- `93ad620` `feat: filter sftp content through visible items`

### 已落地结果

- 右栏模式内容宿主已切换为静态 `ContentControl`。
- `SftpModeViewModel` 已承接 `HeaderOverlayMode`、`RecentPaths`、`SearchQuery`、`ShowHiddenFiles`、`VisibleItems` 与筛选空状态。
- `SFTP` 头部已实现 browse surface + utility slot + overlay shell，地址与搜索各自使用 overlay 输入框。
- 历史路径与 `More` 已切换到 `FAMenuFlyout`；`More` 当前只承载 `显示隐藏文件` 勾选项。
- `SFTP` 内容区已改为 `VisibleItems` 绑定，并在过滤无结果时显示独立状态卡片。

### 自动化验证记录

- 定向回归命令：
  `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~RightSidebar|FullyQualifiedName~RightPanel|FullyQualifiedName~Sftp" -v minimal`
- 全量测试命令：
  `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj -v minimal`
- 构建命令：
  `dotnet build src/SkylarkTerminal/SkylarkTerminal.csproj -v minimal`

### 手动验证备注

- 本计划中的 GUI 手动点验清单未在当前会话逐项执行，因为当前环境未启动图形界面。
- 下一步 TDD 阶段输入文档已生成：`docs/plans/2026-03-09-right-sidebar-layout-bugfix3-tdd-spec.md`
