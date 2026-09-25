// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Alert;
using Content.Shared.Armor;
using Content.Shared.Ashfall.Audio;
using Content.Shared.Ashfall.Combat.Concussion;
using Content.Shared.Explosion;
using Content.Shared.FixedPoint;
using Content.Shared.Flash;
using Content.Shared.Flash.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Rejuvenate;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Inventory;
using Content.Shared.Speech.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Ashfall.Combat.Concussion;

public sealed partial class ConcussionSystem : SharedConcussionSystem
{
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private AlertsSystem _alertsSystem = default!;
    [Dependency] private SharedDeafnessSystem _deafness = default!;
    [Dependency] private InventorySystem _inventory = default!;

    private static readonly ProtoId<AlertPrototype> ConcussionAlert = "Concussion";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ConcussionThresholdComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<ConcussionThresholdComponent, BeforeExplodeEvent>(OnBeforeExplode);
        SubscribeLocalEvent<ConcussionThresholdComponent, AfterFlashedEvent>(OnAfterFlashed);
        SubscribeLocalEvent<ConcussionThresholdComponent, ConcussionStateChangedEvent>(OnConcussionStateChanged);
        SubscribeLocalEvent<ConcussionThresholdComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ConcussionThresholdComponent, RejuvenateEvent>(OnRejuvenate);
        SubscribeLocalEvent<ConcussionThresholdComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);

        SubscribeLocalEvent<ConcussedComponent, ComponentInit>(OnConcussedInit);
        SubscribeLocalEvent<ConcussedComponent, ComponentShutdown>(OnConcussedShutdown);
    }

    private void OnDamageChanged(EntityUid uid, ConcussionThresholdComponent comp, DamageChangedEvent args)
    {
        if (args.DamageDelta == null)
            return;

        var helmetProtection = 1.0f;
        if (_inventory.TryGetSlotEntity(uid, "head", out var headItem) &&
            TryComp<ConcussionProtectionComponent>(headItem, out var concussionProtection))
        {
            helmetProtection = Math.Clamp(1f - concussionProtection.Protection, 0f, 1f);
        }

        // Heavy blunt trauma (batons, hammers, impacts) causes concussion shock
        if (args.DamageDelta.DamageDict.TryGetValue("Blunt", out var blunt) && blunt.Float() >= 15f)
        {
            AddConcussionDamage(uid, comp, FixedPoint2.New(blunt.Float() * 1.2f * helmetProtection));
        }

        // Heavy caliber piercing rounds deliver hydraulic/kinetic shock
        if (args.DamageDelta.DamageDict.TryGetValue("Piercing", out var piercing) && piercing.Float() >= 25f)
        {
            AddConcussionDamage(uid, comp, FixedPoint2.New(piercing.Float() * 0.8f * helmetProtection));
        }
    }

    private void OnMapInit(EntityUid uid, ConcussionThresholdComponent comp, MapInitEvent args)
    {
        comp.NextUpdate = _timing.CurTime + comp.UpdateInterval;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<ConcussionThresholdComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.StoredDamage <= FixedPoint2.Zero)
                continue;

            if (curTime < comp.NextUpdate)
                continue;

            var elapsed = (float)(curTime - (comp.NextUpdate - comp.UpdateInterval)).TotalSeconds;
            var healing = comp.HealRate * elapsed;
            comp.StoredDamage = FixedPoint2.Max(FixedPoint2.Zero, comp.StoredDamage - healing);

            UpdateConcussionState(uid, comp);
            Dirty(uid, comp);
            comp.NextUpdate = curTime + comp.UpdateInterval;
        }
    }

    private void OnConcussionStateChanged(EntityUid uid, ConcussionThresholdComponent comp, ConcussionStateChangedEvent args)
    {
        _movement.RefreshMovementSpeedModifiers(uid);

        if (args.NewState != ConcussionState.Sane)
        {
            EnsureComp<ConcussedComponent>(uid);
            EnsureComp<SlurredAccentComponent>(uid);
        }
        else
        {
            RemComp<ConcussedComponent>(uid);
            RemComp<SlurredAccentComponent>(uid);
        }
    }

    private void OnBeforeExplode(EntityUid uid, ConcussionThresholdComponent comp, ref BeforeExplodeEvent args)
    {
        var totalDmg = args.Damage.GetTotal().Float();
        if (totalDmg <= 0)
            return;

        var earProt = _deafness.GetEarProtection(uid);
        var acousticMultiplier = MathF.Max(0.0f, 1.0f - earProt);
        var concussionDmg = FixedPoint2.New(totalDmg * 1.5f * acousticMultiplier);

        if (concussionDmg > 0)
        {
            AddConcussionDamage(uid, comp, concussionDmg);
        }

        if (earProt < 0.8f)
        {
            var deafDuration = TimeSpan.FromSeconds(Math.Clamp(totalDmg * 0.25f, 15f, 25f));
            _deafness.TryDeafen(uid, deafDuration);
        }
    }

    private void OnAfterFlashed(EntityUid uid, ConcussionThresholdComponent comp, ref AfterFlashedEvent args)
    {
        if (args.Target != uid)
            return;

        var isFlashbang = args.Used is { } used && HasComp<Content.Shared.Trigger.Components.Effects.FlashOnTriggerComponent>(used);

        if (isFlashbang)
        {
            if (_deafness.HasEarProtection(uid))
                return;

            // Flashbang explosive detonation causes acute acoustic shock and disorientation
            AddConcussionDamage(uid, comp, FixedPoint2.New(35));
            _deafness.TryDeafen(uid, TimeSpan.FromSeconds(20));
        }
        else
        {
            // Optical flash causes minor disorientation without permanent acoustic deafness
            AddConcussionDamage(uid, comp, FixedPoint2.New(10));
        }
    }

    private void OnRefreshSpeed(EntityUid uid, ConcussionThresholdComponent comp, RefreshMovementSpeedModifiersEvent args)
    {
        if (!comp.SpeedModifierThresholds.TryGetValue(comp.CurrentState, out var speed))
            return;

        args.ModifySpeed(speed, speed);
    }

    private void OnRejuvenate(EntityUid uid, ConcussionThresholdComponent comp, RejuvenateEvent args)
    {
        comp.StoredDamage = FixedPoint2.Zero;
        UpdateConcussionState(uid, comp);
        Dirty(uid, comp);
    }

    private void OnConcussedInit(EntityUid uid, ConcussedComponent comp, ComponentInit args)
    {
        _alertsSystem.ShowAlert(uid, ConcussionAlert);
    }

    private void OnConcussedShutdown(EntityUid uid, ConcussedComponent comp, ComponentShutdown args)
    {
        _alertsSystem.ClearAlert(uid, ConcussionAlert);
    }
}
