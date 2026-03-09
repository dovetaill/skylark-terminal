namespace SkylarkTerminal.Models;

public sealed class RemoteFileNode
{
    public string Name { get; init; } = string.Empty;

    public string FullPath { get; init; } = string.Empty;

    public bool IsDirectory { get; init; }

    public long Size { get; init; }

    public string IconGlyph => IsDirectory ? "\uE8B7" : "\uE7C3";

    public string KindLabelZh => IsDirectory ? "目录" : "文件";

    public string SizeLabel => IsDirectory ? "目录" : FormatSize(Size);

    private static string FormatSize(long size)
    {
        if (size < 1024)
        {
            return $"{size} B";
        }

        if (size < 1024 * 1024)
        {
            return $"{size / 1024d:0.#} KB";
        }

        if (size < 1024 * 1024 * 1024)
        {
            return $"{size / (1024d * 1024d):0.#} MB";
        }

        return $"{size / (1024d * 1024d * 1024d):0.#} GB";
    }
}
