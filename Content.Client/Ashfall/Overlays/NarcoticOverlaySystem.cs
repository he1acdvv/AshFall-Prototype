// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Drowsiness;
using Content.Shared.StatusEffectNew;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client.Ashfall.Overlays;

public sealed partial class NarcoticOverlaySystem : EntitySystem
{
    [Dependency] private IPlayerManager _playerMan = default!;
    [Dependency] private IOverlayManager _overlayMan = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private IGameTiming _timing = default!;

    private NarcoticOverlay _overlay = default!;
    private float _targetIntensity;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new NarcoticOverlay
        {
            NarcoticIntensity = 0f
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
        _overlay.NarcoticIntensity = 0f;
        if (_overlayMan.HasOverlay<NarcoticOverlay>())
            _overlayMan.RemoveOverlay(_overlay);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_playerMan.LocalEntity is not { } player)
        {
            if (_overlay.NarcoticIntensity > 0f)
                ResetIntensity();
            return;
        }

        var calculatedIntensity = 0f;

        if (_statusEffects.TryGetEffectsEndTimeWithComp<DrowsinessStatusEffectComponent>(player, out var endTime))
        {
            endTime ??= TimeSpan.MaxValue;
            var timeLeft = (float)(endTime - _timing.CurTime).Value.TotalSeconds;
            if (timeLeft > 0f)
            {
                calculatedIntensity = Math.Clamp(timeLeft / 30f, 0.2f, 1f);
            }
        }

        _targetIntensity = calculatedIntensity;

        // Smooth transition
        _overlay.NarcoticIntensity = MathHelper.Lerp(_overlay.NarcoticIntensity, _targetIntensity, Math.Clamp(frameTime * 3f, 0f, 1f));

        if (MathF.Abs(_overlay.NarcoticIntensity - _targetIntensity) < 0.005f)
            _overlay.NarcoticIntensity = _targetIntensity;

        if (_overlay.NarcoticIntensity > 0.005f)
        {
            if (!_overlayMan.HasOverlay<NarcoticOverlay>())
                _overlayMan.AddOverlay(_overlay);
        }
        else
        {
            if (_overlayMan.HasOverlay<NarcoticOverlay>())
            {
                _overlay.NarcoticIntensity = 0f;
                _overlayMan.RemoveOverlay(_overlay);
            }
        }
    }
}
