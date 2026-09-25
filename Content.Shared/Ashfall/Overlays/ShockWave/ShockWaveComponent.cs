using Robust.Shared.GameStates;

namespace Content.Shared.Ashfall.Overlays.ShockWave;

/// <summary>
/// Displays a propagating refractive screen shockwave ring.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShockWaveComponent : Component
{
    [DataField, AutoNetworkedField]
    public float WaveSpeed = 15.3f;

    [DataField, AutoNetworkedField]
    public float WaveStrength = 1.0f;

    [DataField, AutoNetworkedField]
    public float DownScale = 1.5f;

    [DataField, AutoNetworkedField]
    public float FadeTime = 1.5f;

    [DataField, AutoNetworkedField]
    public TimeSpan InitTime;
}
