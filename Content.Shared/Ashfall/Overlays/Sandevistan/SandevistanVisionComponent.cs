using Robust.Shared.GameStates;

namespace Content.Shared.Ashfall.Overlays.Sandevistan;

/// <summary>
/// Attached to entities under adrenaline surge or Sandevistan reflex boost.
/// Causes the client to render chromatic distortion and fast-action motion trails.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SandevistanVisionComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled = true;
}
