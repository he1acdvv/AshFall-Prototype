using Robust.Shared.GameStates;

namespace Content.Shared.Ashfall.Animations;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class EmoteAnimationComponent : Component
{
    [DataField, AutoNetworkedField]
    public string AnimationId = string.Empty;

    [DataField, AutoNetworkedField]
    public uint CurAnimationIndex;

    [ViewVariables]
    public uint LastClientAnimationIndex;
}
