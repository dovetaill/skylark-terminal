using System;
using System.Threading;
using System.Threading.Tasks;

namespace SkylarkTerminal.Services;

public interface ISshTerminalSession : IDisposable
{
    string SessionId { get; }

    bool IsConnected { get; }

    event EventHandler<string>? OutputReceived;

    event EventHandler<string>? Closed;

    event EventHandler<Exception>? Faulted;

    Task SendAsync(string data, CancellationToken cancellationToken = default);

    Task ResizeAsync(
        uint columns,
        uint rows,
        uint widthPixels,
        uint heightPixels,
        CancellationToken cancellationToken = default);

    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
