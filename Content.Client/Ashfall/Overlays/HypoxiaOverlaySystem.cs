// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client.Ashfall.Overlays;

public sealed partial class HypoxiaOverlaySystem : EntitySystem
{
    [Dependency] private IPlayerManager _playerMan = default!;
    [Dependency] private IOverlayManager _overlayMan = default!;
    [Dependency] private DamageableSystem _damageable = default!;

    private HypoxiaOverlay _overlay = default!;
    private float _targetIntensity;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new HypoxiaOverlay
        {
            HypoxiaIntensity = 0f
        };

        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnPlayerDetached);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnPlayerAttached(LocalPlayerAttachedEvent args)
    {
        ResetIntensity();
    }

    private void OnPlayerDetached(LocalPlayerDetachedEvent args)
    {
        ResetIntensity();
    }

    private void ResetIntensity()
    {
        _targetIntensity = 0f;
        _overlay.HypoxiaIntensity = 0f;
        if (_overlayMan.HasOverlay<HypoxiaOverlay>())
            _overlayMan.RemoveOverlay(_overlay);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_playerMan.LocalEntity is not { } player)
        {
            if (_overlay.HypoxiaIntensity > 0f)
                ResetIntensity();
            return;
        }

        var calculatedIntensity = 0f;

        if (TryComp<DamageableComponent>(player, out var damageable))
        {
            var damage = _damageable.GetAllDamage((player, damageable));
            if (damage.DamageDict.TryGetValue("Asphyxiation", out var asphyxDamage) && asphyxDamage > 0)
            {
                // Full intensity reached at 80 asphyxiation damage
                calculatedIntensity = Math.Clamp(asphyxDamage.Float() / 80f, 0f, 1f);
            }
        }

        _targetIntensity = calculatedIntensity;

        // Smooth transition
        _overlay.HypoxiaIntensity = MathHelper.Lerp(_overlay.HypoxiaIntensity, _targetIntensity, Math.Clamp(frameTime * 4f, 0f, 1f));

        if (MathF.Abs(_overlay.HypoxiaIntensity - _targetIntensity) < 0.005f)
            _overlay.HypoxiaIntensity = _targetIntensity;

        if (_overlay.HypoxiaIntensity > 0.005f)
        {
            if (!_overlayMan.HasOverlay<HypoxiaOverlay>())
                _overlayMan.AddOverlay(_overlay);
        }
        else
        {
            if (_overlayMan.HasOverlay<HypoxiaOverlay>())
            {
                _overlay.HypoxiaIntensity = 0f;
                _overlayMan.RemoveOverlay(_overlay);
            }
        }
    }
}
