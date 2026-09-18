using System.Numerics;
using Content.Shared.GameTicking;
using Content.Shared.Ashfall.Audio;
using Content.Shared.Camera;
using Content.Shared.Flash;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Jittering;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Content.Trauma.Shared.Knowledge.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Ashfall.Weapons.Ranged;

public sealed partial class AshfallGunRecoilSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedWieldableSystem _wield = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private SharedJitteringSystem _jittering = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedDeafnessSystem _deafness = default!;
    [Dependency] private SharedKnowledgeSystem _knowledge = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private BlurryVisionSystem _blurryVision = default!;

    private readonly Dictionary<EntityUid, TimeSpan> _lastRecoilPopup = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GunComponent, GunShotEvent>(OnGunShot);
        SubscribeLocalEvent<EntityTerminatingEvent>(OnTerminating);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _lastRecoilPopup.Clear());
    }

    private void OnTerminating(ref EntityTerminatingEvent args)
    {
        _lastRecoilPopup.Remove(args.Entity);
    }

    private void OnGunShot(Entity<GunComponent> gun, ref GunShotEvent args)
    {
        var user = args.User;

        if (args.Ammo.Count == 0)
            return;

        HandleRecoilAndDrop(gun, user);
        HandleTinnitus(gun, user);
    }

    private void TryPopup(EntityUid user, string text, PopupType type)
    {
        var curTime = _timing.CurTime;
        if (_lastRecoilPopup.TryGetValue(user, out var last) && curTime - last < TimeSpan.FromSeconds(1.2f))
            return;

        _lastRecoilPopup[user] = curTime;
        _popup.PopupEntity(text, user, user, type);
    }

    private void HandleRecoilAndDrop(Entity<GunComponent> gun, EntityUid user)
    {
        var isTwoHanded = TryComp<WieldableComponent>(gun, out var wieldable);
        var isWielded = !isTwoHanded || (wieldable?.Wielded ?? false);

        var skillLevel = _knowledge.GetKnowledgeLevel(user, ShootingKnowledgeSystem.ShootingKnowledge);
        var isUnskilled = skillLevel <= 0;

        var isHeavyGun = isTwoHanded || gun.Comp.CameraRecoilScalar >= 1.5f;

        var gunRotation = _transform.GetWorldRotation(gun);
        var impulseDir = -gunRotation.ToWorldVec().Normalized();

        // Muzzle flash, blast blur with actual magnitude, and camera rumble on unskilled / heavy / unwielded shots
        if (isHeavyGun || !isWielded || isUnskilled)
        {
            _statusEffects.TryAddStatusEffectDuration(user, SharedFlashSystem.FlashedKey, TimeSpan.FromSeconds(0.18f));
            _statusEffects.TryAddStatusEffectDuration(user, "StatusEffectBlurryVision", TimeSpan.FromSeconds(0.8f));
            _blurryVision.SetBlurMagnitude(user, 3.5f, TimeSpan.FromSeconds(0.8f));
        }

        if (!isWielded || isUnskilled)
        {
            var staminaCost = isTwoHanded && !isWielded ? 18f : (isHeavyGun ? 14f : 10f);
            _stamina.TakeStaminaDamage(user, staminaCost);
        }

        // Case 1: Two-handed weapon fired single-handed (unwielded)
        // High chance (85-90%) to drop & throw weapon out of hand, blunt injury to shooter, stamina loss
        if (isTwoHanded && !isWielded)
        {
            var dropProb = isUnskilled ? 0.90f : 0.85f;
            if (_random.Prob(dropProb) && _hands.TryDrop(user, gun, checkActionBlocker: false))
            {
                var throwDir = (-gunRotation.ToWorldVec() + _random.NextVector2(0.25f)).Normalized();
                _throwing.TryThrow(gun, throwDir * 2.0f, baseThrowSpeed: 3.5f, user: user);
                TryPopup(user, Loc.GetString("ashfall-gun-recoil-dropped"), PopupType.LargeCaution);
            }
            else
            {
                TryPopup(user, Loc.GetString("ashfall-gun-recoil-push"), PopupType.MediumCaution);
            }

            // Blunt recoil self-damage to arm/body
            var bluntDmg = new DamageSpecifier();
            bluntDmg.DamageDict["Blunt"] = _random.NextFloat(6f, 10f);
            _damage.TryChangeDamage(user, bluntDmg, origin: gun);

            _throwing.TryThrow(user, impulseDir * 2.0f, baseThrowSpeed: 3.0f, doSpin: false, playSound: false);
            _jittering.DoJitter(user, TimeSpan.FromSeconds(0.6f), true, 16f, 6f);
            RaiseNetworkEvent(new CameraKickEvent(GetNetEntity(user), impulseDir * 2.5f), user);
            return;
        }

        // Case 2: Two-handed weapon fired wielded (with 2 hands) without skill
        // Weapon stays firmly in hands. Produces camera shake, jitter and stamina strain.
        // Only with a small chance (4%) can the grip slip into one hand (unwield).
        if (isTwoHanded && isWielded && isUnskilled)
        {
            if (wieldable != null && _random.Prob(0.04f))
            {
                _wield.TryUnwield((gun, wieldable), user, force: true);
                TryPopup(user, Loc.GetString("ashfall-gun-recoil-unwielded"), PopupType.MediumCaution);
                _jittering.DoJitter(user, TimeSpan.FromSeconds(0.4f), true, 10f, 4f);
                RaiseNetworkEvent(new CameraKickEvent(GetNetEntity(user), impulseDir * 1.8f), user);
                return;
            }

            _jittering.DoJitter(user, TimeSpan.FromSeconds(0.2f), true, 5f, 2f);
            TryPopup(user, Loc.GetString("ashfall-gun-recoil-push"), PopupType.SmallCaution);
            RaiseNetworkEvent(new CameraKickEvent(GetNetEntity(user), impulseDir * 1.2f), user);
            return;
        }

        // Case 3: Other heavy guns (e.g. heavy one-handed revolvers) fired without shooting skill
        if (isUnskilled && isHeavyGun && !isTwoHanded)
        {
            if (_random.Prob(0.12f) && _hands.TryDrop(user, gun, checkActionBlocker: false))
            {
                var throwDir = (-gunRotation.ToWorldVec() + _random.NextVector2(0.2f)).Normalized();
                _throwing.TryThrow(gun, throwDir * 1.5f, baseThrowSpeed: 2.5f, user: user);
                TryPopup(user, Loc.GetString("ashfall-gun-recoil-dropped"), PopupType.LargeCaution);
                _jittering.DoJitter(user, TimeSpan.FromSeconds(0.4f), true, 12f, 4f);
            }
            else
            {
                _jittering.DoJitter(user, TimeSpan.FromSeconds(0.25f), true, 6f, 2f);
                TryPopup(user, Loc.GetString("ashfall-gun-recoil-push"), PopupType.SmallCaution);
            }

            RaiseNetworkEvent(new CameraKickEvent(GetNetEntity(user), impulseDir * 1.2f), user);
        }
    }

    private void HandleTinnitus(Entity<GunComponent> gun, EntityUid user)
    {
        if (IsSuppressed(gun.Comp))
            return;

        // Deafens shooter without ear protection (popup suppressed to prevent overlapping with recoil popup)
        _deafness.TryDeafen(user, TimeSpan.FromSeconds(10.0f), showPopup: false);

        // Deafens nearby bystanders in 1.5m radius without ear protection (they receive caution popup)
        var userCoords = _transform.GetMapCoordinates(user);
        foreach (var entity in _lookup.GetEntitiesInRange(userCoords, 1.5f))
        {
            if (entity == user)
                continue;

            if (!HasComp<MobStateComponent>(entity))
                continue;

            _deafness.TryDeafen(entity, TimeSpan.FromSeconds(8.0f), showPopup: true);
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
