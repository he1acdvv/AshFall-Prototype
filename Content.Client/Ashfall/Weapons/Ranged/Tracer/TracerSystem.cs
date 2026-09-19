using System.Numerics;
using Content.Shared.Ashfall.Weapons.Ranged.Tracer;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client.Ashfall.Weapons.Ranged.Tracer;

public sealed partial class TracerSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private TracerOverlay _tracerOverlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _tracerOverlay = new TracerOverlay(this);
        _overlay.AddOverlay(_tracerOverlay);

        SubscribeLocalEvent<TracerComponent, ComponentStartup>(OnTracerStart);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay(_tracerOverlay);
    }

    private void OnTracerStart(Entity<TracerComponent> ent, ref ComponentStartup args)
    {
        var xform = Transform(ent);
        var pos = _transform.GetWorldPosition(xform);

        ent.Comp.Data = new TracerData(
            new List<Vector2> { pos },
            _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.Lifetime)
        );
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<TracerComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var tracer, out var xform))
        {
            var currentPos = _transform.GetWorldPosition(xform);

            if (tracer.Data == null)
            {
                tracer.Data = new TracerData(
                    new List<Vector2> { currentPos },
                    curTime + TimeSpan.FromSeconds(tracer.Lifetime)
                );
            }

            var data = tracer.Data.Value;
            if (curTime > data.EndTime)
            {
                RemCompDeferred<TracerComponent>(uid);
                continue;
            }

            data.PositionHistory.Add(currentPos);

            while (data.PositionHistory.Count > 2 &&
                   GetTrailLength(data.PositionHistory) > tracer.Length)
            {
                data.PositionHistory.RemoveAt(0);
            }

            if (data.PositionHistory.Count >= 2)
            {
                var trailLen = GetTrailLength(data.PositionHistory);
                if (trailLen > tracer.Length)
                {
                    var excess = trailLen - tracer.Length;
                    var seg = data.PositionHistory[1] - data.PositionHistory[0];
                    var segLen = seg.Length();
                    if (segLen > 0.0001f)
                    {
                        var t = MathF.Min(excess / segLen, 1f);
                        data.PositionHistory[0] = Vector2.Lerp(data.PositionHistory[0], data.PositionHistory[1], t);
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
        var query = EntityQueryEnumerator<TracerComponent, TransformComponent>();

        while (query.MoveNext(out _, out var tracer, out var xform))
        {
            if (xform.MapID != currentMap || tracer.Data == null)
                continue;

            var positions = tracer.Data.Value.PositionHistory;

            if (positions.Count < 2)
                continue;

            handle.SetTransform(Matrix3x2.Identity);

            for (var i = 1; i < positions.Count; i++)
            {
                handle.DrawLine(positions[i - 1], positions[i], tracer.Color);
            }
        }
    }
}
