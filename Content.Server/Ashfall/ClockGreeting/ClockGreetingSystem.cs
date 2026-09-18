using Content.Server.GameTicking;
using Content.Shared.Ashfall.ClockGreeting;
using Content.Shared.GameTicking;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Server.Ashfall.ClockGreeting;

public sealed partial class ClockGreetingSystem : EntitySystem
{
    private const int GameYear = 2291;
    private const int EarthTimeOffsetHours = 3; // Moscow / Earth standard timezone offset

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private GameTicker _ticker = default!;

    private readonly HashSet<NetUserId> _greeted = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _greeted.Clear();
    }

    private void OnPlayerSpawn(PlayerSpawnCompleteEvent ev)
    {
        if (ev.Silent || !_greeted.Add(ev.Player.UserId))
            return;

        var now = DateTime.UtcNow.AddHours(EarthTimeOffsetHours);
        var gameDate = new DateTime(GameYear, now.Month, now.Day, now.Hour, now.Minute, now.Second);
        var shift = _timing.CurTime - _ticker.RoundStartTimeSpan;
        RaiseNetworkEvent(new ClockGreetingMessage(gameDate, shift), ev.Player);
    }
}
