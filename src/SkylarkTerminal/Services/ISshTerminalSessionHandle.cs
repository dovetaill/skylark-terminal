namespace SkylarkTerminal.Services;

public interface ISshTerminalSessionHandle
{
    string TabId { get; }

    ISshTerminalSession Session { get; }
}
