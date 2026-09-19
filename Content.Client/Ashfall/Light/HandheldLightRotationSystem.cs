// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared.CombatMode;
using Content.Shared.Light.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Map;

namespace Content.Client.Ashfall.Light;

/// <summary>
/// Smoothly interpolates the world rotation of handheld flashlights and directional lights,
/// allowing the light cone to smoothly track character turns and mouse cursor aim.
/// </summary>
public sealed partial class HandheldLightRotationSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private EntityQuery<CombatModeComponent> _combatQuery = default!;

    private readonly Dictionary<EntityUid, Angle> _smoothedAngles = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PointLightComponent, ComponentShutdown>(OnLightShutdown);
    }

    private void OnLightShutdown(EntityUid uid, PointLightComponent component, ComponentShutdown args)
    {
        _smoothedAngles.Remove(uid);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<PointLightComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var light, out var xform))
        {
            if (!light.Enabled || light.LightMask == null)
            {
                _smoothedAngles.Remove(uid);
                continue;
            }

            // Only smooth directional handheld lights / flashlights
            if (!HasComp<HandheldLightComponent>(uid) && !HasComp<ItemTogglePointLightComponent>(uid))
                continue;

#pragma warning disable RA0002
            // Disable auto-rotation in Clyde so our smoothed light.Rotation is used directly as world angle
            light.MaskAutoRotate = false;

            var targetAngle = GetTargetAngle(uid, xform);

            if (!_smoothedAngles.TryGetValue(uid, out var currentAngle))
            {
                currentAngle = targetAngle;
            }
            else
            {
                var factor = MathF.Min(1f, 16.0f * frameTime);
                currentAngle = Angle.Lerp(currentAngle, targetAngle, factor).Reduced();
            }

            _smoothedAngles[uid] = currentAngle;
            light.Rotation = currentAngle;
#pragma warning restore RA0002
        }
    }

    private Angle GetTargetAngle(EntityUid uid, TransformComponent xform)
    {
        var localPlayer = _player.LocalEntity;
        var (worldPos, worldRot) = _transform.GetWorldPositionRotation(xform);

        // If held/equipped by local player
        if (localPlayer != null && (xform.ParentUid == localPlayer || _transform.ContainsEntity(localPlayer.Value, uid)))
        {
            if (_combatQuery.TryComp(localPlayer, out var combat) && combat.IsInCombatMode)
            {
                var mouseScreen = _input.MouseScreenPosition;
                var mouseWorld = _eye.PixelToMap(mouseScreen);
                var holderCoords = _transform.GetMapCoordinates(localPlayer.Value);
                if (mouseWorld.MapId != MapId.Nullspace && mouseWorld.MapId == holderCoords.MapId)
                {
                    var dir = mouseWorld.Position - holderCoords.Position;
                    if (dir.LengthSquared() > 0.01f)
                        return dir.ToWorldAngle();
                }
            }

            // If not in combat mode, follow player's facing direction
            return _transform.GetWorldRotation(localPlayer.Value);
        }

        // For other entities or lights on the ground, follow transform world rotation
        return worldRot;
    }
}
