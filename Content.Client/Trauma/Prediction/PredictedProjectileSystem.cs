// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.GameTicking;
using Content.Shared.Projectiles;
using Robust.Client.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.Client.Trauma.Prediction;

/// <summary>
/// Marks projectiles as participating in client-side physics prediction.
/// Only marks projectiles fired by the local player (via ShotPredictedProjectileEvent).
/// </summary>
public sealed partial class PredictedProjectileSystem : EntitySystem
{
    [Dependency] private SharedPhysicsSystem _physics = default!;

    private readonly HashSet<EntityUid> _predictedProjectiles = new();
    private readonly HashSet<NetEntity> _pendingNetEntities = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ProjectileComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ProjectileComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ProjectileComponent, UpdateIsPredictedEvent>(OnUpdateIsPredicted);
        SubscribeNetworkEvent<ShotPredictedProjectileEvent>(OnShotPredictedProjectile);
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnStartup(Entity<ProjectileComponent> ent, ref ComponentStartup args)
    {
        var netEnt = GetNetEntity(ent.Owner);
        if (_pendingNetEntities.Remove(netEnt))
        {
            _predictedProjectiles.Add(ent.Owner);
            _physics.UpdateIsPredicted(ent.Owner);
        }
    }

    private void OnShutdown(Entity<ProjectileComponent> ent, ref ComponentShutdown args)
    {
        _predictedProjectiles.Remove(ent.Owner);
        if (TryGetNetEntity(ent.Owner, out var netEnt))
            _pendingNetEntities.Remove(netEnt.Value);
    }

    private void OnUpdateIsPredicted(Entity<ProjectileComponent> ent, ref UpdateIsPredictedEvent args)
    {
        if (_predictedProjectiles.Contains(ent.Owner))
            args.IsPredicted = true;
    }

    private void OnShotPredictedProjectile(ShotPredictedProjectileEvent args)
    {
        if (!TryGetEntity(args.Projectile, out var uid) || !Exists(uid))
        {
            _pendingNetEntities.Add(args.Projectile);
            return;
        }

        _predictedProjectiles.Add(uid.Value);
        _physics.UpdateIsPredicted(uid.Value);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _predictedProjectiles.Clear();
        _pendingNetEntities.Clear();
    }
}
