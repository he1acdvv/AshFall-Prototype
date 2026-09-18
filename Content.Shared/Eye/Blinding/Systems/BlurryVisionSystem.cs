using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Inventory.Events;
using Content.Shared.Inventory;

using Robust.Shared.Timing;

namespace Content.Shared.Eye.Blinding.Systems;

public sealed partial class BlurryVisionSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VisionCorrectionComponent, GotEquippedEvent>(OnGlassesEquipped);
        SubscribeLocalEvent<VisionCorrectionComponent, GotUnequippedEvent>(OnGlassesUnequipped);
        SubscribeLocalEvent<VisionCorrectionComponent, InventoryRelayedEvent<GetBlurEvent>>(OnGetBlur);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<BlurryVisionComponent>();
        while (query.MoveNext(out var uid, out var blurry))
        {
            if (blurry.BlurEndTime != null && curTime >= blurry.BlurEndTime.Value)
            {
                blurry.BlurEndTime = null;
                UpdateBlurMagnitude(uid);
            }
        }
    }

    private void OnGetBlur(Entity<VisionCorrectionComponent> glasses, ref InventoryRelayedEvent<GetBlurEvent> args)
    {
        args.Args.Blur += glasses.Comp.VisionBonus;
        args.Args.CorrectionPower *= glasses.Comp.CorrectionPower;
    }

    /// <summary>
    /// Update a blurry vision component according to a blindable component.
    /// </summary>
    /// <param name="ent">The entity with the component to update.</param>
    public void UpdateBlurMagnitude(Entity<BlindableComponent?> ent)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return;

        var ev = new GetBlurEvent(ent.Comp.EyeDamage);
        RaiseLocalEvent(ent, ev);

        var blur = Math.Clamp(ev.Blur, 0, BlurryVisionComponent.MaxMagnitude);
        if (blur <= 0)
        {
            RemCompDeferred<BlurryVisionComponent>(ent);
            return;
        }

        var blurry = EnsureComp<BlurryVisionComponent>(ent);
        blurry.Magnitude = blur;
        blurry.CorrectionPower = ev.CorrectionPower;
        Dirty(ent, blurry);
    }

    /// <summary>
    /// Explicitly set the blur magnitude on an entity (e.g. from recoil disorientation or flash).
    /// </summary>
    public void SetBlurMagnitude(EntityUid uid, float magnitude, TimeSpan? duration = null)
    {
        var blurry = EnsureComp<BlurryVisionComponent>(uid);
        blurry.Magnitude = Math.Clamp(magnitude, 0, BlurryVisionComponent.MaxMagnitude);
        blurry.CorrectionPower = BlurryVisionComponent.DefaultCorrectionPower;
        if (duration != null)
        {
            var newEnd = _timing.CurTime + duration.Value;
            if (blurry.BlurEndTime == null || newEnd > blurry.BlurEndTime.Value)
                blurry.BlurEndTime = newEnd;
        }
        Dirty(uid, blurry);
    }

    private void OnGlassesEquipped(Entity<VisionCorrectionComponent> glasses, ref GotEquippedEvent args)
    {
        UpdateBlurMagnitude(args.EquipTarget);
    }

    private void OnGlassesUnequipped(Entity<VisionCorrectionComponent> glasses, ref GotUnequippedEvent args)
    {
        UpdateBlurMagnitude(args.EquipTarget);
    }
}

public sealed class GetBlurEvent : EntityEventArgs, IInventoryRelayEvent
{
    public readonly float BaseBlur;
    public float Blur;
    public float CorrectionPower = BlurryVisionComponent.DefaultCorrectionPower;

    public GetBlurEvent(float blur)
    {
        Blur = blur;
        BaseBlur = blur;
    }

    public SlotFlags TargetSlots => SlotFlags.HEAD | SlotFlags.MASK | SlotFlags.EYES;
}
