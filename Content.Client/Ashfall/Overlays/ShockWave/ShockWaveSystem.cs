using Content.Shared.Ashfall.Overlays.ShockWave;
using Content.Shared.Explosion;
using Content.Shared.Explosion.Components;
using Robust.Client.Graphics;
using Robust.Shared.GameStates;
using Robust.Shared.Timing;

namespace Content.Client.Ashfall.Overlays.ShockWave;

public sealed partial class ShockWaveSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayMan = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly HashSet<EntityUid> _registered = new();
    private readonly HashSet<EntityUid> _spawnedForExplosion = new();
    private ShockWaveOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new ShockWaveOverlay();
        _overlayMan.AddOverlay(_overlay);

        SubscribeLocalEvent<ShockWaveComponent, ComponentStartup>(OnShockWaveStartup);
        SubscribeLocalEvent<ShockWaveComponent, ComponentRemove>(OnShockWaveRemoved);
        SubscribeLocalEvent<ExplosionVisualsComponent, ComponentHandleState>(OnExplosionState);
    }

    public override void Shutdown()
    {
        _overlayMan.RemoveOverlay(_overlay);
        _registered.Clear();
        _spawnedForExplosion.Clear();
        base.Shutdown();
    }

    private void OnExplosionState(EntityUid uid, ExplosionVisualsComponent comp, ref ComponentHandleState args)
    {
        if (args.Current is not ExplosionVisualsState state)
            return;

        if (state.Epicenter != Robust.Shared.Map.MapCoordinates.Nullspace && _spawnedForExplosion.Add(uid))
        {
            Spawn("AshfallEffectShockWave", state.Epicenter);
        }
    }

    private void OnShockWaveStartup(Entity<ShockWaveComponent> ent, ref ComponentStartup args)
    {
        if (!_registered.Add(ent.Owner))
            return;

        if (ent.Comp.InitTime == TimeSpan.Zero)
            ent.Comp.InitTime = _timing.CurTime;

        _overlay.Register(ent);
    }

    private void OnShockWaveRemoved(Entity<ShockWaveComponent> ent, ref ComponentRemove args)
    {
        _registered.Remove(ent.Owner);
    }
}
