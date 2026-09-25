using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Ashfall.Weapons.Ranged.Tracer;

/// <summary>
/// Added to projectiles to give them tracer effects.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TracerComponent : Component
{
    /// <summary>
    /// How long the tracer effect should remain active.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Lifetime = 5f;

    /// <summary>
    /// The maximum length of the tracer trail in meters.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Length = 2.5f;

    /// <summary>
    /// Color of the tracer line effect.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color Color = Color.FromHex("#FFE082");

    [ViewVariables]
    public TracerData? Data;
}

[Serializable, NetSerializable, DataRecord]
public partial struct TracerData(List<Vector2> positionHistory, TimeSpan endTime)
{
    public List<Vector2> PositionHistory = positionHistory;
    public TimeSpan EndTime = endTime;
}
