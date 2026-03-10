# Skylark Terminal

基于 **Avalonia UI + FluentAvalonia** 构建的现代化 SSH/SFTP 桌面终端客户端原型，目标体验对标 Termius / Tabby，并贴合 Windows 11 Fluent Design 视觉风格。

## 项目现状

当前仓库已完成 UI 壳层、终端会话主流程以及基础 SSH 接入：

- 自绘标题栏（无边框窗口）+ 窗口拖拽 + 最小化/最大化/关闭
- 顶部菜单（Settings/Language/Help/About）已接入真实命令与弹窗
- 左侧资产区：图标导航、Tree/Flat 单按钮模式切换、右键菜单（按功能分组，支持导入/导出、批量打开）
- 密钥资产面板：独立右键语义（新建密码/新建密钥文件/导入密钥文件/导出密钥）
- Flat 模式支持 Windows 风格多选：`Ctrl/Shift` 扩展选择、右键保留多选、左键拖拽框选
- 资产面板顶部：紧凑图标工具条（搜索/展开收起/创建/模式切换），支持点击展开搜索框与空查询自动收起
- 中间工作区：FluentAvalonia `TabView` 多标签；支持双击资产连接开新标签、标签标题显示连接名、右键标签操作（Duplicate/Close Others/Close Left/Close Right/Close All）
- 启动默认页：`Quick Start` 入口页，支持最近连接卡片、搜索过滤、快捷跳转 Hosts 与新建标签
- Quick Start 定位策略：最近连接可在 `Tree/Flat` 两种资产视图中自动定位并高亮对应 Host
- 无连接配置标签会稳定显示 Quick Start，不再出现 `Disconnected / invalid SSH config` 告警条
- 真实终端会话：已接入 `SSH.NET` + `Iciclecreek.Avalonia.Terminal`，支持连接状态流转（Connecting/Connected/Disconnected/Faulted）
- 终端交互：支持 ANSI/VT100 基础键位映射、右键复制/粘贴/清屏、窗口尺寸变化同步到远端 PTY
- 右侧工具区：已完成 `RightSidebarHostView` 容器化（左对齐 icon-only ModeRail + 模式级 HeaderSlot + 静态 `ContentControl`）
- 右侧工具模式架构：`IRightPanelModeViewModel` + `Snippets/History/Sftp` 子 ViewModel + `ModeActionDescriptor` 元数据动作模型
- Snippets 面板：browse 已切到 `TreeView`，支持分类树、实时搜索、Create/Edit/ViewMore 三态、双击粘贴、root/category/item 三层右键菜单
- Snippets 持久化：数据落地到本地 `LocalApplicationData/SkylarkTerminal/snippets.json`，损坏 JSON 会自动备份为 `.broken`
- Snippets 执行策略：`Run` 只发送到当前终端并补 `\r`，`Paste` 不补换行，`Run in all tabs` 仅命中已连接 SSH tabs 且会二次确认
- Snippets 编辑流：界面文案已统一为中文；`Create` 显示 `保存/取消`，`Edit` 显示 `保存/删除/取消`，`ViewMore` 提供 `返回`；分类字段支持选择已有分类或直接输入新名称，tags 不再暴露在 UI
- SFTP 导航：`SftpNavigationService` 已接入（Back/Forward/Address/Refresh/Up），头部为固定 `Grid` toolbar，地址栏采用 `browse surface + overlay editor` 双层结构，`历史路径 / 搜索` 收进 `PathChip` 内部 utility slot
- SFTP 内容区：模式自身维护 `Idle / Loading / Loaded / Empty / Error` 状态机，文件列表通过 `VisibleItems` 承接搜索与隐藏文件过滤，并补充筛选空状态表达
- 右侧快捷键：`Ctrl+1/2/3` 快速切换 `Snippets/History/SFTP`
- 左右侧栏可拖拽缩放，收起时 splitter 与内容列保持同步联动
- 默认深色主题，支持顶部图标按钮切换深浅色，Settings 支持透明/不透明窗口材质切换
- 日志策略：默认只写入错误级别日志（`ERROR`），用于保留异常与崩溃定位信息

> 说明：为避免敏感信息落库，仓库默认资产不再内置任何硬编码测试连接数据。

## 最近迭代（Git Log 摘要）

- `1a5ecfc` (2026-03-08): 补充 Quick Start 优化方案文档（step1），明确后续迭代拆分与验收范围。
- `e53f610` (2026-03-08): 完成 Quick Start Host 定位逻辑与终端渲染相关测试（含行渲染与主题 token 回退校验）。
- `3a17b59` (2026-03-08): 重构 Quick Start 页面并接入“最近连接”入口，强化从入口到资产区/工作区的跳转链路。
- `8bf7c52` (2026-03-06): 接入真实 SSH 终端会话并默认打开 Quick Start，完善连接状态流转与会话初始化。
- `335c18c` (2026-03-06): 统一右键菜单布局并收紧弹窗宽度，修复上下文菜单视觉与密度不一致问题。
- `b9c840a` (2026-03-06): 打通 Workspace `TabView` 与资产区联动，新增批量关闭标签等操作闭环。

## 技术栈

- .NET: `net10.0`
- UI: `Avalonia 11.3.12`
- Fluent 控件与主题: `FluentAvaloniaUI 2.5.0`
- MVVM: `CommunityToolkit.Mvvm 8.4.0`
- DI: `Microsoft.Extensions.DependencyInjection 10.0.0`
- SSH: `SSH.NET 2025.1.0`
- 终端控件: `Iciclecreek.Avalonia.Terminal 1.0.7`
- 测试: `xUnit`

## 目录结构

```text
.
├── src/SkylarkTerminal
│   ├── Models/                # 资产、连接、工具面板等数据模型
│   ├── Services/              # 业务接口与实现
│   │   └── Mock/              # Mock 服务实现
│   ├── ViewModels/            # MVVM 视图模型
│   ├── Views/                 # Avalonia XAML 视图
│   ├── App.axaml              # 全局主题与样式入口
│   ├── App.axaml.cs           # DI 注册与应用初始化
│   └── Program.cs             # 桌面应用入口
├── tests/SkylarkTerminal.Tests
│   └── *.cs                   # 交互策略、Quick Start 定位、终端渲染与服务会话等单元测试
└── SkylarkTerminal.slnx
```

## 快速开始

### 环境要求

- .NET SDK 10.0+
- Linux/macOS/Windows 均可构建；GUI 运行需可用显示环境

### 还原与构建

```bash
dotnet restore SkylarkTerminal.slnx
dotnet build SkylarkTerminal.slnx
```

### 运行应用

```bash
dotnet run --project src/SkylarkTerminal/SkylarkTerminal.csproj
```

如果你在无图形界面的 Linux 服务器运行，可能出现 `XOpenDisplay failed`。这是显示环境缺失导致，属于预期现象。此场景建议：

- 仅执行构建与测试验证（推荐）
- 或使用虚拟显示（如 `xvfb-run`）后再启动 GUI

示例（按需）：

```bash
xvfb-run -a dotnet run --project src/SkylarkTerminal/SkylarkTerminal.csproj
```

### 运行测试

```bash
dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj
```

### Windows x64 发布脚本

发布版与调试版现在使用两个独立脚本，避免混用参数：

```bash
./build-win-x64.sh
./build-win-x64-debug.sh
```

- `./build-win-x64.sh`：生成 `Release` 的 `win-x64` 自包含发布目录，并输出根目录 `publish.zip`
- `./build-win-x64-debug.sh`：生成 `Debug` 的 `win-x64` 自包含发布目录，并输出根目录 `publish-debug.zip`
- 两个脚本都支持 `--no-single-file`，用于切换到多文件发布目录
- `Release` 产物不会包含 `Iciclecreek.Avalonia.Terminal.Fork.pdb`
- `Debug` 产物会保留 `Iciclecreek.Avalonia.Terminal.Fork.pdb`，便于调试

当前测试覆盖的核心行为包含：

- 左/右侧栏展开收起状态与宽度联动
- 资产视图模式切换（Tree/Flat）
- 资产增删改操作
- Tab 增删改与右键操作逻辑（含 Close Left/Right/Others/All）
- 右侧工具模式架构、模式级 header slot、静态内容宿主与 `SFTP` overlay 状态切换
- SFTP 导航服务路径栈（Back/Forward/Up/RecentPaths）、地址提交解析与隐藏文件过滤
- 顶部菜单命令（Settings/Language/Help/About）调用链
- Flat 模式多选后的批量导出与批量打开标签行为
- 密钥资产面板专属命令行为与可执行状态约束
- Quick Start 最近连接在 `Tree/Flat` 下的定位与高亮策略
- 终端行渲染与主题 token 调色板回退行为
- SSH 终端会话创建、关闭与异常链路验证
- Snippets repository、terminal bridge、模式状态机与 browse/editor/detail 模板绑定

## 核心接口（Mock Driven）

在接入真实 SSH.NET 前，项目通过接口隔离业务实现：

```csharp
public interface ISshConnectionService
{
    Task<bool> ConnectAsync(ConnectionConfig config);
    Task DisconnectAsync(string connectionId);
    Task RunCommandAsync(string connectionId, string command);
    Task<ISshTerminalSession> CreateTerminalSessionAsync(
        ConnectionConfig config,
        CancellationToken cancellationToken = default);
}

public interface ISftpService
{
    Task<List<RemoteFileNode>> ListDirectoryAsync(string connectionId, string path);
}
```

另外顶部菜单弹窗通过 `IAppDialogService` 抽象，避免 ViewModel 与具体 UI 弹窗组件强耦合。
终端会话通过 `ISshTerminalSession` 暴露 `OutputReceived/Closed/Faulted` 事件与 `SendAsync/ResizeAsync` 操作。

## UI 交互说明

- 顶部左侧 `Menu`：打开 Settings / Language / Help / About，其中 Settings 可切换窗口透明/不透明
- 顶部主题按钮：月亮/太阳图标切换深浅色
- 左侧资产栏底部按钮：展开/收起资产面板；资产头部单按钮在 `Tree/Flat` 两种模式间切换
- 资产工具条：`资产列表` 标题 + 图标操作区，顶部与列表通过独立分割线和轻色阶区分，符合 Win11 低对比层次
- 搜索交互：点击搜索图标展开输入框；空内容时点击任意非搜索区域自动收起；有内容时保持展开并实时过滤
- 中央 Tab：支持双击左侧连接快速开页签、右键批量管理与会话复制
- 右上角 Tools 按钮：展开/收起右侧工具面板（保留上次选中模式），并支持拖拽到阈值后自动收起
- 右侧 ModeRail：左对齐 icon-only ghost tile 切换 `Snippets/History/SFTP`
- 右侧 Header：按模式切换头部；`Snippets/History` 使用轻量动作条，`SFTP` 使用固定 `Grid` toolbar，并移除模式内容切换过渡
- Snippets：browse 顶部提供 `新建代码块` 与搜索框；分类与代码块以树节点呈现，双击代码块只粘贴不执行，root/category/item 都有独立右键菜单，分类/代码块都支持删除
- Snippets 批量执行：`Run in all tabs` 只统计并命中已连接 SSH tabs，执行前弹确认
- Snippets 空白区右键：browse 空白区域支持 `新建代码块 / 新建分类 / 从剪贴板创建 / 清空搜索`
- SFTP 地址栏：默认显示带 utility slot 的路径 chip；点击 chip 打开地址 overlay，点击搜索按钮打开搜索 overlay，`Enter` 提交，`Esc` 或失焦收起
- SFTP More 菜单：使用 Fluent `FAMenuFlyout`，当前仅承载 `显示隐藏文件` 勾选项
- 右侧快捷键：`Ctrl+1/2/3` 快速切换模式

## 后续开发建议

1. 连接配置持久化：接入安全存储（避免明文），支持连接模板与最近会话
2. SFTP 真实实现：替换当前 Mock 列表，补齐上传/下载/重命名/权限操作
3. 终端能力完善：补齐更完整的 ANSI/VT 特性与会话恢复能力
4. 多平台发布：完善 Windows/Linux/macOS 打包脚本与签名流程

## 许可证

当前仓库未附带 LICENSE 文件。如需开源发布，请补充许可证声明。
