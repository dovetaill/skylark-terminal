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
- 无连接配置标签会稳定显示 Quick Start，不再出现 `Disconnected / invalid SSH config` 告警条
- 真实终端会话：已接入 `SSH.NET` + `Iciclecreek.Avalonia.Terminal`，支持连接状态流转（Connecting/Connected/Disconnected/Faulted）
- 终端交互：支持 ANSI/VT100 基础键位映射、右键复制/粘贴/清屏、窗口尺寸变化同步到远端 PTY
- 右侧工具区：Snippet / History / SFTP 视图切换与 Mock 数据展示，支持拖拽与阈值自动收起
- 左右侧栏可拖拽缩放，收起时 splitter 与内容列保持同步联动
- 默认深色主题，支持顶部图标按钮切换深浅色，Settings 支持透明/不透明窗口材质切换
- 日志策略：默认只写入错误级别日志（`ERROR`），用于保留异常与崩溃定位信息

> 说明：为避免敏感信息落库，仓库默认资产不再内置任何硬编码测试连接数据。

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
- Tab 增删改与右键操作逻辑（含 Close Left/Right/Others/All）
- 右侧工具面板视图切换
- 顶部菜单命令（Settings/Language/Help/About）调用链
- Flat 模式多选后的批量导出与批量打开标签行为
- 密钥资产面板专属命令行为与可执行状态约束

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
- 右上角 Tools 按钮：展开/收起右侧工具面板（保留上次选中视图），并支持拖拽到阈值后自动收起

## 后续开发建议

1. 连接配置持久化：接入安全存储（避免明文），支持连接模板与最近会话
2. SFTP 真实实现：替换当前 Mock 列表，补齐上传/下载/重命名/权限操作
3. 终端能力完善：补齐更完整的 ANSI/VT 特性与会话恢复能力
4. 多平台发布：完善 Windows/Linux/macOS 打包脚本与签名流程

## 许可证

当前仓库未附带 LICENSE 文件。如需开源发布，请补充许可证声明。
