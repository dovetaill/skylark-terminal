# Skylark Terminal

基于 **Avalonia UI + FluentAvalonia** 构建的现代化 SSH/SFTP 桌面终端客户端原型，目标体验对标 Termius / Tabby，并贴合 Windows 11 Fluent Design 视觉风格。

## 项目现状

当前仓库已完成 UI 壳层与交互主流程（Mock 驱动）：

- 自绘标题栏（无边框窗口）+ 窗口拖拽 + 最小化/最大化/关闭
- 顶部菜单（Settings/Language/Help/About）已接入真实命令与弹窗
- 左侧资产区：图标导航、Tree/Flat 单按钮模式切换、右键菜单（新建/编辑/删除）
- 中间工作区：FluentAvalonia `TabView` 多标签、右键标签操作（Duplicate/Close Others/Close Right/Close All）
- 右侧工具区：Snippet / History / SFTP 视图切换与 Mock 数据展示，支持拖拽与阈值自动收起
- 左右侧栏可拖拽缩放，收起时 splitter 与内容列保持同步联动
- 默认深色主题，支持顶部图标按钮切换深浅色，Settings 支持透明/不透明窗口材质切换

> 说明：当前未接入真实 SSH.NET，仅提供服务接口与 Mock 实现，UI 可完整联调。

## 最近迭代（Git Log 摘要）

- `2f9a20e` (2026-03-05): 修复资产面板显隐状态与 splitter 联动；统一资产模式命名为 `Tree/Flat`；修复右侧栏阈值收缩后不可继续拖拽的问题。
- `7ac6003` (2026-03-05): 重构主窗口壳层，统一资产视图切换交互，补全 Settings 弹窗返回逻辑与透明度切换状态。
- `72c9f22` (2026-03-05): 实现自绘顶部状态栏与无边框窗口，完善顶部菜单命令链与窗口交互细节。
- `830e20a` (2026-03-05): 完善项目 README，并清理误跟踪的 `obj` 噪音文件。

## 技术栈

- .NET: `net10.0`
- UI: `Avalonia 11.3.12`
- Fluent 控件与主题: `FluentAvaloniaUI 2.5.0`
- MVVM: `CommunityToolkit.Mvvm 8.4.0`
- DI: `Microsoft.Extensions.DependencyInjection 10.0.0`
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
│   └── UnitTest1.cs           # ViewModel 行为测试（含菜单弹窗命令）
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

当前测试覆盖的核心行为包含：

- 左/右侧栏展开收起状态与宽度联动
- 资产视图模式切换（Tree/Flat）
- 资产增删改操作
- Tab 增删改与右键操作逻辑
- 右侧工具面板视图切换
- 顶部菜单命令（Settings/Language/Help/About）调用链

## 核心接口（Mock Driven）

在接入真实 SSH.NET 前，项目通过接口隔离业务实现：

```csharp
public interface ISshConnectionService
{
    Task<bool> ConnectAsync(ConnectionConfig config);
    Task DisconnectAsync(string connectionId);
    Task RunCommandAsync(string connectionId, string command);
}

public interface ISftpService
{
    Task<List<RemoteFileNode>> ListDirectoryAsync(string connectionId, string path);
}
```

另外顶部菜单弹窗通过 `IAppDialogService` 抽象，避免 ViewModel 与具体 UI 弹窗组件强耦合。

## UI 交互说明

- 顶部左侧 `Menu`：打开 Settings / Language / Help / About，其中 Settings 可切换窗口透明/不透明
- 顶部主题按钮：月亮/太阳图标切换深浅色
- 左侧资产栏底部按钮：展开/收起资产面板；资产头部单按钮在 `Tree/Flat` 两种模式间切换
- 中央 Tab：支持右键管理与快速复制会话
- 右上角 Tools 按钮：展开/收起右侧工具面板（保留上次选中视图），并支持拖拽到阈值后自动收起

## 后续开发建议

1. 接入 SSH.NET：实现 `ISshConnectionService` 与 `ISftpService` 的真实版本
2. 终端渲染：将 Tab 内容中的占位面板替换为 ANSI 终端控件
3. 配置持久化：保存主题、语言、最近会话、工具面板状态
4. 资产管理：接入本地加密存储或远程同步
5. 多平台发布：完善 Windows/Linux/macOS 打包脚本

## 许可证

当前仓库未附带 LICENSE 文件。如需开源发布，请补充许可证声明。
