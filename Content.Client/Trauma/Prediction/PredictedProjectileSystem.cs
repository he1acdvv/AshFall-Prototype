// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Projectiles;
using Robust.Client.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.Client.Trauma.Prediction;

/// <summary>
/// Marks projectiles as participating in client-side physics prediction.
/// </summary>
public sealed partial class PredictedProjectileSystem : EntitySystem
{
    [Dependency] private SharedPhysicsSystem _physics = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ProjectileComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ProjectileComponent, UpdateIsPredictedEvent>(OnUpdateIsPredicted);
        SubscribeNetworkEvent<ShotPredictedProjectileEvent>(OnShotPredictedProjectile);
    }

    private void OnStartup(Entity<ProjectileComponent> ent, ref ComponentStartup args)
    {
        _physics.UpdateIsPredicted(ent.Owner);
    }

    private void OnUpdateIsPredicted(Entity<ProjectileComponent> ent, ref UpdateIsPredictedEvent args)
    {
        args.IsPredicted = true;
    }

    private void OnShotPredictedProjectile(ShotPredictedProjectileEvent args)
    {
        var uid = GetEntity(args.Projectile);
        if (!uid.IsValid())
            return;

        _physics.UpdateIsPredicted(uid);
    }
}
