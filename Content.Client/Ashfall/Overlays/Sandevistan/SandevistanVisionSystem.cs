using Content.Shared.Ashfall.Overlays.Sandevistan;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client.Ashfall.Overlays.Sandevistan;

public sealed partial class SandevistanVisionSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayMan = default!;
    [Dependency] private IPlayerManager _playerMan = default!;

    private SandevistanVisionOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new SandevistanVisionOverlay();

        SubscribeLocalEvent<SandevistanVisionComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<SandevistanVisionComponent, ComponentShutdown>(OnComponentShutdown);
        SubscribeLocalEvent<SandevistanVisionComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<SandevistanVisionComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);
    }

    public override void Shutdown()
    {
        _overlayMan.RemoveOverlay(_overlay);
        base.Shutdown();
    }

    private void OnComponentStartup(Entity<SandevistanVisionComponent> ent, ref ComponentStartup args)
    {
        if (ent.Owner == _playerMan.LocalEntity)
            _overlayMan.AddOverlay(_overlay);
    }

    private void OnComponentShutdown(Entity<SandevistanVisionComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Owner == _playerMan.LocalEntity)
            _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnPlayerAttached(Entity<SandevistanVisionComponent> ent, ref LocalPlayerAttachedEvent args)
    {
        _overlayMan.AddOverlay(_overlay);
    }

    private void OnPlayerDetached(Entity<SandevistanVisionComponent> ent, ref LocalPlayerDetachedEvent args)
    {
        _overlayMan.RemoveOverlay(_overlay);
    }
}
