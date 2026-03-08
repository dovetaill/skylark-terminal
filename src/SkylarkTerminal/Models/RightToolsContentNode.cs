namespace SkylarkTerminal.Models;

public abstract record RightToolsContentNode;

public sealed record SnippetsRightToolsContent : RightToolsContentNode;

public sealed record HistoryRightToolsContent : RightToolsContentNode;

public sealed record SftpRightToolsContent : RightToolsContentNode;
