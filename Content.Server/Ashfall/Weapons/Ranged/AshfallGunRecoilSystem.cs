using System.Numerics;
using Content.Shared.Ashfall.Audio;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Jittering;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable.Components;
using Content.Trauma.Shared.Knowledge.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;

namespace Content.Server.Ashfall.Weapons.Ranged;

public sealed partial class AshfallGunRecoilSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedJitteringSystem _jittering = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedDeafnessSystem _deafness = default!;
    [Dependency] private SharedKnowledgeSystem _knowledge = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GunComponent, GunShotEvent>(OnGunShot);
    }

    private void OnGunShot(Entity<GunComponent> gun, ref GunShotEvent args)
    {
        var user = args.User;

        if (args.Ammo.Count == 0)
            return;

        HandleRecoilAndDrop(gun, user);
        HandleTinnitus(gun, user);
    }

    private void HandleRecoilAndDrop(Entity<GunComponent> gun, EntityUid user)
    {
        var isTwoHanded = TryComp<WieldableComponent>(gun, out var wieldable);
        var isWielded = wieldable != null && wieldable.Wielded;

        var skillLevel = _knowledge.GetKnowledgeLevel(user, ShootingKnowledgeSystem.ShootingKnowledge);
        var isUnskilled = skillLevel <= 0;

        var isHeavyGun = isTwoHanded || gun.Comp.CameraRecoilScalar >= 1.5f;

        // Case 1: Two-handed weapon fired single-handed (unwielded) -> 100% weapon drop + heavy knockback
        if (isTwoHanded && !isWielded)
        {
            _hands.TryDrop(user, gun, checkActionBlocker: false);
            _popup.PopupEntity(Loc.GetString("ashfall-gun-recoil-dropped"), user, user, PopupType.LargeCaution);
            _jittering.DoJitter(user, TimeSpan.FromSeconds(0.6f), true, 16f, 6f);
            ApplyKnockback(gun, user, 45f);
            return;
        }

        // Case 2: Heavy gun fired two-handed without shooting skill -> strong knockback + 30% drop chance
        if (isWielded && isUnskilled && isHeavyGun)
        {
            ApplyKnockback(gun, user, 25f);

            if (_random.Prob(0.30f))
            {
                _hands.TryDrop(user, gun, checkActionBlocker: false);
                _popup.PopupEntity(Loc.GetString("ashfall-gun-recoil-dropped"), user, user, PopupType.LargeCaution);
                _jittering.DoJitter(user, TimeSpan.FromSeconds(0.5f), true, 12f, 5f);
            }
            else
            {
                _popup.PopupEntity(Loc.GetString("ashfall-gun-recoil-push"), user, user, PopupType.SmallCaution);
                _jittering.DoJitter(user, TimeSpan.FromSeconds(0.3f), true, 8f, 4f);
            }
            return;
        }

        // Case 3: Other guns fired without shooting skill -> light push
        if (isUnskilled && isHeavyGun)
        {
            ApplyKnockback(gun, user, 12f);
        }
    }

    private void ApplyKnockback(Entity<GunComponent> gun, EntityUid user, float strength)
    {
        if (!TryComp<PhysicsComponent>(user, out var userPhysics))
            return;

        var gunRotation = _transform.GetWorldRotation(gun);
        var impulseDir = -gunRotation.ToWorldVec().Normalized();
        _physics.ApplyLinearImpulse(user, impulseDir * strength, body: userPhysics);
    }

    private void HandleTinnitus(Entity<GunComponent> gun, EntityUid user)
    {
        if (IsSuppressed(gun.Comp))
            return;

        // Deafens shooter without ear protection
        _deafness.TryDeafen(user, TimeSpan.FromSeconds(3.5f));

        // Deafens nearby bystanders in 1.5m radius without ear protection
        var userCoords = _transform.GetMapCoordinates(user);
        foreach (var entity in _lookup.GetEntitiesInRange(userCoords, 1.5f))
        {
            if (entity == user)
                continue;

            if (!HasComp<MobStateComponent>(entity))
                continue;

            _deafness.TryDeafen(entity, TimeSpan.FromSeconds(2.0f));
        }
    }

    private bool IsSuppressed(GunComponent gun)
    {
        if (gun.SoundGunshot == null)
            return true;

        if (gun.SoundGunshot is SoundCollectionSpecifier col &&
            col.Collection != null &&
            col.Collection.Value.Id.Contains("Supressed", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (gun.SoundGunshot is SoundPathSpecifier path &&
            path.Path.ToString().Contains("silence", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
