# 2026-03-09 Win-x64 Publish Modes Design

## Goal

在保留现有 `build-win-x64.sh` 作为发布入口的前提下，新增独立的调试发布脚本，并让 `Release` 产物不再包含 `Iciclecreek.Avalonia.Terminal.Fork.pdb`。

## Approved Scope

- 保留 `build-win-x64.sh`，继续负责 `Release` 的 `win-x64` 发布打包。
- 新增 `build-win-x64-debug.sh`，流程与目录结构和发布脚本保持一致，但使用 `Debug` 配置。
- `Release` 下禁用 `src/ThirdParty/Iciclecreek.Avalonia.Terminal.Fork/Iciclecreek.Avalonia.Terminal.Fork.csproj` 的 PDB 生成。
- `Debug` 下继续保留 PDB，便于调试。
- 为避免调试构建覆盖发布压缩包，调试脚本输出 `publish-debug.zip`。

## Options Considered

### Option 1: 为现有脚本增加 `--config`

优点：
- 只有一个脚本
- 逻辑不重复

缺点：
- 与用户偏好的“分开入口”不符
- 日后执行时容易误选参数

### Option 2: 新增独立调试脚本

优点：
- 发布版和调试版入口明确
- 与现有脚本心智模型一致
- 可以在不影响发布脚本的前提下演进调试流程

缺点：
- 会有少量脚本重复

## Decision

采用 Option 2。

## Verification

- 运行 `./build-win-x64.sh`，确认 `Release publish` 目录不再出现 `Iciclecreek.Avalonia.Terminal.Fork.pdb`。
- 运行 `./build-win-x64-debug.sh`，确认 `Debug publish` 目录仍保留该 PDB。
- 两个脚本都能正常生成 zip，且文件名不冲突。
