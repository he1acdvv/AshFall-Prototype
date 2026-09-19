// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Inventory;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.Ashfall.Audio;

[Virtual]
public partial class SharedDeafnessSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

    public bool HasEarProtection(EntityUid uid)
    {
        if (TryComp<EarProtectionComponent>(uid, out var selfProt) && selfProt.Protection >= 0.8f)
            return true;

        if (_inventory.TryGetContainerSlotEnumerator(uid, out var slots))
        {
            while (slots.NextItem(out var item, out _))
            {
                if (TryComp<EarProtectionComponent>(item, out var prot) && prot.Protection >= 0.8f)
                    return true;
            }
        }

        return false;
    }

    public bool TryDeafen(EntityUid uid, TimeSpan duration, bool ignoreProtection = false, bool showPopup = true)
    {
        if (duration <= TimeSpan.Zero)
            return false;

        if (!ignoreProtection && HasEarProtection(uid))
            return false;

        var isNew = !HasComp<DeafenedComponent>(uid);
        var comp = EnsureComp<DeafenedComponent>(uid);
        var curTime = _timing.CurTime;
        var newEndTime = curTime + duration;

        if (newEndTime > comp.EndTime)
        {
            comp.EndTime = newEndTime;
            comp.TotalDuration = duration;
            Dirty(uid, comp);
        }

        if (isNew && showPopup && _net.IsServer)
        {
            _popup.PopupEntity(Loc.GetString("gun-deafened-ringing"), uid, uid, PopupType.MediumCaution);
        }

        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<DeafenedComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (curTime >= comp.EndTime)
            {
                RemCompDeferred<DeafenedComponent>(uid);
            }
        }
    }
}
