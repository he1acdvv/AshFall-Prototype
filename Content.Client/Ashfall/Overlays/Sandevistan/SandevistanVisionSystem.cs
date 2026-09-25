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

        if (_playerMan.LocalEntity is { Valid: true } player
            && HasComp<SandevistanVisionComponent>(player)
            && !_overlayMan.HasOverlay<SandevistanVisionOverlay>())
        {
            _overlayMan.AddOverlay(_overlay);
        }
    }

    public override void Shutdown()
    {
        if (_overlayMan.HasOverlay<SandevistanVisionOverlay>())
            _overlayMan.RemoveOverlay<SandevistanVisionOverlay>();
        base.Shutdown();
    }

    private void OnComponentStartup(Entity<SandevistanVisionComponent> ent, ref ComponentStartup args)
    {
        if (ent.Owner == _playerMan.LocalEntity)
        {
            if (_overlayMan.HasOverlay<SandevistanVisionOverlay>())
                _overlayMan.RemoveOverlay<SandevistanVisionOverlay>();
            _overlayMan.AddOverlay(_overlay);
        }
    }

    private void OnComponentShutdown(Entity<SandevistanVisionComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Owner == _playerMan.LocalEntity && _overlayMan.HasOverlay<SandevistanVisionOverlay>())
            _overlayMan.RemoveOverlay<SandevistanVisionOverlay>();
    }

    private void OnPlayerAttached(Entity<SandevistanVisionComponent> ent, ref LocalPlayerAttachedEvent args)
    {
        if (_overlayMan.HasOverlay<SandevistanVisionOverlay>())
            _overlayMan.RemoveOverlay<SandevistanVisionOverlay>();
        _overlayMan.AddOverlay(_overlay);
    }

    private void OnPlayerDetached(Entity<SandevistanVisionComponent> ent, ref LocalPlayerDetachedEvent args)
    {
        if (_overlayMan.HasOverlay<SandevistanVisionOverlay>())
            _overlayMan.RemoveOverlay<SandevistanVisionOverlay>();
    }
}
