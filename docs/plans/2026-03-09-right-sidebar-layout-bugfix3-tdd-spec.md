# RightSidebar Layout Bugfix3 TDD Spec

## 目标

为 `RightSidebar` 第三轮 `SFTP` 体验修复补齐更系统的测试覆盖，重点锁定以下能力：

- 右栏模式切换时 `SFTP` 内容不再残影。
- `SFTP` header 的 browse surface / overlay shell / Fluent menu 结构保持稳定。
- `VisibleItems` 过滤链路在地址导航、历史路径导航、搜索与隐藏文件切换下持续正确。

## 本轮涉及的核心类

- `src/SkylarkTerminal/ViewModels/RightPanelModes/SftpModeViewModel.cs`
- `src/SkylarkTerminal/Services/SftpNavigationService.cs`
- `src/SkylarkTerminal/Views/RightHeaders/SftpToolbarHeaderView.axaml`
- `src/SkylarkTerminal/Views/RightHeaders/SftpToolbarHeaderView.axaml.cs`
- `src/SkylarkTerminal/Views/RightModes/SftpModeView.axaml`
- `src/SkylarkTerminal/Views/RightSidebarHostView.axaml`

## 关键方法与状态点

### `SftpModeViewModel`

- `OpenAddressOverlayCommand`
  预期：切到 `HeaderOverlayMode.Address`，显示地址 overlay，隐藏 utility strip。
- `OpenSearchOverlayCommand`
  预期：切到 `HeaderOverlayMode.Search`，隐藏地址编辑态，显示搜索 overlay。
- `CloseHeaderOverlayCommand`
  预期：关闭任意 overlay，恢复 browse surface。
- `CommitAddressAsync()`
  预期：提交地址、重新加载目录、关闭 overlay。
- `NavigateHistoryPathAsync(string? path)`
  预期：通过 `SftpNavigationService` 切换目录并刷新 `VisibleItems`。
- `RebuildVisibleItems()`
  预期：统一承接 `SearchQuery` 与 `ShowHiddenFiles` 的过滤，不在 View 层做集合裁切。
- `HasVisibleItems`
  预期：仅在 `LoadState == Loaded` 且 `VisibleItems.Count > 0` 时为真。
- `IsFilteredEmptyState`
  预期：原始 `Items` 非空、`VisibleItems` 为空、且处于 `Loaded` 时为真。

### `SftpNavigationService`

- `NavigateTo(string path)`
  预期：维护 `CurrentPath`、清空 forward 栈并更新 `RecentPaths`。
- `GoBack()` / `GoForward()` / `GoUp()`
  预期：与 `RecentPaths` 协同工作，不产生重复路径污染。
- `TryResolveAddressInput(string input)`
  预期：处理空输入、相对路径格式与结尾 `/` 归一化。

### `SftpToolbarHeaderView`

- `OnAddressChipClick`
  预期：打开地址 overlay 并聚焦 `AddressOverlayTextBox`。
- `OnSearchButtonClick`
  预期：打开搜索 overlay 并聚焦 `SearchOverlayTextBox`。
- `OnOverlayEditorLostFocus`
  预期：关闭 overlay，但不要影响浏览态按钮布局。
- `OnOverlayEditorKeyDown`
  预期：`Esc` 关闭 overlay 并把焦点还给 `AddressChipButton`。
- `OnHistoryFlyoutOpening`
  预期：每次基于最新 `RecentPaths` 动态构建菜单项，避免过期菜单缓存。

## 建议新增或强化的测试

### 1. ViewModel 行为测试

- 搜索 overlay 打开后再打开地址 overlay，应由 `Search` 切回 `Address`，且不会同时显示两个 overlay。
- 在 `ShowHiddenFiles = true` 且 `SearchQuery` 非空时，`VisibleItems` 应同时满足两个过滤条件。
- `NavigateHistoryPathAsync(null)` 与空字符串输入应直接返回，不触发目录加载。
- `CommitAddressAsync()` 在空白输入时应保持当前路径，不破坏 `RecentPaths` 顺序。

### 2. 导航服务测试

- `RecentPaths` 对重复路径去重时，应保持“最近访问优先”的顺序。
- `GoBack()` / `GoForward()` 后，`RecentPaths` 顶部应反映最新所在目录。
- `NormalizePath` 对 `\\var\\log\\`、`var/log/`、`/` 的处理应稳定。

### 3. 模板/结构测试

- `RightSidebarHostView.axaml` 持续禁止 `TransitioningContentControl`。
- `SftpToolbarHeaderView.axaml` 持续要求：
  `AddressHistoryButton`、`AddressSearchButton`、`AddressOverlayRoot`、`AddressOverlayTextBox`、`SearchOverlayTextBox`、`ui:FAMenuFlyout`、`ui:ToggleMenuFlyoutItem`。
- `SftpModeView.axaml` 持续要求：
  `FilteredEmptyStatePanel`、`VisibleItems`、`HasVisibleItems`、`IsFilteredEmptyState`。

### 4. 交互集成测试

- 历史路径菜单打开后，菜单项数量应受 `RecentPaths.Take(8)` 限制。
- 选择历史路径菜单项后，`CurrentPath`、`AddressInput` 与 `VisibleItems` 应同步更新。
- 切换 `ShowHiddenFiles` 后，`.env` 这类隐藏文件应在 UI 绑定集合中显隐正确。

## 边缘情况

- 在 overlay 打开状态下切换右栏模式，旧的 `SFTP` overlay 不应残留到 `Snippets / History`。
- 目录真实为空时应显示 `EmptyStatePanel`，而不是 `FilteredEmptyStatePanel`。
- 筛选无结果时应显示 `FilteredEmptyStatePanel`，但不应把 `LoadState` 改成 `Empty`。
- `RecentPaths` 初始值应包含根路径 `/`，避免历史菜单首次为空。
- 当前实现中 `MoreCommands` 仍保留旧元数据列表，但 `More` 菜单已不再直接绑定该集合；后续如继续收敛，可考虑删除未使用元数据并同步测试。

## 手动验证建议

- 在 340px 右栏宽度下点开地址 overlay，观察 `Back / Forward / Refresh / Up / More` 是否完全不跳动。
- 在搜索 overlay 中输入 `deploy`、`missing-keyword`、空字符串，分别确认列表、空状态和恢复逻辑。
- 勾选与取消勾选 `显示隐藏文件`，确认 `.env` 的显隐与搜索结果一致。
- 反复切换 `SFTP -> Snippets -> History -> SFTP`，确认不再出现旧状态卡片闪现。

## 下一步建议

- 下一阶段优先补 `SftpToolbarHeaderView.axaml.cs` 的行为测试或 UI 自动化测试，覆盖焦点切换、菜单重建与 overlay 关闭时机。
- 若后续删除 `MoreCommands` 旧元数据列表，应先补测试，再做收敛式重构。
