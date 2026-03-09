using System.Threading.Tasks;

namespace SkylarkTerminal.Services.Mock;

public sealed class MockClipboardService : IClipboardService
{
    public string? Text { get; set; }

    public Task SetTextAsync(string text)
    {
        Text = text;
        return Task.CompletedTask;
    }

    public Task<string?> GetTextAsync()
    {
        return Task.FromResult(Text);
    }
}
