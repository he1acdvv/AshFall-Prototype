// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Ashfall.Combat;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client.Ashfall.Overlays;

public sealed partial class SuppressionOverlaySystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private IPlayerManager _player = default!;

    private SuppressionOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new SuppressionOverlay();

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
        _overlay.Intensity = 0f;
        if (_overlayManager.HasOverlay<SuppressionOverlay>())
            _overlayManager.RemoveOverlay(_overlay);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_player.LocalEntity is not { } local || !TryComp<SuppressionComponent>(local, out var supp))
        {
            if (_overlay.Intensity > 0.001f)
            {
                _overlay.Intensity = MathF.Max(0f, _overlay.Intensity - frameTime * 0.8f);
            }
            else
            {
                _overlay.Intensity = 0f;
            }

            if (_overlay.Intensity <= 0.005f && _overlayManager.HasOverlay<SuppressionOverlay>())
                _overlayManager.RemoveOverlay(_overlay);

            return;
        }

        var targetIntensity = Math.Clamp(supp.Level, 0f, 1f);
        var lerpSpeed = MathF.Min(1f, 8.0f * frameTime);
        _overlay.Intensity = MathHelper.Lerp(_overlay.Intensity, targetIntensity, lerpSpeed);

        if (_overlay.Intensity > 0.005f)
        {
            if (!_overlayManager.HasOverlay<SuppressionOverlay>())
                _overlayManager.AddOverlay(_overlay);
        }
        else
        {
            if (_overlayManager.HasOverlay<SuppressionOverlay>())
            {
                _overlay.Intensity = 0f;
                _overlayManager.RemoveOverlay(_overlay);
            }
        }
    }
}
