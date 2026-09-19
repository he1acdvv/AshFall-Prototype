using Content.Server.GameTicking;
using Content.Shared.Ashfall.ClockGreeting;
using Content.Shared.GameTicking;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Ashfall.ClockGreeting;

public sealed partial class ClockGreetingSystem : EntitySystem
{
    private const int GameYear = 2291;

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private IRobustRandom _random = default!;

    private readonly HashSet<NetUserId> _greeted = new();
    private DateTime _roundBaseDate;
    private TimeSpan _initialShiftOffset;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn);
        GenerateRoundTimes();
    }

    private void GenerateRoundTimes()
    {
        var month = _random.Next(1, 13);
        var day = _random.Next(1, DateTime.DaysInMonth(GameYear, month) + 1);
        var hour = _random.Next(0, 24);
        var minute = _random.Next(0, 60);
        var second = _random.Next(0, 60);

        _roundBaseDate = new DateTime(GameYear, month, day, hour, minute, second);
        _initialShiftOffset = TimeSpan.FromHours(_random.Next(1, 7))
                              + TimeSpan.FromMinutes(_random.Next(5, 55))
                              + TimeSpan.FromSeconds(_random.Next(0, 60));
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _greeted.Clear();
        GenerateRoundTimes();
    }

    private void OnPlayerSpawn(PlayerSpawnCompleteEvent ev)
    {
        if (ev.Silent || !_greeted.Add(ev.Player.UserId))
            return;

        var elapsed = _timing.CurTime - _ticker.RoundStartTimeSpan;
        var gameDate = _roundBaseDate.Add(elapsed);
        var shift = _initialShiftOffset + elapsed;
        RaiseNetworkEvent(new ClockGreetingMessage(gameDate, shift), ev.Player);
    }
}
