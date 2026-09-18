namespace Content.Shared.Projectiles;

[ByRefEvent]
public readonly record struct PlayerShotProjectileEvent(EntityUid Projectile, EntityUid User);
