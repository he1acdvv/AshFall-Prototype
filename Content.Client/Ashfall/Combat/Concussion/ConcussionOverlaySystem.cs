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

        var dmg = comp.StoredDamage.Float();
        var targetIntensity = dmg <= 5f
            ? 0f
            : Math.Clamp((dmg - 5f) / 95f, 0f, 1f);

        var lerpSpeed = targetIntensity > _overlay.ConcussionIntensity
            ? MathF.Min(1f, 8.0f * frameTime)
            : MathF.Min(1f, 2.0f * frameTime);
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
