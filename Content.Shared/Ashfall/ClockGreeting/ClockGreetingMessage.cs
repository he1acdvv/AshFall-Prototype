using Robust.Shared.Serialization;

namespace Content.Shared.Ashfall.ClockGreeting;

/// <summary>
/// Shift greeting data sent upon initial spawn: game date and shift elapsed duration.
/// </summary>
[Serializable, NetSerializable]
public sealed class ClockGreetingMessage(DateTime date, TimeSpan shift) : EntityEventArgs
{
    public DateTime Date { get; } = date;
    public TimeSpan Shift { get; } = shift;
}
