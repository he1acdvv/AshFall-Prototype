// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Overlays;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client.Ashfall.Overlays;

public sealed partial class BloodlossOverlaySystem : EntitySystem
{
    [Dependency] private IPlayerManager _playerMan = default!;
    [Dependency] private IOverlayManager _overlayMan = default!;
    [Dependency] private DamageableSystem _damageable = default!;

    private BloodlossOverlay _overlay = default!;
    private float _targetIntensity;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new BloodlossOverlay();

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
        _overlay.Intensity = 0f;
        if (_overlayMan.HasOverlay<BloodlossOverlay>())
            _overlayMan.RemoveOverlay(_overlay);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_playerMan.LocalEntity is not { } player)
        {
            if (_overlay.Intensity > 0f)
                ResetIntensity();
            return;
        }

        var calculatedIntensity = 0f;

        if (TryComp<BloodstreamComponent>(player, out var stream))
        {
            // BloodLevel is networked (1.0 = normal blood level).
            // Desaturation begins below 65% blood (0.65), hitting full grayscale at <= 20% (0.20)
            if (stream.BloodLevel < 0.65f)
            {
                calculatedIntensity = Math.Clamp((0.65f - stream.BloodLevel) / (0.65f - 0.20f), 0f, 1f);
            }
        }

        if (TryComp<DamageableComponent>(player, out var damageable))
        {
            if (damageable.Damage.DamageDict.TryGetValue("Bloodloss", out var bloodlossDamage) && bloodlossDamage > 0)
            {
                var dmgIntensity = Math.Clamp(bloodlossDamage.Float() / 80f, 0f, 1f);
                calculatedIntensity = MathF.Max(calculatedIntensity, dmgIntensity);
            }
        }

        _targetIntensity = calculatedIntensity;

        // Smooth transition
        _overlay.Intensity = MathHelper.Lerp(_overlay.Intensity, _targetIntensity, Math.Clamp(frameTime * 4f, 0f, 1f));

        if (MathF.Abs(_overlay.Intensity - _targetIntensity) < 0.005f)
            _overlay.Intensity = _targetIntensity;

        // Dynamic registration: only keep overlay registered when it has non-zero intensity.
        // This avoids costly full-screen framebuffer copies every frame when healthy.
        if (_overlay.Intensity > 0.005f)
        {
            if (!_overlayMan.HasOverlay<BloodlossOverlay>())
                _overlayMan.AddOverlay(_overlay);
        }
        else
        {
            if (_overlayMan.HasOverlay<BloodlossOverlay>())
            {
                _overlay.Intensity = 0f;
                _overlayMan.RemoveOverlay(_overlay);
            }
        }
    }
}
