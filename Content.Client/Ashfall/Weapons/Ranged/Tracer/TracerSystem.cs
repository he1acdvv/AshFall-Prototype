using System.Numerics;
using Content.Shared.Ashfall.Weapons.Ranged.Tracer;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Ashfall.Weapons.Ranged.Tracer;

public sealed partial class TracerSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private static readonly ProtoId<ShaderPrototype> UnshadedShaderId = "unshaded";

    private TracerOverlay _tracerOverlay = default!;
    private ShaderInstance _unshadedShader = default!;
    private readonly List<ActiveTracerTrail> _activeTrails = new();

    private const float TracerThickness = 0.08f;
    private const float TrailFadeDuration = 0.22f;

    public override void Initialize()
    {
        base.Initialize();
        _unshadedShader = _proto.Index(UnshadedShaderId).Instance();
        if (_overlay.HasOverlay<TracerOverlay>())
            _overlay.RemoveOverlay<TracerOverlay>();
        _tracerOverlay = new TracerOverlay(this);
        _overlay.AddOverlay(_tracerOverlay);

        SubscribeLocalEvent<TracerComponent, ComponentStartup>(OnTracerStart);
        SubscribeLocalEvent<TracerComponent, ComponentRemove>(OnTracerRemove);
    }

    public override void Shutdown()
    {
        _overlay.RemoveOverlay<TracerOverlay>();
        _activeTrails.Clear();
        base.Shutdown();
    }

    private void OnTracerStart(Entity<TracerComponent> ent, ref ComponentStartup args)
    {
        var xform = Transform(ent);
        var currentPos = _transform.GetWorldPosition(xform);
        var positions = new List<Vector2> { currentPos };

        ent.Comp.Data = new TracerData(
            positions,
            _timing.RealTime + TimeSpan.FromSeconds(ent.Comp.Lifetime)
        );

        _activeTrails.Add(new ActiveTracerTrail
        {
            Entity = ent.Owner,
            MapId = xform.MapID,
            Color = ent.Comp.Color,
            Length = ent.Comp.Length,
            Positions = positions,
            EndTime = _timing.RealTime + TimeSpan.FromSeconds(ent.Comp.Lifetime),
        });
    }

    private void OnTracerRemove(Entity<TracerComponent> ent, ref ComponentRemove args)
    {
        foreach (var trail in _activeTrails)
        {
            if (trail.Entity == ent.Owner)
            {
                trail.Entity = null;
                trail.FadeStartTime ??= _timing.RealTime;
            }
        }
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var curTime = _timing.RealTime;

        for (var i = _activeTrails.Count - 1; i >= 0; i--)
        {
            var trail = _activeTrails[i];

            if (trail.FadeStartTime is { } fadeStart)
            {
                if ((float)(curTime - fadeStart).TotalSeconds >= trail.FadeDuration)
                {
                    _activeTrails.RemoveAt(i);
                }
                continue;
            }

            if (trail.Entity == null || !Exists(trail.Entity) || Deleted(trail.Entity) || curTime > trail.EndTime)
            {
                trail.Entity = null;
                trail.FadeStartTime = curTime;
                continue;
            }

            if (!TryComp(trail.Entity.Value, out TransformComponent? xform))
            {
                trail.Entity = null;
                trail.FadeStartTime = curTime;
                continue;
            }

            trail.MapId = xform.MapID;
            var currentPos = _transform.GetWorldPosition(xform);

            if (trail.Positions.Count == 0 || Vector2.DistanceSquared(trail.Positions[^1], currentPos) > 0.0001f)
            {
                trail.Positions.Add(currentPos);
            }

            while (trail.Positions.Count > 2 && GetTrailLength(trail.Positions) > trail.Length)
            {
                trail.Positions.RemoveAt(0);
            }

            if (trail.Positions.Count >= 2)
            {
                var trailLen = GetTrailLength(trail.Positions);
                if (trailLen > trail.Length)
                {
                    var excess = trailLen - trail.Length;
                    var seg = trail.Positions[1] - trail.Positions[0];
                    var segLen = seg.Length();
                    if (segLen > 0.0001f)
                    {
                        var t = MathF.Min(excess / segLen, 1f);
                        trail.Positions[0] = Vector2.Lerp(trail.Positions[0], trail.Positions[1], t);
                    }
                }
            }
        }
    }

    private static float GetTrailLength(List<Vector2> positions)
    {
        var length = 0f;
        for (var i = 1; i < positions.Count; i++)
        {
            length += Vector2.Distance(positions[i - 1], positions[i]);
        }
        return length;
    }

    public void Draw(DrawingHandleWorld handle, MapId currentMap)
    {
        var curTime = _timing.RealTime;
        var hasDrawnAny = false;

        handle.SetTransform(Matrix3x2.Identity);

        foreach (var trail in _activeTrails)
        {
            if (trail.MapId != currentMap || trail.Positions.Count < 2)
                continue;

            var fade = 1.0f;
            if (trail.FadeStartTime is { } fadeStart)
            {
                fade = 1.0f - Math.Clamp((float)(curTime - fadeStart).TotalSeconds / trail.FadeDuration, 0f, 1f);
                if (fade <= 0.001f)
                    continue;
            }

            if (!hasDrawnAny)
            {
                handle.UseShader(_unshadedShader);
                hasDrawnAny = true;
            }

            var segCount = trail.Positions.Count - 1;
            for (var i = 1; i < trail.Positions.Count; i++)
            {
                var p0 = trail.Positions[i - 1];
                var p1 = trail.Positions[i];
                var diff = p1 - p0;
                var len = diff.Length();
                if (len < 0.001f)
                    continue;

                var progress = (float)i / segCount;
                var segAlpha = fade * MathHelper.Lerp(0.35f, 1.0f, progress);
                var color = trail.Color.WithAlpha(trail.Color.A * segAlpha);

                var rect = new Box2Rotated(
                    Box2.FromDimensions(-len / 2f, -TracerThickness / 2f, len, TracerThickness),
                    diff.ToAngle(),
                    (p0 + p1) / 2f);

                handle.DrawRect(rect, color);
            }
        }

        if (hasDrawnAny)
        {
            handle.UseShader(null);
        }
    }

    private sealed class ActiveTracerTrail
    {
        public EntityUid? Entity;
        public MapId MapId;
        public Color Color;
        public float Length;
        public List<Vector2> Positions = new();
        public TimeSpan EndTime;
        public TimeSpan? FadeStartTime;
        public float FadeDuration = TrailFadeDuration;
    }
}
