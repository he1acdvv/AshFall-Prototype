// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared.Ashfall.Combat.Concussion;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ConcussionThresholdComponent : Component
{
    [DataField, AutoNetworkedField]
    public FixedPoint2 StoredDamage = FixedPoint2.Zero;

    [DataField]
    public FixedPoint2 AbsoluteCap = FixedPoint2.New(200);

    [DataField]
    public FixedPoint2 HealRate = FixedPoint2.New(5.0f);

    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan NextUpdate = TimeSpan.Zero;

    [DataField, AutoNetworkedField]
    public ConcussionState CurrentState = ConcussionState.Sane;

    [DataField]
    public Dictionary<FixedPoint2, ConcussionState> Thresholds = new()
    {
        { FixedPoint2.New(20), ConcussionState.Minor },
        { FixedPoint2.New(50), ConcussionState.Moderate },
        { FixedPoint2.New(100), ConcussionState.Severe }
    };

    [DataField]
    public Dictionary<ConcussionState, float> SpeedModifierThresholds = new()
    {
        { ConcussionState.Minor, 0.90f },
        { ConcussionState.Moderate, 0.75f },
        { ConcussionState.Severe, 0.55f }
    };
}
