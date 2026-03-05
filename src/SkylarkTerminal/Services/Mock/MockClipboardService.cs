using System.Threading.Tasks;

namespace SkylarkTerminal.Services.Mock;

public sealed class MockClipboardService : IClipboardService
{
    public Task SetTextAsync(string text)
    {
        return Task.CompletedTask;
    }
}
