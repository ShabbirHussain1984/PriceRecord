using Serilog.Core;
using Serilog.Events;

namespace PriceRecord.UI;

/// <summary>
/// Serilog sink that forwards events to whoever listens (the main form's log box).
/// Events raised before a listener attaches are dropped; the file sink still has them.
/// </summary>
public sealed class UiLogSink : ILogEventSink
{
    public event Action<LogEvent>? Emitted;

    public void Emit(LogEvent logEvent) => Emitted?.Invoke(logEvent);
}
