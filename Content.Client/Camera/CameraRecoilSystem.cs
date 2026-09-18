using System.Numerics;
using Content.Shared.Camera;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;

namespace Content.Client.Camera;

public sealed partial class CameraRecoilSystem : SharedCameraRecoilSystem
{
    [Dependency] private IConfigurationManager _configManager = default!;

    private float _intensity;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<CameraKickEvent>(OnCameraKick);

        Subs.CVar(_configManager, CCVars.ScreenShakeIntensity, OnCvarChanged, true);
    }

    private void OnCvarChanged(float value)
    {
        _intensity = value;
    }

    private void OnCameraKick(CameraKickEvent ev)
    {
        KickCamera(GetEntity(ev.NetEntity), ev.Recoil);
    }

    public override void KickCamera(EntityUid uid, Vector2 recoil, CameraRecoilComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return;

        if (_intensity == 0)
            return;

        recoil *= _intensity * 1.6f;

        const float maxKick = 2.5f;
        var existing = component.CurrentKick.Length();
        var dampen = existing / maxKick;
        component.CurrentKick += recoil * (1 - Math.Clamp(dampen, 0f, 0.8f));

        if (component.CurrentKick.Length() > maxKick)
            component.CurrentKick = component.CurrentKick.Normalized() * maxKick;

        component.LastKickTime = 0;
    }
}
