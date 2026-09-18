// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Clothing;
using Content.Shared.EntityConditions;
using Content.Shared.Examine;
using Content.Trauma.Shared.Knowledge.Components;
using Content.Trauma.Shared.MartialArts.Components;
using Robust.Shared.Prototypes;

namespace Content.Trauma.Shared.Knowledge.Systems;

public abstract partial class SharedKnowledgeSystem
{
    [Dependency] private SharedEntityConditionsSystem _conditions = default!;

    private void InitializeOnWear()
    {
        SubscribeLocalEvent<KnowledgeGrantOnWearComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<KnowledgeGrantOnWearComponent, ClothingGotEquippedEvent>(OnGrantKnowledgeWear);
        SubscribeLocalEvent<KnowledgeGrantOnWearComponent, ClothingGotUnequippedEvent>(OnRemoveKnowledgeWear);
    }

    private void OnExamined(Entity<KnowledgeGrantOnWearComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || !ent.Comp.Examinable)
            return;

        using (args.PushGroup(nameof(KnowledgeGrantOnWearComponent)))
        {
            args.PushMarkup("This offsets these skills when used:");
            foreach (var (skill, level) in ent.Comp.Skills)
            {
                var color = level < 0
                    ? "red"
                    : "green";
                var name = ProtoMan.Index(skill).Name;
                args.PushMarkup($"- [color={color}]{level}[/color] [bold]{name}[/bold]");
            }
        }
    }

    private void OnGrantKnowledgeWear(Entity<KnowledgeGrantOnWearComponent> ent, ref ClothingGotEquippedEvent args)
        => ApplyKnowledgeModifiers(args.Wearer, ent);

    private void OnRemoveKnowledgeWear(Entity<KnowledgeGrantOnWearComponent> ent, ref ClothingGotUnequippedEvent args)
        => RemoveKnowledgeModifiers(args.Wearer, ent);

    private void ApplyKnowledgeModifiers(EntityUid wearer, Entity<KnowledgeGrantOnWearComponent> ent)
    {
        ent.Comp.Applied = _conditions.TryConditions(wearer, ent.Comp.Conditions);
        DirtyField(ent, ent.Comp, nameof(KnowledgeGrantOnWearComponent.Applied));
        if (!ent.Comp.Applied || GetContainer(wearer) is not { } brain)
            return;

        // Handle Skills (Temporary Levels)
        foreach (var (id, level) in ent.Comp.Skills)
        {
            if (EnsureKnowledge(brain, id) is { } unit)
            {
                unit.Comp.TemporaryLevel += level;
                Dirty(unit);
            }
        }

        // Handle Blocks
        foreach (var id in ent.Comp.Blocked.Keys)
        {
            if (GetKnowledge(brain, id) is { } unit && TryComp<MartialArtsKnowledgeComponent>(unit, out var martial))
            {
                martial.TemporaryBlockedCounter++;
                martial.Blocked = true;
                Dirty(unit, martial);
            }
        }
    }

    private void RemoveKnowledgeModifiers(EntityUid wearer, Entity<KnowledgeGrantOnWearComponent> ent)
    {
        if (TerminatingOrDeleted(wearer) || !ent.Comp.Applied || GetContainer(wearer) is not { } brain)
            return;

        ent.Comp.Applied = false;
        DirtyField(ent, ent.Comp, nameof(KnowledgeGrantOnWearComponent.Applied));

        // Remove Skills
        foreach (var (id, level) in ent.Comp.Skills)
        {
            if (GetKnowledge(brain, id) is not { } unit)
                continue;

            unit.Comp.TemporaryLevel = Math.Max(0, unit.Comp.TemporaryLevel - level);

            // If they have no real levels and no more temp levels, clean up
            if (unit.Comp.NetLevel <= 0)
                RemoveKnowledge(brain, id);
            else
                Dirty(unit);
        }

        // Remove Blocks
        foreach (var id in ent.Comp.Blocked.Keys)
        {
            if (GetKnowledge(brain, id) is { } unit && TryComp<MartialArtsKnowledgeComponent>(unit, out var martial))
            {
                martial.Blocked = --martial.TemporaryBlockedCounter > 0;
                Dirty(unit, martial);
            }
        }
    }

    /// <summary>
    /// One-time adjustment to skills.
    /// Stores the new skills but also adds them to the current user.
    /// </summary>
    public void AddGrantedSkills(Entity<KnowledgeGrantOnWearComponent?> ent, EntityUid user, Dictionary<EntProtoId, int> skills)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        foreach (var (id, level) in skills)
        {
            ent.Comp.Skills[id] = ent.Comp.Skills.GetValueOrDefault(id) + level;
        }
        DirtyField(ent, ent.Comp, nameof(KnowledgeGrantOnWearComponent.Skills));

        // adjust immediately if it was applied already, if it wasn't applied it will be handled later
        if (!ent.Comp.Applied || GetContainer(user) is not { } brain)
            return;

        foreach (var (id, level) in skills)
        {
            if (EnsureKnowledge(brain, id) is { } unit)
            {
                unit.Comp.TemporaryLevel += level;
                Dirty(unit);
            }
        }
    }
}
