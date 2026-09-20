using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Client.Ashfall.Weapons.Ranged.Tracer;

public sealed class TracerOverlay : Overlay
{
    private readonly TracerSystem _tracer;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public TracerOverlay(TracerSystem tracer)
    {
        _tracer = tracer;
        ZIndex = 10;
        IoCManager.InjectDependencies(this);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        _tracer.Draw(args.WorldHandle, args.MapId);
    }
}
