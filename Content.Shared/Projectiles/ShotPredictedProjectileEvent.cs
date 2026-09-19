using Robust.Shared.Serialization;

namespace Content.Shared.Projectiles;

[Serializable, NetSerializable]
public sealed class ShotPredictedProjectileEvent : EntityEventArgs
{
    public NetEntity Projectile;
}
