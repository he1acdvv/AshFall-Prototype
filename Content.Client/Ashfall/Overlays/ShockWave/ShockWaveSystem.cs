using Content.Client.Explosion;
using Content.Shared.Ashfall.Overlays.ShockWave;
using Content.Shared.Explosion;
using Content.Shared.Explosion.Components;
using Robust.Client.Graphics;
using Robust.Shared.Map;
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

        if (_overlayMan.HasOverlay<ShockWaveOverlay>())
            _overlayMan.RemoveOverlay<ShockWaveOverlay>();
        _overlay = new ShockWaveOverlay();
        _overlayMan.AddOverlay(_overlay);

        SubscribeLocalEvent<ShockWaveComponent, ComponentStartup>(OnShockWaveStartup);
        SubscribeLocalEvent<ShockWaveComponent, ComponentRemove>(OnShockWaveRemoved);
        SubscribeLocalEvent<ExplosionVisualsComponent, ExplosionVisualsStateAppliedEvent>(OnExplosionStateApplied);
        SubscribeLocalEvent<ExplosionVisualsComponent, ComponentShutdown>(OnExplosionVisualsShutdown);
        SubscribeLocalEvent<ExplosionVisualsTexturesComponent, ComponentStartup>(OnExplosionTexturesStartup);
        SubscribeLocalEvent<ExplosionVisualsTexturesComponent, ComponentShutdown>(OnExplosionTexturesShutdown);
    }

    public override void Shutdown()
    {
        _overlay.Clear();
        _overlayMan.RemoveOverlay<ShockWaveOverlay>();
        _registered.Clear();
        _spawnedForExplosion.Clear();
        base.Shutdown();
    }

    private void TrySpawnShockWave(EntityUid uid, ExplosionVisualsComponent visuals)
    {
        if (visuals.Epicenter != MapCoordinates.Nullspace && _spawnedForExplosion.Add(uid))
        {
            Spawn("AshfallEffectShockWave", visuals.Epicenter);
        }
    }

    private void OnExplosionStateApplied(EntityUid uid, ExplosionVisualsComponent comp, ref ExplosionVisualsStateAppliedEvent args)
    {
        TrySpawnShockWave(uid, comp);
    }

    private void OnExplosionTexturesStartup(EntityUid uid, ExplosionVisualsTexturesComponent comp, ComponentStartup args)
    {
        if (TryComp<ExplosionVisualsComponent>(uid, out var visuals))
            TrySpawnShockWave(uid, visuals);
    }

    private void OnExplosionVisualsShutdown(EntityUid uid, ExplosionVisualsComponent comp, ComponentShutdown args)
    {
        _spawnedForExplosion.Remove(uid);
    }

    private void OnExplosionTexturesShutdown(EntityUid uid, ExplosionVisualsTexturesComponent comp, ComponentShutdown args)
    {
        _spawnedForExplosion.Remove(uid);
    }

    private void OnShockWaveStartup(Entity<ShockWaveComponent> ent, ref ComponentStartup args)
    {
        if (!_registered.Add(ent.Owner))
            return;

        if (ent.Comp.InitTime == TimeSpan.Zero)
            ent.Comp.InitTime = _timing.RealTime;

        _overlay.Register(ent);
    }

    private void OnShockWaveRemoved(Entity<ShockWaveComponent> ent, ref ComponentRemove args)
    {
        _registered.Remove(ent.Owner);
    }
}
