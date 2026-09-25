using Content.Server.Chat.Systems;
using Content.Shared.Ashfall.Animations;
using Content.Shared.Chat;

namespace Content.Server.Ashfall.Animations;

public sealed partial class EmoteAnimationSystem : SharedEmoteAnimationSystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EmoteAnimationComponent, EmoteEvent>(OnEmote);
    }

    private void OnEmote(EntityUid uid, EmoteAnimationComponent component, ref EmoteEvent args)
    {
        if (args.Handled)
            return;

        var emoteId = args.Emote.ID;
        if (emoteId.Equals("Flip", StringComparison.OrdinalIgnoreCase))
            PlayAnimation(uid, AnimationFlip, component);
        else if (emoteId.Equals("Jump", StringComparison.OrdinalIgnoreCase))
            PlayAnimation(uid, AnimationJump, component);
        else if (emoteId.Equals("Turn", StringComparison.OrdinalIgnoreCase) || emoteId.Equals("Spin", StringComparison.OrdinalIgnoreCase))
            PlayAnimation(uid, AnimationTurn, component);
        else if (emoteId.Equals("Tremble", StringComparison.OrdinalIgnoreCase) || emoteId.Equals("Shiver", StringComparison.OrdinalIgnoreCase) || emoteId.Equals("Shudder", StringComparison.OrdinalIgnoreCase))
            PlayAnimation(uid, AnimationTremble, component);
        else if (emoteId.Equals("TailWag", StringComparison.OrdinalIgnoreCase) || emoteId.Equals("Wag", StringComparison.OrdinalIgnoreCase))
            PlayAnimation(uid, AnimationTailWag, component);
        else if (emoteId.Equals("TailStop", StringComparison.OrdinalIgnoreCase))
            PlayAnimation(uid, AnimationTailStop, component);
    }

    public void PlayAnimation(EntityUid uid, string animationId, EmoteAnimationComponent? comp = null)
    {
        if (!Resolve(uid, ref comp, false))
            return;

        comp.AnimationId = animationId;
        comp.CurAnimationIndex++;
        Dirty(uid, comp);
    }
}
