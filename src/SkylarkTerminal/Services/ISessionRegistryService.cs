using SkylarkTerminal.Models;
using System.Threading;
using System.Threading.Tasks;

namespace SkylarkTerminal.Services;

public interface ISessionRegistryService
{
    ValueTask<ISshTerminalSessionHandle> GetOrCreateAsync(
        string tabId,
        ConnectionConfig config,
        CancellationToken cancellationToken = default);

    void Attach(string tabId, ISshTerminalSessionHandle handle);

    bool TryGet(string tabId, out ISshTerminalSessionHandle? handle);

    bool TryDetach(string tabId, out ISshTerminalSessionHandle? handle);

    Task DisposeAsync(string tabId, CancellationToken cancellationToken = default);
}
