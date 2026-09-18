// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Bed.Sleep;
using Content.Shared.Body;
using Content.Shared.Cloning.Events;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Polymorph;
using Content.Shared.Random.Helpers;
using Content.Trauma.Common.CCVar;
using Content.Trauma.Common.Knowledge;
using Content.Trauma.Common.Knowledge.Components;
using Content.Trauma.Common.Knowledge.Prototypes;
using Content.Trauma.Common.Knowledge.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Trauma.Shared.Knowledge.Systems;

/// <summary>
/// This handles all knowledge related entities.
/// </summary>
public abstract partial class SharedKnowledgeSystem : CommonKnowledgeSystem
{
    [Dependency] protected IConfigurationManager _cfg = default!;
    [Dependency] protected IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] protected ISharedPlayerManager _player = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private EntityQuery<KnowledgeComponent> _query = default!;
    [Dependency] private EntityQuery<KnowledgeContainerComponent> _containerQuery = default!;
    [Dependency] private EntityQuery<KnowledgeHolderComponent> _holderQuery = default!;
    [Dependency] private EntityQuery<SleepingComponent> _sleepingQuery = default!;

    /// <summary>
    /// Every knowledge prototype and its data.
    /// </summary>
    public Dictionary<EntProtoId, KnowledgeComponent> AllKnowledges = new();

    public static readonly string[] MasteryNames = [
        "Unskilled",
        "Average",
        "Advanced",
        "Expert",
        "Master"
    ];

    public bool SkillsEnabled;
    private bool _skillGain;
    private TimeSpan _nextUpdate;
    private TimeSpan _updateDelay = TimeSpan.FromSeconds(1);
    private float _learnChance = 0.2f;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<KnowledgeContainerComponent, ComponentStartup>(OnContainerStartup);
        SubscribeLocalEvent<KnowledgeContainerComponent, ComponentShutdown>(OnContainerShutdown);

        SubscribeLocalEvent<KnowledgeContainerComponent, OrganGotInsertedEvent>(OnOrganInserted);
        SubscribeLocalEvent<KnowledgeContainerComponent, OrganGotRemovedEvent>(OnOrganRemoved);

        SubscribeLocalEvent<KnowledgeHolderComponent, CloningEvent>(OnCloned);
        SubscribeLocalEvent<KnowledgeHolderComponent, PolymorphedEvent>(OnPolymorphed);
        SubscribeLocalEvent<KnowledgeHolderComponent, MindAddedMessage>(OnMindAdded);

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);

        Subs.CVar(_cfg, TraumaCVars.SkillsEnabled, x => SkillsEnabled = x, true);
        Subs.CVar(_cfg, TraumaCVars.SkillGain, x => _skillGain = x, true);

        InitializeMartialArts();
        InitializeOnWear();

        LoadSkillPrototypes();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_skillGain || _timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + _updateDelay;

        if (_player.LocalEntity is {} player)
        {
            UpdateHolder(player);
            return;
        }

        var query = EntityQueryEnumerator<KnowledgeHolderComponent>();
        while (query.MoveNext(out var ent, out _))
        {
            UpdateHolder(ent);
        }
    }

    private void UpdateHolder(EntityUid ent)
    {
        if (TryGetAllKnowledgeUnits(ent) is not { } knowledgeUnits)
            return;

        foreach (var knowledgeUnit in knowledgeUnits)
        {
            if (RollForLevelUp(knowledgeUnit, ent))
                return;
        }
    }

    private void OnContainerStartup(Entity<KnowledgeContainerComponent> ent, ref ComponentStartup args)
    {
        EnsureContainer(ent);
    }

    private void OnContainerShutdown(Entity<KnowledgeContainerComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Container is { } container)
            _container.ShutdownContainer(container);
    }

    protected void LinkContainer(EntityUid target, Entity<KnowledgeContainerComponent> ent)
    {
        if (_timing.ApplyingState)
            return;

        var holder = EnsureComp<KnowledgeHolderComponent>(target);
        if (holder.KnowledgeEntity == ent.Owner)
            return;

        // If target was self-linked as a placeholder before organs were inserted, allow the brain organ to take over
        if (holder.KnowledgeEntity == target && ent.Owner != target)
        {
            if (_containerQuery.TryComp(target, out var targetContainerComp))
            {
                targetContainerComp.Holder = null;
                DirtyField(target, targetContainerComp, nameof(KnowledgeContainerComponent.Holder));
            }
            holder.KnowledgeEntity = null;
        }

        if (ent.Comp.Holder != null && ent.Comp.Holder != target)
        {
            Log.Warning($"Tried to link {ToPrettyString(target)} to {ToPrettyString(ent)} but it was already linked to holder {ToPrettyString(ent.Comp.Holder)}!");
            return;
        }

        if (holder.KnowledgeEntity != null && holder.KnowledgeEntity != ent.Owner)
        {
            Log.Warning($"Tried to link {ToPrettyString(target)} to {ToPrettyString(ent)} but target is already linked to container {ToPrettyString(holder.KnowledgeEntity)}!");
            return;
        }

        holder.KnowledgeEntity = ent;
        Dirty(target, holder);
        ent.Comp.Holder = target;
        DirtyField(ent, ent.Comp, nameof(KnowledgeContainerComponent.Holder));
    }

    private void UnlinkContainer(EntityUid target, Entity<KnowledgeContainerComponent> ent)
    {
        if (_timing.ApplyingState ||
            !_holderQuery.TryComp(target, out var holder))
            return;

        if (holder.KnowledgeEntity != ent.Owner && ent.Comp.Holder != target)
            return;

        if (holder.KnowledgeEntity == ent.Owner)
        {
            holder.KnowledgeEntity = null;
            Dirty(target, holder);
        }

        if (ent.Comp.Holder == target)
        {
            ent.Comp.Holder = null;
            DirtyField(ent, ent.Comp, nameof(KnowledgeContainerComponent.Holder));
        }
    }

    private void OnOrganInserted(Entity<KnowledgeContainerComponent> ent, ref OrganGotInsertedEvent args)
    {
        LinkContainer(args.Target, ent);
    }

    private void OnOrganRemoved(Entity<KnowledgeContainerComponent> ent, ref OrganGotRemovedEvent args)
    {
        UnlinkContainer(args.Target, ent);
    }

    private void OnCloned(Entity<KnowledgeHolderComponent> ent, ref CloningEvent args)
    {
        TransferKnowledge(ent.Owner, args.CloneUid);
    }

    private void OnPolymorphed(Entity<KnowledgeHolderComponent> ent, ref PolymorphedEvent args)
    {
        if (ent.Owner == args.OldEntity)
            TransferKnowledge(ent.Owner, args.NewEntity);
    }

    private void OnMindAdded(Entity<KnowledgeHolderComponent> ent, ref MindAddedMessage args)
    {
        EnsureKnowledgeContainer(ent);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<EntityPrototype>())
            LoadSkillPrototypes();
    }

    private void LoadSkillPrototypes()
    {
        AllKnowledges.Clear();
        var name = Factory.GetComponentName<KnowledgeComponent>();
        foreach (var proto in ProtoMan.EnumeratePrototypes<EntityPrototype>())
        {
            if (!proto.TryGetComponent<KnowledgeComponent>(name, out var comp))
                continue;

            AllKnowledges[proto.ID] = comp;
        }
    }

    public void TransferKnowledge(EntityUid ent, EntityUid otherHolder)
    {
        if (TryGetAllKnowledgeUnits(ent) is not { } found)
            return;

        var mobContainer = EnsureKnowledgeContainer(otherHolder);
        if (mobContainer.Comp.Container is not { } container)
            return;

        foreach (var knowledgeEnt in found)
        {
            _container.Insert(knowledgeEnt.Owner, container);
            var protoId = Prototype(knowledgeEnt)?.ID;
            if (protoId is { } id)
                mobContainer.Comp.KnowledgeDict[id] = knowledgeEnt.Owner;
        }
        ClearKnowledge(ent, false);
    }

    public void SkillPopup(string popup, EntityUid user)
    {
        var ev = new SkillPopupEvent(popup);
        if (_net.IsServer)
            RaiseNetworkEvent(ev, user);
        else if (_player.LocalEntity == user && _timing.IsFirstTimePredicted)
            RaiseLocalEvent(ev);
    }

    public void AddExperience(EntityUid target, [ForbidLiteral] EntProtoId id, int xp, int levelCap = 100, bool popup = true)
    {
        if (GetContainer(target) is { } container)
            AddExperience(container, id, xp, levelCap, popup);
    }

    public void AddExperience(Entity<KnowledgeContainerComponent> ent, [ForbidLiteral] EntProtoId id, int xp, int levelCap = 100, bool popup = true)
    {
        if (!_skillGain)
            return;

        if (GetKnowledge(ent, id) is not { } unit)
        {
            if (ProtoMan.Index(id).TryGetComponent<KnowledgeComponent>(out var knowledge, Factory) && knowledge?.Complex == true)
                return;

            if (SharedRandomExtensions.PredictedProb(_timing, _learnChance, GetNetEntity(ent)))
                EnsureKnowledge(ent, id, 0, popup);
            return;
        }

        if (ent.Comp.Holder is { } holder)
        {
            AddExperience(unit.AsNullable(), holder, xp, levelCap);

            var updateEv = new UpdateExperienceEvent();
            RaiseLocalEvent(holder, ref updateEv);
        }
    }

    public void AddExperience(Entity<KnowledgeComponent?> ent, EntityUid target, int added, int limit = 100)
    {
        if (!_skillGain || !_query.Resolve(ent, ref ent.Comp))
            return;

        var now = _timing.CurTime;
        if (now < ent.Comp.TimeToNextExperience || ent.Comp.LearnedLevel >= Math.Min(limit, 100))
            return;

        ent.Comp.TimeToNextExperience = now + ent.Comp.TimeBetweenExperience;
        ent.Comp.Experience += added + ent.Comp.BonusExperience;
        Dirty(ent);

        RollForLevelUp((ent, ent.Comp), target);
    }

    public bool RollForLevelUp(Entity<KnowledgeComponent> ent, EntityUid target)
    {
        if (ent.Comp.Experience < ent.Comp.ExperienceCost || ent.Comp.LearnedLevel >= 100)
            return false;

        var oldMastery = GetMastery(ent.Comp.NetLevel);

        int timesToRoll = ent.Comp.Experience / ent.Comp.ExperienceCost;
        ent.Comp.Experience -= ent.Comp.ExperienceCost * timesToRoll;
        for (int i = 0; i < timesToRoll && ent.Comp.LearnedLevel < 100; i++)
        {
            var rollInnard = RollPenetrating(ent);
            ent.Comp.LearnedLevel += rollInnard.Item1;
        }

        if (ent.Comp.LearnedLevel > 100)
            ent.Comp.LearnedLevel = 100;

        if (oldMastery != GetMastery(ent.Comp.NetLevel))
            SkillPopup(Loc.GetString("knowledge-level-up-popup", ("knowledge", Name(ent)), ("mastery", GetMasteryString(ent).ToLower())), target);

        return true;
    }

    public (ProtoId<KnowledgeCategoryPrototype> Category, KnowledgeInfo Info) GetKnowledgeInfo(Entity<KnowledgeComponent> ent)
    {
        var meta = MetaData(ent);
        var name = meta.EntityName;
        var desc = meta.EntityDescription;
        var levelStr = Loc.GetString("knowledge-info-description", ("level", ent.Comp.NetLevel), ("mastery", GetMasteryString(ent)));
        var knowledgeInfo = new KnowledgeInfo(name, desc, levelStr, ent.Comp.Color, ent.Comp.Sprite, ent.Comp.LearnedLevel, ent.Comp.NetLevel, ent.Comp.Experience, ent.Comp.ExperienceCost);
        return (ent.Comp.Category, knowledgeInfo);
    }

    public Entity<KnowledgeComponent>? EnsureKnowledge(Entity<KnowledgeContainerComponent> ent, [ForbidLiteral] EntProtoId id, int level = 0, bool popup = true)
    {
        if (GetKnowledge(ent, id) is { } existing)
        {
            if (existing.Comp.LearnedLevel < level)
            {
                existing.Comp.LearnedLevel = level;
                Dirty(existing, existing.Comp);
            }
            return existing;
        }

        PredictedTrySpawnInContainer(id, ent.Owner, KnowledgeContainerComponent.ContainerId, out var spawned);
        if (spawned is not { } unit)
        {
            Log.Error($"Failed to spawn knowledge {id} for {ToPrettyString(ent)}!");
            return null;
        }

        var comp = _query.Comp(unit);
        comp.LearnedLevel = level;
        Dirty(unit, comp);

        ent.Comp.KnowledgeDict[id] = unit;
        DirtyField(ent, ent.Comp, nameof(KnowledgeContainerComponent.KnowledgeDict));

        if (ent.Comp.Holder is not { } holder)
            return (unit, comp);

        var ev = new KnowledgeAddedEvent(ent, holder);
        RaiseLocalEvent(unit, ref ev);

        if (popup)
        {
            var msg = Loc.GetString("knowledge-unit-learned-popup", ("knowledge", Name(unit)));
            SkillPopup(msg, holder);
        }
        return (unit, comp);
    }

    public Entity<KnowledgeComponent>? RaiseMastery(Entity<KnowledgeContainerComponent> ent, [ForbidLiteral] EntProtoId id, int mastery, bool popup = true)
    {
        if (EnsureKnowledge(ent, id, popup: popup) is not { } unit)
            return null;

        mastery += GetMastery(unit.Comp.LearnedLevel);
        var level = GetInverseMastery(mastery);
        unit.Comp.LearnedLevel = Math.Min(level, 100);
        Dirty(unit);
        return unit;
    }

    public void AddKnowledgeUnits(EntityUid target, Dictionary<EntProtoId, int> knowledgeList, bool popup = false)
    {
        if (GetContainer(target) is not { } ent)
            return;

        foreach (var (id, level) in knowledgeList)
        {
            EnsureKnowledge(ent, id, level, popup);
        }

        var updateEv = new UpdateExperienceEvent();
        RaiseLocalEvent(target, ref updateEv);
    }

    public EntityUid? RemoveKnowledge(EntityUid target, [ForbidLiteral] EntProtoId id, bool force = false)
    {
        if (_timing.ApplyingState ||
            GetContainer(target) is not { } ent ||
            ent.Comp.Holder is not { } holder ||
            ent.Comp.Container is not { } container ||
            GetKnowledge(ent, id) is not { } unit ||
            unit.Comp.Unremoveable && !force ||
            !_container.Remove(unit.Owner, container, force: force))
            return null;

        ent.Comp.KnowledgeDict.Remove(id);
        DirtyField(ent, ent.Comp, nameof(KnowledgeContainerComponent.KnowledgeDict));

        var ev = new KnowledgeRemovedEvent(ent, holder);
        RaiseLocalEvent(unit, ref ev);

        PredictedQueueDel(unit);

        SkillPopup(Loc.GetString("knowledge-unit-forgotten-popup", ("knowledge", Name(unit))), holder);
        return target;
    }

    public override Entity<KnowledgeComponent>? GetKnowledge(EntityUid target, [ForbidLiteral] EntProtoId id)
        => GetContainer(target) is { } ent
            ? GetKnowledge(ent, id)
            : null;

    public int GetKnowledgeLevel(EntityUid target, [ForbidLiteral] EntProtoId id)
        => GetContainer(target) is { } ent
            ? GetKnowledge(ent, id)?.Comp.NetLevel ?? 0
            : 0;

    public Entity<KnowledgeComponent>? GetKnowledge(Entity<KnowledgeContainerComponent> ent, [ForbidLiteral] EntProtoId id)
        => ent.Comp.KnowledgeDict.TryGetValue(id, out var unit) && _query.TryComp(unit, out var comp)
            ? (unit, comp)
            : null;

    public List<Entity<KnowledgeComponent>>? TryGetAllKnowledgeUnits(EntityUid target)
    {
        if (GetContainer(target) is not { } ent)
            return null;

        var found = new List<Entity<KnowledgeComponent>>();
        foreach (var unit in ent.Comp.KnowledgeDict.Values)
        {
            if (_query.TryComp(unit, out var comp))
                found.Add((unit, comp));
        }

        return found;
    }

    /// <summary>
    /// Returns all knowledge entities that have a required component.
    /// </summary>
    public List<Entity<T, KnowledgeComponent>>? GetKnowledgeWith<T>(EntityUid target) where T : IComponent
    {
        if (GetContainer(target)?.Comp.Container is not { } container)
            return null;

        var knowledgeEnts = new List<Entity<T, KnowledgeComponent>>();
        var query = GetEntityQuery<T>();
        foreach (var knowledge in container.ContainedEntities)
        {
            if (!_query.TryComp(knowledge, out var knowledgeComp))
                continue;

            if (query.TryComp(knowledge, out var comp))
                knowledgeEnts.Add((knowledge, comp, knowledgeComp));
        }

        return knowledgeEnts;
    }

    public bool IsHolder(EntityUid target)
        => _holderQuery.HasComp(target);

    public override void ClearKnowledge(EntityUid target, bool deleteAll)
    {
        if (GetContainer(target) is not { } ent)
            return;

        ent.Comp.KnowledgeDict.Clear();
        DirtyField(ent, ent.Comp, nameof(KnowledgeContainerComponent.KnowledgeDict));
        if (deleteAll && ent.Comp.Container is { } container)
        {
            foreach (var entity in container.ContainedEntities)
            {
                PredictedQueueDel(entity);
            }
        }
    }

    public Entity<KnowledgeContainerComponent>? GetContainer(EntityUid uid)
    {
        if (_containerQuery.TryComp(uid, out var comp))
            return (uid, comp);

        if (_holderQuery.CompOrNull(uid)?.KnowledgeEntity is not { } ent || TerminatingOrDeleted(ent))
            return null;

        if (_containerQuery.TryComp(ent, out var container))
            return (ent, container);

        return null;
    }

    public bool IsAwake(EntityUid ent)
        => _mobState.IsAlive(ent) && !_sleepingQuery.HasComp(ent);

    public void RelayEvent<T>(Entity<KnowledgeHolderComponent> ent, ref T args) where T : notnull
    {
        if (!IsAwake(ent) || GetContainer(ent)?.Comp.Container is not { } container)
            return;

        foreach (var unit in container.ContainedEntities)
        {
            RaiseLocalEvent(unit, ref args);
        }
    }

    /// <summary>
    /// Relays an event to all non-martial arts knowledges a mob has.
    /// It also relays it to the active martial art, but not any inactive ones.
    /// </summary>
    public void RelayActiveEvent<T>(Entity<KnowledgeHolderComponent> ent, ref T args) where T : notnull
    {
        if (!IsAwake(ent) || GetContainer(ent) is not {} brain || brain.Comp.Container is not {} container)
            return;

        foreach (var unit in container.ContainedEntities)
        {
            // dont relay to inactive martial arts
            if (_artQuery.HasComp(unit) && unit != brain.Comp.ActiveMartialArt)
                continue;

            RaiseLocalEvent(unit, ref args);
        }
    }

    /// <summary>
    /// Relays an event only to the mob's currently active martial art, if any.
    /// </summary>
    public void RelayMartialArt<T>(Entity<KnowledgeHolderComponent> ent, ref T args) where T : notnull
    {
        if (IsAwake(ent) && GetActiveMartialArt(ent) is {} skill)
            RaiseLocalEvent(skill, ref args);
    }

    public override Dictionary<EntProtoId, int> GetSkillMasteries(EntityUid target)
    {
        var skills = new Dictionary<EntProtoId, int>();
        if (GetContainer(target) is not {} brain)
            return skills;

        foreach (var (id, unit) in brain.Comp.KnowledgeDict)
        {
            skills[id] = GetMastery(unit);
        }
        return skills;
    }

    public string GetMasteryString(Entity<KnowledgeComponent> ent)
        => GetMasteryString(GetMastery(ent.Comp.NetLevel));

    public override string GetMasteryString(int mastery)
    {
        var clamped = Math.Clamp(mastery, 0, 4);
        return Loc.TryGetString($"knowledge-mastery-{clamped}", out var str) ? str : MasteryNames[clamped];
    }

    public override int GetMastery(int level)
        => level switch
        {
            >= 100 => 5,
            >= 88 => 4,
            >= 75 => 3,
            >= 50 => 2,
            >= 25 => 1,
            _ => 0,
        };

    public override int GetMastery(EntityUid uid)
        => GetMastery(GetLevel(uid));

    public int GetLevel(EntityUid uid)
        => _query.TryComp(uid, out var comp)
            ? Math.Clamp(comp.NetLevel, 0, 100)
            : 0;

    public override int GetInverseMastery(int mastery)
        => mastery switch
        {
            >= 5 => 100,
            >= 4 => 88,
            >= 3 => 75,
            >= 2 => 50,
            >= 1 => 25,
            _ => 0,
        };

    private int DiceDictionary(Entity<KnowledgeComponent> ent, int shift = 0)
    {
        return (GetMastery(ent.Comp) + shift) switch
        {
            >= 4 => 3,
            >= 3 => 4,
            >= 2 => 6,
            >= 1 => 8,
            _ => 12,
        };
    }

    public override float SharpCurve(Entity<KnowledgeComponent> knowledge, int offset = 0, float inverseScale = 100.0f)
        => SharpCurve(knowledge.Comp.NetLevel, offset, inverseScale);

    public float SharpCurve(int level, int offset = 0, float inverseScale = 100f)
    {
        var linear = (float) (level + offset) / inverseScale;
        return linear * linear;
    }

    public (int, bool) RollPenetrating(Entity<KnowledgeComponent> ent)
    {
        var rand = SharedRandomExtensions.PredictedRandom(_timing, GetNetEntity(ent.Owner));
        var sides = DiceDictionary(ent);
        var isCritical = false;
        int penetratingRolls = 0;
        int currentRoll = rand.Next(1, sides + 1);
        int total = currentRoll;

        while (currentRoll == sides && penetratingRolls < 10)
        {
            sides = DiceDictionary(ent, penetratingRolls / 2);
            currentRoll = rand.Next(1, sides + 1);
            total += currentRoll - 1;
            isCritical = true;
            penetratingRolls++;
        }

        return (total, isCritical);
    }

    private Container EnsureContainer(Entity<KnowledgeContainerComponent> ent)
    {
        if (ent.Comp.Container != null)
            return ent.Comp.Container;

        ent.Comp.Container = _container.EnsureContainer<Container>(ent.Owner, KnowledgeContainerComponent.ContainerId);
        return ent.Comp.Container;
    }

    public Entity<KnowledgeContainerComponent> EnsureKnowledgeContainer(EntityUid uid)
    {
        EnsureComp<KnowledgeHolderComponent>(uid);
        if (GetContainer(uid) is { } brain)
            return brain;

        var comp = EnsureComp<KnowledgeContainerComponent>(uid);
        LinkContainer(uid, (uid, comp));
        return (uid, comp);
    }

    /// <summary>
    /// Applies job knowledge floors to an entity.
    /// Only raises skills if below floor; does not stack extra levels.
    /// </summary>
    public void ApplyJobFloors(EntityUid mob, Dictionary<EntProtoId, int> floors)
    {
        var container = EnsureKnowledgeContainer(mob);
        foreach (var (skillId, floor) in floors)
        {
            if (EnsureKnowledge(container, skillId, popup: false) is not { } unit ||
                GetMastery(unit.Comp.LearnedLevel) >= floor)
            {
                continue;
            }

            unit.Comp.LearnedLevel = GetInverseMastery(floor);
            Dirty(unit);
        }
    }
}

/// <summary>
/// Raised on a knowledge entity after it gets added to a container.
/// </summary>
[ByRefEvent]
public record struct KnowledgeAddedEvent(Entity<KnowledgeContainerComponent> Container, EntityUid Holder);

/// <summary>
/// Raised on a knowledge entity after it has been removed from a container, before deleting it.
/// </summary>
[ByRefEvent]
public record struct KnowledgeRemovedEvent(Entity<KnowledgeContainerComponent> Container, EntityUid Holder);

/// <summary>
/// Raised on an active knowledge entity just before deactivating it.
/// </summary>
[ByRefEvent]
public record struct KnowledgeEnabledEvent(Entity<KnowledgeContainerComponent> Container, EntityUid Holder);

/// <summary>
/// Raised on an active knowledge entity just after activating it.
/// </summary>
[ByRefEvent]
public record struct KnowledgeDisabledEvent(Entity<KnowledgeContainerComponent> Container, EntityUid Holder);

/// <summary>
/// Event to try show a skill popup to the user.
/// </summary>
[Serializable, NetSerializable]
public sealed class SkillPopupEvent(string popup) : EntityEventArgs
{
    public readonly string Popup = popup;
}
