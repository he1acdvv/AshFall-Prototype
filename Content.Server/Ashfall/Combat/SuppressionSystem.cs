// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared.Ashfall.Combat;
using Content.Shared.Camera;
using Content.Shared.Jittering;
using Content.Shared.Movement.Systems;
using Content.Shared.Projectiles;
using Robust.Server.GameObjects;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Server.Ashfall.Combat;

public sealed partial class SuppressionSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private SharedJitteringSystem _jittering = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;

    private TimeSpan _nextCheck = TimeSpan.Zero;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(0.12f);
    private const float FlybyRadius = 2.2f;
    private const float ImpactRadius = 2.5f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PhysicsComponent, ProjectileHitEvent>(OnProjectileHit);
        SubscribeLocalEvent<SuppressionComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    private void OnProjectileHit(EntityUid uid, PhysicsComponent comp, ref ProjectileHitEvent args)
    {
        if (!TryComp<ProjectileComponent>(uid, out var proj))
            return;

        var pos = _transform.GetMapCoordinates(uid);
        if (pos.MapId == Robust.Shared.Map.MapId.Nullspace)
            return;

        foreach (var entity in _lookup.GetEntitiesInRange<SuppressionComponent>(pos, ImpactRadius))
        {
            if (entity.Owner == proj.Shooter)
                continue;

            AddSuppression(entity.Owner, entity.Comp, 0.25f);
        }
    }

    private void OnRefreshSpeed(EntityUid uid, SuppressionComponent comp, RefreshMovementSpeedModifiersEvent args)
    {
        if (comp.Level > 0.6f)
        {
            var mod = MathHelper.Lerp(1.0f, 0.75f, (comp.Level - 0.6f) / 0.4f);
            args.ModifySpeed(mod, mod);
        }
    }

    public void AddSuppression(EntityUid uid, float amount)
    {
        if (TryComp<SuppressionComponent>(uid, out var comp))
            AddSuppression(uid, comp, amount);
    }

    public void AddSuppression(EntityUid uid, SuppressionComponent comp, float amount)
    {
        var prevLevel = comp.Level;
        comp.Level = Math.Clamp(comp.Level + amount, 0f, 1f);

        if (comp.Level > 0.6f || prevLevel > 0.6f)
        {
            _movement.RefreshMovementSpeedModifiers(uid);
        }

        if (comp.Level >= 0.7f)
        {
            _jittering.DoJitter(uid, TimeSpan.FromSeconds(0.35f), false, 4f, 2f);
        }

        Dirty(uid, comp);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;

        // 1. Bullet flyby detection
        if (curTime >= _nextCheck)
        {
            _nextCheck = curTime + CheckInterval;

            var projQuery = EntityQueryEnumerator<ProjectileComponent, PhysicsComponent, TransformComponent>();
            while (projQuery.MoveNext(out var pUid, out var proj, out var phys, out var xform))
            {
                if (proj.ProjectileSpent || phys.LinearVelocity.LengthSquared() < 4.0f)
                    continue;

                var mapCoords = _transform.GetMapCoordinates((pUid, xform));
                if (mapCoords.MapId == Robust.Shared.Map.MapId.Nullspace)
                    continue;

                foreach (var entity in _lookup.GetEntitiesInRange<SuppressionComponent>(mapCoords, FlybyRadius))
                {
                    if (entity.Owner == proj.Shooter)
                        continue;

                    AddSuppression(entity.Owner, entity.Comp, 0.15f);
                }
            }
        }

        // 2. Decay suppression level
        var suppQuery = EntityQueryEnumerator<SuppressionComponent>();
        while (suppQuery.MoveNext(out var sUid, out var supp))
        {
            if (supp.Level <= 0f)
                continue;

            var prev = supp.Level;
            supp.Level = MathF.Max(0f, supp.Level - supp.DecayRate * frameTime);

            if (prev > 0.6f || supp.Level > 0.6f)
            {
                _movement.RefreshMovementSpeedModifiers(sUid);
            }

            Dirty(sUid, supp);
        }
    }
}
