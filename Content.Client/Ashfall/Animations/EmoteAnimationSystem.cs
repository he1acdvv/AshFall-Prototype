using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Shared.Ashfall.Animations;
using Content.Shared.Mobs;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;

using Content.Shared.Mobs.Components;

namespace Content.Client.Ashfall.Animations;

public sealed partial class EmoteAnimationSystem : SharedEmoteAnimationSystem
{
    [Dependency] private AnimationPlayerSystem _animationPlayer = default!;

    private const string EmoteAnimKey = "AshfallEmoteAnimation";
    private readonly Dictionary<EntityUid, (Vector2 Offset, Angle Rotation)> _savedTransforms = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EmoteAnimationComponent, AfterAutoHandleStateEvent>(OnHandleState);
        SubscribeLocalEvent<EmoteAnimationComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<EmoteAnimationComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<EmoteAnimationComponent, AnimationCompletedEvent>(OnAnimationCompleted);
    }

    private bool TryStartAnimation(EntityUid uid, [NotNullWhen(true)] out SpriteComponent? sprite)
    {
        if (_animationPlayer.HasRunningAnimation(uid, EmoteAnimKey) || !TryComp(uid, out sprite))
        {
            sprite = null;
            return false;
        }

        _savedTransforms[uid] = (sprite.Offset, sprite.Rotation);
        return true;
    }

    private void StopRunningAnimation(EntityUid uid)
    {
        if (_animationPlayer.HasRunningAnimation(uid, EmoteAnimKey))
        {
            _animationPlayer.Stop(uid, EmoteAnimKey);
            if (_savedTransforms.Remove(uid, out var saved) && TryComp<SpriteComponent>(uid, out var sprite))
            {
                sprite.Offset = saved.Offset;
                sprite.Rotation = saved.Rotation;
            }
        }
        else
        {
            _savedTransforms.Remove(uid);
        }
    }

    private void OnAnimationCompleted(EntityUid uid, EmoteAnimationComponent comp, AnimationCompletedEvent args)
    {
        if (args.Key != EmoteAnimKey)
            return;

        if (_savedTransforms.Remove(uid, out var saved) && TryComp<SpriteComponent>(uid, out var sprite))
        {
            sprite.Offset = saved.Offset;
            sprite.Rotation = saved.Rotation;
        }
    }

    private void OnMobStateChanged(Entity<EmoteAnimationComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Alive)
        {
            StopRunningAnimation(ent.Owner);
            PlayEmoteTail(ent.Owner, false);
        }
    }

    private void OnShutdown(Entity<EmoteAnimationComponent> ent, ref ComponentShutdown args)
    {
        StopRunningAnimation(ent.Owner);
        PlayEmoteTail(ent.Owner, false);
    }

    private void OnHandleState(Entity<EmoteAnimationComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (ent.Comp.CurAnimationIndex == ent.Comp.LastClientAnimationIndex)
            return;

        ent.Comp.LastClientAnimationIndex = ent.Comp.CurAnimationIndex;

        if (TryComp<MobStateComponent>(ent.Owner, out var mobState) && mobState.CurrentState != MobState.Alive)
            return;

        switch (ent.Comp.AnimationId)
        {
            case AnimationFlip:
                PlayEmoteFlip(ent.Owner);
                break;
            case AnimationJump:
                PlayEmoteJump(ent.Owner);
                break;
            case AnimationTurn:
            case AnimationSpin:
                PlayEmoteTurn(ent.Owner);
                break;
            case AnimationTremble:
            case AnimationShiver:
                PlayEmoteTremble(ent.Owner);
                break;
            case AnimationTailWag:
                PlayEmoteTail(ent.Owner, true);
                break;
            case AnimationTailStop:
                PlayEmoteTail(ent.Owner, false);
                break;
        }
    }

    public void PlayEmoteFlip(EntityUid uid)
    {
        if (!TryStartAnimation(uid, out var sprite))
            return;

        var baseAngle = sprite.Rotation;

        var anim = new Animation
        {
            Length = TimeSpan.FromMilliseconds(500),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Rotation),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(baseAngle.Degrees), 0f),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(baseAngle.Degrees + 180), 0.25f),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(baseAngle.Degrees + 360), 0.5f),
                    }
                }
            }
        };

        _animationPlayer.Play(uid, anim, EmoteAnimKey);
    }

    public void PlayEmoteJump(EntityUid uid)
    {
        if (!TryStartAnimation(uid, out var sprite))
            return;

        var baseOffset = sprite.Offset;

        var anim = new Animation
        {
            Length = TimeSpan.FromMilliseconds(250),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    InterpolationMode = AnimationInterpolationMode.Cubic,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(baseOffset, 0f),
                        new AnimationTrackProperty.KeyFrame(baseOffset + new Vector2(0, 0.65f), 0.125f),
                        new AnimationTrackProperty.KeyFrame(baseOffset, 0.25f),
                    }
                }
            }
        };

        _animationPlayer.Play(uid, anim, EmoteAnimKey);
    }

    public void PlayEmoteTurn(EntityUid uid)
    {
        if (!TryStartAnimation(uid, out var sprite))
            return;

        var baseAngle = sprite.Rotation;

        var anim = new Animation
        {
            Length = TimeSpan.FromMilliseconds(600),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Rotation),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(baseAngle.Degrees), 0f),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(baseAngle.Degrees + 180), 0.3f),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(baseAngle.Degrees + 360), 0.6f),
                    }
                }
            }
        };

        _animationPlayer.Play(uid, anim, EmoteAnimKey);
    }

    public void PlayEmoteTremble(EntityUid uid)
    {
        if (!TryStartAnimation(uid, out var sprite))
            return;

        var baseOffset = sprite.Offset;

        var anim = new Animation
        {
            Length = TimeSpan.FromMilliseconds(400),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(baseOffset, 0f),
                        new AnimationTrackProperty.KeyFrame(baseOffset + new Vector2(-0.06f, 0), 0.05f),
                        new AnimationTrackProperty.KeyFrame(baseOffset + new Vector2(0.06f, 0), 0.10f),
                        new AnimationTrackProperty.KeyFrame(baseOffset + new Vector2(-0.05f, 0), 0.15f),
                        new AnimationTrackProperty.KeyFrame(baseOffset + new Vector2(0.05f, 0), 0.20f),
                        new AnimationTrackProperty.KeyFrame(baseOffset + new Vector2(-0.03f, 0), 0.25f),
                        new AnimationTrackProperty.KeyFrame(baseOffset + new Vector2(0.03f, 0), 0.30f),
                        new AnimationTrackProperty.KeyFrame(baseOffset, 0.40f),
                    }
                }
            }
        };

        _animationPlayer.Play(uid, anim, EmoteAnimKey);
    }

    public void PlayEmoteTail(EntityUid uid, bool start)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        foreach (var layer in sprite.AllLayers)
        {
            if (layer.RsiState.Name != null && layer.RsiState.Name.Contains("tail", StringComparison.OrdinalIgnoreCase))
            {
                layer.AutoAnimated = start;
                if (!start)
                    layer.AnimationTime = 0;
            }
        }
    }
}
