// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Ashfall.Combat.Concussion;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client.Ashfall.Combat.Concussion;

public sealed partial class ConcussionOverlaySystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private IPlayerManager _player = default!;

    private ConcussionOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new ConcussionOverlay();

        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnPlayerDetached);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayManager.RemoveOverlay(_overlay);
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
        _overlay.ConcussionIntensity = 0f;
        if (_overlayManager.HasOverlay<ConcussionOverlay>())
            _overlayManager.RemoveOverlay(_overlay);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_player.LocalEntity is not { } local || !TryComp<ConcussionThresholdComponent>(local, out var comp))
        {
            if (_overlay.ConcussionIntensity > 0.001f)
            {
                _overlay.ConcussionIntensity = MathF.Max(0f, _overlay.ConcussionIntensity - frameTime * 0.8f);
            }
            else
            {
                _overlay.ConcussionIntensity = 0f;
            }

            if (_overlay.ConcussionIntensity <= 0.005f && _overlayManager.HasOverlay<ConcussionOverlay>())
                _overlayManager.RemoveOverlay(_overlay);

            return;
        }

        var targetIntensity = comp.CurrentState switch
        {
            ConcussionState.Severe => Math.Clamp(comp.StoredDamage.Float() / 120f, 0.75f, 1f),
            ConcussionState.Moderate => Math.Clamp(comp.StoredDamage.Float() / 100f, 0.45f, 0.70f),
            ConcussionState.Minor => Math.Clamp(comp.StoredDamage.Float() / 50f, 0.20f, 0.40f),
            _ => 0f
        };

        var lerpSpeed = MathF.Min(1f, 6.0f * frameTime);
        _overlay.ConcussionIntensity = MathHelper.Lerp(_overlay.ConcussionIntensity, targetIntensity, lerpSpeed);

        if (_overlay.ConcussionIntensity > 0.005f)
        {
            if (!_overlayManager.HasOverlay<ConcussionOverlay>())
                _overlayManager.AddOverlay(_overlay);
        }
        else
        {
            if (_overlayManager.HasOverlay<ConcussionOverlay>())
            {
                _overlay.ConcussionIntensity = 0f;
                _overlayManager.RemoveOverlay(_overlay);
            }
        }
    }
}
