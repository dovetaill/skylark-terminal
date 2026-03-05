using System.Threading.Tasks;

namespace SkylarkTerminal.Services;

public interface IClipboardService
{
    Task SetTextAsync(string text);
}
