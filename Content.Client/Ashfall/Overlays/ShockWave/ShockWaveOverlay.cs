using System.Numerics;
using Content.Shared.Ashfall.Overlays.ShockWave;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Ashfall.Overlays.ShockWave;

public sealed partial class ShockWaveOverlay : Overlay
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IGameTiming _timing = default!;

    private SharedTransformSystem? _xformSystem;
    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;
    private readonly ShaderInstance _shader;
    private static readonly ProtoId<ShaderPrototype> ShaderProto = "ScreechShockWave";

    private readonly List<(EntityUid Entity, InnerShaderInstance Instance)> _cached = new();
    private int _currentCount;
    private const int MaximumInstances = 10;

    private readonly Vector2[] _positions;
    private readonly float[] _waveStrengths;
    private readonly float[] _waveSpeeds;
    private readonly float[] _downScales;
    private readonly float[] _fades;
    private readonly float[] _times;

    public ShockWaveOverlay()
    {
        IoCManager.InjectDependencies(this);
        ZIndex = 15;
        _shader = _prototypeManager.Index(ShaderProto).InstanceUnique();
        _positions = new Vector2[MaximumInstances];
        _waveStrengths = new float[MaximumInstances];
        _waveSpeeds = new float[MaximumInstances];
        _downScales = new float[MaximumInstances];
        _fades = new float[MaximumInstances];
        _times = new float[MaximumInstances];
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (args.Viewport.Eye == null)
            return false;

        if (_xformSystem is null && !_entMan.TrySystem(out _xformSystem))
            return false;

        _currentCount = 0;

        _cached.RemoveAll(entry => (float)(_timing.RealTime - entry.Instance.InitTime).TotalSeconds > entry.Instance.FadeTime || !_entMan.EntityExists(entry.Entity));

        foreach (var (entityUid, distortion) in _cached)
        {
            if (!_entMan.TryGetComponent<TransformComponent>(entityUid, out var xform))
                continue;

            if (xform.MapID != args.MapId)
                continue;

            var mapPos = _xformSystem.GetWorldPosition(xform);
            var tempCoords = args.Viewport.WorldToLocal(mapPos);

            tempCoords.Y = 1 - tempCoords.Y / args.Viewport.Size.Y;
            tempCoords.X /= args.Viewport.Size.X;

            var time = Math.Max(0f, (float)(_timing.RealTime - distortion.InitTime).TotalSeconds);
            var fade = 1f - Math.Clamp(time / distortion.FadeTime, 0f, 1f);

            var i = _currentCount;
            _positions[i] = tempCoords;
            _waveStrengths[i] = distortion.WaveStrength;
            _waveSpeeds[i] = distortion.WaveSpeed;
            _downScales[i] = distortion.DownScale;
            _fades[i] = fade;
            _times[i] = time;

            _currentCount += 1;
            if (_currentCount == MaximumInstances)
                break;
        }

        return _currentCount != 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null || args.Viewport.Eye == null)
            return;

        _shader.SetParameter("positions", _positions);
        _shader.SetParameter("waveSpeeds", _waveSpeeds);
        _shader.SetParameter("downScales", _downScales);
        _shader.SetParameter("waveStrengths", _waveStrengths);
        _shader.SetParameter("fades", _fades);
        _shader.SetParameter("times", _times);
        _shader.SetParameter("count", _currentCount);
        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("renderScale", args.Viewport.RenderScale * args.Viewport.Eye.Scale);

        var worldHandle = args.WorldHandle;
        worldHandle.SetTransform(Matrix3x2.Identity);
        worldHandle.UseShader(_shader);
        worldHandle.DrawRect(args.WorldBounds, Color.White);
        worldHandle.UseShader(null);
    }

    public void Register(Entity<ShockWaveComponent> ent)
    {
        _cached.Add((ent.Owner, new InnerShaderInstance
        {
            WaveSpeed = ent.Comp.WaveSpeed,
            WaveStrength = ent.Comp.WaveStrength,
            DownScale = ent.Comp.DownScale,
            FadeTime = ent.Comp.FadeTime,
            InitTime = ent.Comp.InitTime == TimeSpan.Zero ? _timing.RealTime : ent.Comp.InitTime
        }));
    }

    public void Clear()
    {
        _cached.Clear();
        _currentCount = 0;
    }

    private struct InnerShaderInstance
    {
        public float WaveSpeed;
        public float WaveStrength;
        public float DownScale;
        public float FadeTime;
        public TimeSpan InitTime;
    }
}
