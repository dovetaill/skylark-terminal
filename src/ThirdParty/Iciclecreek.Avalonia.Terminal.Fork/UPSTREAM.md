# Iciclecreek.Avalonia.Terminal Fork Sync Notes

## Current baseline
- Upstream package: `Iciclecreek.Avalonia.Terminal` `1.0.7`
- Local fork path: `src/ThirdParty/Iciclecreek.Avalonia.Terminal.Fork/`
- Stage: boundary setup (no rendering behavior override yet)

## Sync checklist
1. Identify upstream tag or commit matching the target package version.
2. Record upstream source URL and commit hash here.
3. Bring source files into this folder as minimal subset needed by rendering extensions.
4. Keep PTY/input/session-management paths unchanged unless explicitly required.
5. Validate with `dotnet build` and terminal smoke checks after each sync.

## Planned fork scope
- Terminal rendering layer extension points (row stripe, ANSI remap, fallback highlighting).
- No protocol/session behavior changes.
