using System.Numerics;
using Content.Shared.Ashfall.Overlays.Sandevistan;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Ashfall.Overlays.Sandevistan;

public sealed partial class SandevistanVisionOverlay : Overlay
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override bool RequestScreenTexture => true;
    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    private static readonly ProtoId<ShaderPrototype> ShaderProto = "AshfallSandevistanVision";
    private readonly ShaderInstance _shader;
    private readonly TransformSystem _transformSystem;

    private readonly List<(Vector2 Position, Angle Rotation, TimeSpan Time)> _afterImages = new();
    private Vector2 _lastPosition = Vector2.Zero;

    public SandevistanVisionOverlay()
    {
        IoCManager.InjectDependencies(this);
        ZIndex = 20;
        _shader = _prototypeManager.Index(ShaderProto).InstanceUnique();
        _transformSystem = _entityManager.System<TransformSystem>();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return false;

        if (_playerManager.LocalEntity is not { Valid: true } player
            || !_entityManager.TryGetComponent<SandevistanVisionComponent>(player, out var comp)
            || !comp.Enabled)
        {
            _afterImages.Clear();
            return false;
        }

        return base.BeforeDraw(in args);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null || args.Viewport.Eye == null)
            return;

        var player = _playerManager.LocalEntity;
        if (player == null || !_entityManager.TryGetComponent<SpriteComponent>(player.Value, out var playerSprite))
            return;

        var worldHandle = args.WorldHandle;
        var viewport = args.WorldBounds;
        var eye = args.Viewport.Eye;

        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        worldHandle.SetTransform(Matrix3x2.Identity);
        worldHandle.UseShader(_shader);
        worldHandle.DrawRect(viewport, Color.White);
        worldHandle.UseShader(null);

        var playerXform = _entityManager.GetComponent<TransformComponent>(player.Value);
        var playerPos = _transformSystem.GetWorldPosition(playerXform);
        var playerRot = _transformSystem.GetWorldRotation(playerXform);
        var curTime = _timing.CurTime;

        if (Vector2.Distance(playerPos, _lastPosition) > 0.35f)
        {
            _lastPosition = playerPos;
            _afterImages.Add((playerPos, playerRot, curTime));
        }

        _afterImages.RemoveAll(x => (curTime - x.Time).TotalSeconds > 0.30f);

        // Render after-image motion trail silhouettes
        foreach (var ghost in _afterImages)
        {
            var age = (float)(curTime - ghost.Time).TotalSeconds;
            var alpha = Math.Clamp(1f - age / 0.30f, 0f, 0.5f);
            var originalColor = playerSprite.Color;
            playerSprite.Color = new Color(0.2f, 0.9f, 0.85f, alpha * 0.40f);
            playerSprite.Render(worldHandle, eye.Rotation, ghost.Rotation, null, ghost.Position);
            playerSprite.Color = originalColor;
        }

        // Render player cleanly on top
        playerSprite.Render(worldHandle, eye.Rotation, playerRot, null, playerPos);
    }
}
