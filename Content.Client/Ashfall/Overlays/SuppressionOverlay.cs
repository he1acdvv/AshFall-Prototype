// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client.Ashfall.Overlays;

public sealed partial class SuppressionOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> SuppressionShaderProto = "AshfallSuppression";

    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    private readonly ShaderInstance _shader;

    public float Intensity;

    public SuppressionOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _prototypeManager.Index(SuppressionShaderProto).InstanceUnique();
        ZIndex = 24;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (Intensity <= 0.005f)
            return false;

        if (!_entityManager.TryGetComponent(_playerManager.LocalEntity, out EyeComponent? eyeComp))
            return false;

        return args.Viewport.Eye == eyeComp.Eye;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("SuppressionIntensity", Intensity);

        var handle = args.WorldHandle;
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}
