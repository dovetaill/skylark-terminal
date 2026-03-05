namespace SkylarkTerminal.Models;

public sealed class RemoteFileNode
{
    public string Name { get; init; } = string.Empty;

    public string FullPath { get; init; } = string.Empty;

    public bool IsDirectory { get; init; }

    public long Size { get; init; }
}
