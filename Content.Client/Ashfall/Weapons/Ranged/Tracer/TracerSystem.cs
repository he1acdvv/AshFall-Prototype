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
        var pos = xform.Coordinates.Position;

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
            if (tracer.Data == null)
            {
                tracer.Data = new TracerData(
                    new List<Vector2> { xform.Coordinates.Position },
                    curTime + TimeSpan.FromSeconds(tracer.Lifetime)
                );
            }

            var data = tracer.Data.Value;
            if (curTime > data.EndTime)
            {
                RemCompDeferred<TracerComponent>(uid);
                continue;
            }

            var currentPos = xform.Coordinates.Position;
            data.PositionHistory.Add(currentPos);

            while (data.PositionHistory.Count > 2 &&
                   GetTrailLength(data.PositionHistory) > tracer.Length)
            {
                data.PositionHistory.RemoveAt(0);
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

            var parentPos = Vector2.Zero;
            var parentRot = Angle.Zero;

            if (xform.ParentUid.IsValid())
            {
                var parent = Transform(xform.ParentUid);
                parentPos = _transform.GetWorldPosition(parent);
                parentRot = _transform.GetWorldRotation(parent);
            }

            handle.SetTransform(Matrix3x2.Identity);

            for (var i = 1; i < positions.Count; i++)
            {
                var start = parentPos + parentRot.RotateVec(positions[i - 1]);
                var end = parentPos + parentRot.RotateVec(positions[i]);
                handle.DrawLine(start, end, tracer.Color);
            }
        }
    }
}
