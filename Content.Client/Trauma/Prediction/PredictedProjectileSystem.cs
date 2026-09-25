using Content.Shared.GameTicking;
using Content.Shared.Projectiles;
using Robust.Client.Physics;
using Robust.Client.Player;
using Robust.Shared.Physics.Systems;

namespace Content.Client.Trauma.Prediction;

/// <summary>
/// Marks projectiles as participating in client-side physics prediction.
/// Seamlessly predicts projectiles shot by the local player without race conditions.
/// </summary>
public sealed partial class PredictedProjectileSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;

    private readonly HashSet<EntityUid> _predictedProjectiles = new();
    private readonly HashSet<NetEntity> _pendingNetEntities = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ProjectileComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ProjectileComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<PlayerShotProjectileEvent>(OnPlayerShotProjectile);
        SubscribeLocalEvent<ProjectileComponent, AfterAutoHandleStateEvent>(OnHandleState);
        SubscribeLocalEvent<ProjectileComponent, UpdateIsPredictedEvent>(OnUpdateIsPredicted);
        SubscribeNetworkEvent<ShotPredictedProjectileEvent>(OnShotPredictedProjectile);
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void TryPredict(EntityUid uid, ProjectileComponent comp)
    {
        if (_predictedProjectiles.Contains(uid))
            return;

        var netEnt = GetNetEntity(uid);
        var isLocalShooter = comp.Shooter != null && comp.Shooter == _player.LocalEntity;
        var isPending = _pendingNetEntities.Remove(netEnt);

        if (isLocalShooter || isPending)
        {
            _predictedProjectiles.Add(uid);
            _physics.UpdateIsPredicted(uid);
        }
    }

    private void OnStartup(Entity<ProjectileComponent> ent, ref ComponentStartup args)
    {
        TryPredict(ent.Owner, ent.Comp);
    }

    private void OnPlayerShotProjectile(ref PlayerShotProjectileEvent args)
    {
        if (args.User == _player.LocalEntity && TryComp<ProjectileComponent>(args.Projectile, out var comp))
        {
            TryPredict(args.Projectile, comp);
        }
    }

    private void OnHandleState(Entity<ProjectileComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        TryPredict(ent.Owner, ent.Comp);
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
