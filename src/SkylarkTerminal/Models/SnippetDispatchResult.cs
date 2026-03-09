namespace SkylarkTerminal.Models;

public sealed record SnippetDispatchResult(int SuccessCount, int SkippedCount, int FailureCount)
{
    public static readonly SnippetDispatchResult Empty = new(0, 0, 0);
}
