using Content.Server.Movement.Components;
using Content.Server.Movement.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;

namespace Content.Server.Projectiles;

/// <summary>
/// Compensates for shooter's lag by using flyby fixture to check for where the shooter saw the target at time of shooting.
/// Uses flyby fixture as for most entities this will overlap with the client's opinion of where the target "currently" is.
/// </summary>
public sealed partial class LagCompProjectileSystem : EntitySystem
{
    [Dependency] private LagCompensationSystem _lag = default!;
    [Dependency] private ProjectileSystem _projectile = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityQuery<ActorComponent> _actorQuery = default!;
    [Dependency] private EntityQuery<LagCompensationComponent> _lagQuery = default!;

    public float Range = 0.6f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerShotProjectileEvent>(OnShotProjectile);
        SubscribeLocalEvent<LagCompProjectileComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<LagCompProjectileComponent, EndCollideEvent>(OnEndCollide);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<LagCompProjectileComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (Deleted(uid))
                continue;

            if (comp.Targets.Count == 0 || comp.ShooterSession == null)
                continue;

            var pos = _transform.GetMapCoordinates(uid);
            foreach (var target in comp.Targets)
            {
                if (Deleted(target) || target == comp.Shooter)
                    continue;

                var lagPos = _transform.ToMapCoordinates(_lag.GetCoordinates(target, comp.ShooterSession));
                if (pos.InRange(lagPos, Range))
                {
                    _projectile.DoHit(uid, target);
                    RemCompDeferred(uid, comp);
                    break;
                }
            }
        }
    }

    private void OnShotProjectile(ref PlayerShotProjectileEvent args)
    {
        if (!_actorQuery.TryComp(args.User, out var actor))
            return;

        var session = actor.PlayerSession;
        var comp = EnsureComp<LagCompProjectileComponent>(args.Projectile);
        comp.ShooterSession = session;
        comp.Shooter = args.User;

        var ev = new ShotPredictedProjectileEvent
        {
            Projectile = GetNetEntity(args.Projectile)
        };
        RaiseNetworkEvent(ev, session);
    }

    private void OnStartCollide(Entity<LagCompProjectileComponent> ent, ref StartCollideEvent args)
    {
        if (args.OurEntity != ent.Owner || args.OurFixtureId != SharedFlyBySoundSystem.FlyByFixture)
            return;

        var target = args.OtherEntity;
        if (target == ent.Comp.Shooter)
            return;

        if (_lagQuery.HasComp(target))
            ent.Comp.Targets.Add(target);
    }

    private void OnEndCollide(Entity<LagCompProjectileComponent> ent, ref EndCollideEvent args)
    {
        if (args.OurFixtureId == SharedFlyBySoundSystem.FlyByFixture)
            ent.Comp.Targets.Remove(args.OtherEntity);
    }
}
