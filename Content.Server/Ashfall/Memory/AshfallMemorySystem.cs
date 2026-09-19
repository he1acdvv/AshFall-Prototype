using Content.Server.Ashfall.CharacterGen;
using Content.Shared.Ashfall;
using Content.Shared.Ashfall.Memory;
using Content.Shared.Ashfall.Memory.Prototypes;
using Content.Shared.Chat;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Popups;
using Content.Shared.Roles.Jobs;
using Content.Shared.Verbs;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Ashfall.Memory;

/// <summary>
///     Manages inter-character memories: weaves relationships between spawned characters,
///     discovers them through proximity/examine/voice triggers, and presents them via
///     popups, chat, and examine verbs.
/// </summary>
public sealed partial class AshfallMemorySystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedJobSystem _jobs = default!;

    private sealed class MemoryLink
    {
        public EntityUid EntityA;
        public EntityUid EntityB;
        public ProtoId<AshfallMemoryTemplatePrototype> TemplateId;
        public AshfallMemoryTier Tier;
        public AshfallMemoryCategory Category;
        public LocId SideATextLoc;
        public LocId? SideBTextLoc;
        public LocId SideASummaryLoc;
        public LocId? SideBSummaryLoc;
        public bool DiscoveredByA;
        public bool DiscoveredByB;
        public TimeSpan? PendingDiscoveryA;
        public TimeSpan? PendingDiscoveryB;
    }

    private readonly List<MemoryLink> _allLinks = new();
    private readonly Dictionary<EntityUid, List<MemoryLink>> _entityIndex = new();

    // Pre-cached recognition line LocIds, keyed by category.
    private readonly Dictionary<AshfallMemoryCategory, List<LocId>> _recognitionPools = new();

    // Pre-cached mutual hint line LocIds.
    private readonly List<LocId> _hintLines = new();

    private float _proximityTimer;
    private ISawmill _sawmill = default!;


    public override void Initialize()
    {
        base.Initialize();
        _sawmill = Logger.GetSawmill("ashfall.memory");

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
        SubscribeLocalEvent<CharacterMemoryComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CharacterMemoryComponent, GetVerbsEvent<ExamineVerb>>(OnGetExamineVerbs);
        SubscribeLocalEvent<CharacterMemoryComponent, EntitySpokeEvent>(OnEntitySpoke);
        SubscribeLocalEvent<CharacterMemoryComponent, ComponentRemove>(OnComponentRemove);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);

        CacheRecognitionPools();
        CacheHintLines();
    }

    public override void Update(float frameTime)
    {
        if (!_cfg.GetCVar(AshfallCCVars.MemoryEnabled))
            return;

        var interval = _cfg.GetCVar(AshfallCCVars.MemoryProximityInterval);
        _proximityTimer += frameTime;
        if (_proximityTimer < interval)
            return;
        _proximityTimer = 0;

        CheckProximityDiscoveries();
    }


    private void CacheRecognitionPools()
    {
        _recognitionPools.Clear();

        foreach (var pool in _proto.EnumeratePrototypes<AshfallRecognitionPoolPrototype>())
        {
            if (!_recognitionPools.TryGetValue(pool.Category, out var existing))
            {
                _recognitionPools[pool.Category] = new List<LocId>(pool.Lines);
            }
            else
            {
                existing.AddRange(pool.Lines);
            }
        }
    }

    private void CacheHintLines()
    {
        _hintLines.Clear();
        for (var i = 1; i <= 5; i++)
        {
            var key = $"ashfall-memory-hint-{i}";
            if (Loc.HasString(key))
                _hintLines.Add(key);
        }
    }


    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        if (!_cfg.GetCVar(AshfallCCVars.MemoryEnabled))
            return;

        var comp = EnsureComp<CharacterMemoryComponent>(ev.Mob);

        // Add derived tags from the profile.
        comp.MatchingTags.Add($"species-{ev.Profile.Species}");

        var age = ev.Profile.Age;
        comp.MatchingTags.Add(age switch
        {
            < 30 => "age-young",
            < 50 => "age-middle",
            _ => "age-senior",
        });

        // Add department and family tags from Job.
        if (ev.JobId != null && _jobs.TryGetAllDepartments(ev.JobId, out var departments))
        {
            foreach (var dept in departments)
            {
                comp.MatchingTags.Add($"family-{dept.ID}");
                comp.MatchingTags.Add($"domain-{dept.ID}");
            }
        }

        // Also merge character pool candidate data if available.
        if (EntityManager.TrySystem<AshfallCharacterPoolSystem>(out var poolSystem) &&
            poolSystem.TryGetSelectedCandidate(ev.Player.UserId, out var candidate))
        {
            if (poolSystem.TryGetMemoryTags(candidate.CandidateId, out var storedTags))
                comp.MatchingTags.UnionWith(storedTags);

            if (!string.IsNullOrEmpty(candidate.PrimaryDomain))
            {
                comp.MatchingTags.Add($"domain-{candidate.PrimaryDomain}");
                comp.MatchingTags.Add($"family-{candidate.PrimaryDomain}");
            }

            if (candidate.Dossier.CultureId != null)
                comp.MatchingTags.Add($"culture-{candidate.Dossier.CultureId}");
        }

        WeaveMemoriesForNewCharacter(ev.Mob, comp);
    }

    private void OnComponentRemove(EntityUid uid, CharacterMemoryComponent component, ComponentRemove args)
    {
        if (!_entityIndex.TryGetValue(uid, out var links))
            return;

        foreach (var link in links)
        {
            _allLinks.Remove(link);

            var other = link.EntityA == uid ? link.EntityB : link.EntityA;
            if (_entityIndex.TryGetValue(other, out var otherLinks))
                otherLinks.Remove(link);
        }

        _entityIndex.Remove(uid);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _allLinks.Clear();
        _entityIndex.Clear();
        _proximityTimer = 0;
    }


    private void WeaveMemoriesForNewCharacter(EntityUid newChar, CharacterMemoryComponent newComp)
    {
        var minMemories = _cfg.GetCVar(AshfallCCVars.MemoryMinPerCharacter);
        var maxMemories = _cfg.GetCVar(AshfallCCVars.MemoryMaxPerCharacter);
        var baseProbability = _cfg.GetCVar(AshfallCCVars.MemoryBaseProbability);

        var newCount = GetMemoryCount(newChar);

        // Collect eligible partners (existing characters with room for more memories).
        var eligible = new List<(EntityUid Uid, CharacterMemoryComponent Comp)>();
        var query = EntityQueryEnumerator<CharacterMemoryComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (uid == newChar)
                continue;

            if (GetMemoryCount(uid) >= maxMemories)
                continue;

            eligible.Add((uid, comp));
        }

        if (eligible.Count == 0)
            return;

        _random.Shuffle(eligible);

        foreach (var (otherUid, otherComp) in eligible)
        {
            if (newCount >= maxMemories)
                break;

            var otherCount = GetMemoryCount(otherUid);
            if (otherCount >= maxMemories)
                continue;

            // Force creation if below minimum quota.
            bool create;
            if (newCount < minMemories)
            {
                create = true;
            }
            else
            {
                // Diminishing probability as both characters accumulate memories.
                var newFactor = 1f - (float) newCount / maxMemories;
                var otherFactor = 1f - (float) otherCount / maxMemories;
                var prob = baseProbability * newFactor * otherFactor;
                create = _random.Prob(prob);
            }

            if (!create)
                continue;

            if (TryCreateMemoryLink(newChar, newComp, otherUid, otherComp))
                newCount++;
        }
    }

    private bool TryCreateMemoryLink(
        EntityUid entityA, CharacterMemoryComponent compA,
        EntityUid entityB, CharacterMemoryComponent compB)
    {
        // Collect eligible templates.
        var candidates = new List<(AshfallMemoryTemplatePrototype Template, float Weight)>();

        foreach (var template in _proto.EnumeratePrototypes<AshfallMemoryTemplatePrototype>())
        {
            // Try both orderings: (A=entityA, B=entityB) and (A=entityB, B=entityA).
            var weight = TryMatchTemplate(template, compA.MatchingTags, compB.MatchingTags);
            if (weight > 0)
            {
                candidates.Add((template, weight));
                continue;
            }

            // Try reversed if there are side-specific requirements.
            if (template.SideARequiredTags.Count > 0 || template.SideBRequiredTags.Count > 0)
            {
                weight = TryMatchTemplate(template, compB.MatchingTags, compA.MatchingTags);
                if (weight > 0)
                {
                    // Swap: entityB becomes side A.
                    candidates.Add((template, -weight)); // Negative = swapped
                }
            }
        }

        if (candidates.Count == 0)
        {
            _sawmill.Debug($"No eligible memory templates for pair {ToPrettyString(entityA)} / {ToPrettyString(entityB)}");
            return false;
        }

        // Weighted selection.
        var totalWeight = 0f;
        foreach (var (_, w) in candidates)
            totalWeight += MathF.Abs(w);

        var roll = _random.NextFloat(totalWeight);
        var accumulated = 0f;
        AshfallMemoryTemplatePrototype? selected = null;
        var swapped = false;

        foreach (var (template, w) in candidates)
        {
            accumulated += MathF.Abs(w);
            if (roll < accumulated)
            {
                selected = template;
                swapped = w < 0;
                break;
            }
        }

        if (selected == null)
            return false;

        // Determine side assignment.
        EntityUid sideAEntity, sideBEntity;
        if (swapped)
        {
            sideAEntity = entityB;
            sideBEntity = entityA;
        }
        else if (selected.SideARequiredTags.Count == 0 && selected.SideBRequiredTags.Count == 0)
        {
            // No side requirements — random assignment.
            if (_random.Prob(0.5f))
            {
                sideAEntity = entityA;
                sideBEntity = entityB;
            }
            else
            {
                sideAEntity = entityB;
                sideBEntity = entityA;
            }
        }
        else
        {
            sideAEntity = entityA;
            sideBEntity = entityB;
        }

        var link = new MemoryLink
        {
            EntityA = sideAEntity,
            EntityB = sideBEntity,
            TemplateId = selected.ID,
            Tier = selected.Tier,
            Category = selected.Category,
            SideATextLoc = selected.SideAText,
            SideBTextLoc = selected.SideBText,
            SideASummaryLoc = selected.SideASummary,
            SideBSummaryLoc = selected.SideBSummary,
        };

        _allLinks.Add(link);
        GetOrCreateIndex(sideAEntity).Add(link);
        GetOrCreateIndex(sideBEntity).Add(link);

        _sawmill.Debug(
            $"Created memory link [{selected.ID}] between {ToPrettyString(sideAEntity)} (A) and {ToPrettyString(sideBEntity)} (B)");

        return true;
    }

    private string GetSideText(MemoryLink link, bool isSideA)
    {
        var user = isSideA ? link.EntityA : link.EntityB;
        var target = isSideA ? link.EntityB : link.EntityA;
        var locId = isSideA ? link.SideATextLoc : (link.SideBTextLoc ?? link.SideATextLoc);
        return Loc.GetString(locId, ("user", user), ("target", target));
    }

    private string GetSideSummary(MemoryLink link, bool isSideA)
    {
        var user = isSideA ? link.EntityA : link.EntityB;
        var target = isSideA ? link.EntityB : link.EntityA;
        var locId = isSideA ? link.SideASummaryLoc : (link.SideBSummaryLoc ?? link.SideASummaryLoc);
        return Loc.GetString(locId, ("user", user), ("target", target));
    }

    private float TryMatchTemplate(
        AshfallMemoryTemplatePrototype template,
        HashSet<string> sideATags,
        HashSet<string> sideBTags)
    {
        // Excluded tags: neither side may have them.
        foreach (var tag in template.ExcludedTags)
        {
            if (sideATags.Contains(tag) || sideBTags.Contains(tag))
                return 0;
        }

        // Required shared tags: both sides must have all of them.
        foreach (var tag in template.RequiredSharedTags)
        {
            if (!sideATags.Contains(tag) || !sideBTags.Contains(tag))
                return 0;
        }

        // Required any tags: at least one side must have at least one.
        if (template.RequiredAnyTags.Count > 0)
        {
            var found = false;
            foreach (var tag in template.RequiredAnyTags)
            {
                if (sideATags.Contains(tag) || sideBTags.Contains(tag))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
                return 0;
        }

        // Side-specific requirements.
        foreach (var tag in template.SideARequiredTags)
        {
            if (!sideATags.Contains(tag))
                return 0;
        }

        foreach (var tag in template.SideBRequiredTags)
        {
            if (!sideBTags.Contains(tag))
                return 0;
        }

        return template.Weight;
    }


    private void CheckProximityDiscoveries()
    {
        var curTime = _timing.CurTime;
        var range = _cfg.GetCVar(AshfallCCVars.MemoryProximityRange);
        var minDelay = MathF.Max(0.5f, _cfg.GetCVar(AshfallCCVars.MemoryMinDiscoveryDelay));
        var maxDelay = MathF.Max(minDelay + 1f, _cfg.GetCVar(AshfallCCVars.MemoryMaxDiscoveryDelay));

        foreach (var link in _allLinks)
        {
            if (link.Tier != AshfallMemoryTier.Passive)
                continue;

            if (link.DiscoveredByA && link.DiscoveredByB)
                continue;

            var inRange = _transform.InRange(link.EntityA, link.EntityB, range);

            // Side A: calculate or trigger delayed recognition
            if (!link.DiscoveredByA)
            {
                if (inRange)
                {
                    if (link.PendingDiscoveryA == null)
                    {
                        var delayA = _random.NextFloat(minDelay, maxDelay);
                        // Prevent identical or near-simultaneous discovery if side B is already pending
                        if (link.PendingDiscoveryB != null)
                        {
                            var bRemaining = (float)(link.PendingDiscoveryB.Value - curTime).TotalSeconds;
                            if (MathF.Abs(delayA - bRemaining) < 2.0f)
                            {
                                delayA = bRemaining >= (minDelay + maxDelay) / 2f
                                    ? MathF.Max(minDelay, bRemaining - 3.0f)
                                    : MathF.Min(maxDelay, bRemaining + 3.0f);
                            }
                        }
                        link.PendingDiscoveryA = curTime + TimeSpan.FromSeconds(delayA);
                    }
                    else if (curTime >= link.PendingDiscoveryA.Value)
                    {
                        TriggerDiscovery(link, link.EntityA, isSideA: true);
                    }
                }
                else
                {
                    link.PendingDiscoveryA = null;
                }
            }

            // Side B: calculate or trigger delayed recognition
            if (!link.DiscoveredByB)
            {
                if (inRange)
                {
                    if (link.PendingDiscoveryB == null)
                    {
                        var delayB = _random.NextFloat(minDelay, maxDelay);
                        // Prevent identical or near-simultaneous discovery if side A is already pending
                        if (link.PendingDiscoveryA != null)
                        {
                            var aRemaining = (float)(link.PendingDiscoveryA.Value - curTime).TotalSeconds;
                            if (MathF.Abs(delayB - aRemaining) < 2.0f)
                            {
                                delayB = aRemaining >= (minDelay + maxDelay) / 2f
                                    ? MathF.Max(minDelay, aRemaining - 3.0f)
                                    : MathF.Min(maxDelay, aRemaining + 3.0f);
                            }
                        }
                        link.PendingDiscoveryB = curTime + TimeSpan.FromSeconds(delayB);
                    }
                    else if (curTime >= link.PendingDiscoveryB.Value)
                    {
                        TriggerDiscovery(link, link.EntityB, isSideA: false);
                    }
                }
                else
                {
                    link.PendingDiscoveryB = null;
                }
            }
        }
    }


    private void OnExamined(EntityUid uid, CharacterMemoryComponent component, ExaminedEvent args)
    {
        if (!_cfg.GetCVar(AshfallCCVars.MemoryEnabled))
            return;

        if (!HasComp<CharacterMemoryComponent>(args.Examiner) || args.Examiner == uid)
            return;

        if (!_entityIndex.TryGetValue(args.Examiner, out var examinerLinks))
            return;

        foreach (var link in examinerLinks)
        {
            var isTarget = link.EntityA == uid || link.EntityB == uid;
            if (!isTarget)
                continue;

            var examinerIsSideA = link.EntityA == args.Examiner;
            var discovered = examinerIsSideA ? link.DiscoveredByA : link.DiscoveredByB;

            // Direct examination immediately triggers undiscovered memory for the examiner
            if (!discovered)
                TriggerDiscovery(link, args.Examiner, examinerIsSideA);

            // After discovery, show summary in tooltip.
            discovered = examinerIsSideA ? link.DiscoveredByA : link.DiscoveredByB;
            if (discovered)
            {
                var summary = GetSideSummary(link, examinerIsSideA);
                args.PushMarkup($"[italic][color=#C8A96E]{summary}[/color][/italic]");
            }
        }
    }


    private void OnGetExamineVerbs(EntityUid uid, CharacterMemoryComponent component,
        GetVerbsEvent<ExamineVerb> args)
    {
        if (!_cfg.GetCVar(AshfallCCVars.MemoryEnabled))
            return;

        if (!HasComp<CharacterMemoryComponent>(args.User) || args.User == uid)
            return;

        if (!_entityIndex.TryGetValue(args.User, out var userLinks))
            return;

        // Collect all discovered memories about this target.
        var memories = new List<string>();
        foreach (var link in userLinks)
        {
            var isTarget = link.EntityA == uid || link.EntityB == uid;
            if (!isTarget)
                continue;

            var userIsSideA = link.EntityA == args.User;
            var discovered = userIsSideA ? link.DiscoveredByA : link.DiscoveredByB;
            if (!discovered)
                continue;

            var fullText = GetSideText(link, userIsSideA);
            memories.Add(fullText);
        }

        if (memories.Count == 0)
            return;

        var combinedText = string.Join("\n\n", memories);
        var verb = new ExamineVerb
        {
            Text = Loc.GetString("ashfall-memory-verb-remember"),
            Message = Loc.GetString("ashfall-memory-verb-remember-tooltip"),
            Category = VerbCategory.Examine,
            Icon = new SpriteSpecifier.Texture(
                new("/Textures/Interface/VerbIcons/information.svg.192dpi.png")),
            Act = () =>
            {
                var markup = FormattedMessage.FromMarkupPermissive(
                    $"[italic][color=#C8A96E]{combinedText}[/color][/italic]");
                _examine.SendExamineTooltip(args.User, uid, markup, false, false);
            },
        };

        args.Verbs.Add(verb);
    }


    private void OnEntitySpoke(EntityUid uid, CharacterMemoryComponent component, EntitySpokeEvent args)
    {
        if (!_cfg.GetCVar(AshfallCCVars.MemoryEnabled))
            return;

        var speaker = uid;
        if (!_entityIndex.TryGetValue(speaker, out var speakerLinks))
            return;

        var voiceRange = _cfg.GetCVar(AshfallCCVars.MemoryVoiceRange);

        foreach (var link in speakerLinks)
        {
            if (link.Tier != AshfallMemoryTier.Passive)
                continue;

            var listener = link.EntityA == speaker ? link.EntityB : link.EntityA;
            var listenerIsSideA = listener == link.EntityA;
            var discovered = listenerIsSideA ? link.DiscoveredByA : link.DiscoveredByB;

            if (discovered)
                continue;

            if (!_transform.InRange(speaker, listener, voiceRange))
                continue;

            TriggerDiscovery(link, listener, listenerIsSideA);
        }
    }


    private void TriggerDiscovery(MemoryLink link, EntityUid discoverer, bool isSideA)
    {
        // Mark as discovered and clear pending timers
        if (isSideA)
        {
            link.DiscoveredByA = true;
            link.PendingDiscoveryA = null;
        }
        else
        {
            link.DiscoveredByB = true;
            link.PendingDiscoveryB = null;
        }

        var otherEntity = isSideA ? link.EntityB : link.EntityA;

        // Pick recognition flash text with user and target context for gender inflection.
        var recognitionText = GetRecognitionLine(link, discoverer, otherEntity, isSideA);

        // Show recognition popup to the discoverer (at the other entity's position).
        // PopupSystem in Content.Client automatically logs this popup to the chat box,
        // so no explicit ChatMessageToOne is needed (which caused duplicate text).
        if (TryComp<ActorComponent>(discoverer, out var discovererActor))
        {
            _popup.PopupEntity(recognitionText, otherEntity, discovererActor.PlayerSession,
                PopupType.Medium);
        }

        // Mutual hint to the other person.
        if (TryComp<ActorComponent>(otherEntity, out var otherActor))
        {
            if (_hintLines.Count > 0)
            {
                var hintLocId = _random.Pick(_hintLines);
                var hintText = Loc.GetString(hintLocId, ("user", otherEntity), ("target", discoverer));
                _popup.PopupEntity(hintText, discoverer, otherActor.PlayerSession, PopupType.SmallCaution);
            }
        }

        _sawmill.Debug(
            $"Memory [{link.TemplateId}] discovered by {ToPrettyString(discoverer)} " +
            $"(side {(isSideA ? "A" : "B")})");
    }

    private string GetRecognitionLine(MemoryLink link, EntityUid discoverer, EntityUid target, bool isSideA)
    {
        // Check for template-specific override.
        if (_proto.TryIndex(link.TemplateId, out var template))
        {
            var overrideLocId = isSideA ? template.RecognitionLineA : template.RecognitionLineB;
            if (overrideLocId != null)
                return Loc.GetString(overrideLocId, ("user", discoverer), ("target", target));
        }

        // Fall back to category pool.
        if (_recognitionPools.TryGetValue(link.Category, out var pool) && pool.Count > 0)
        {
            var locId = _random.Pick(pool);
            return Loc.GetString(locId, ("user", discoverer), ("target", target));
        }

        return "...";
    }


    private int GetMemoryCount(EntityUid entity)
    {
        return _entityIndex.TryGetValue(entity, out var links) ? links.Count : 0;
    }

    private List<MemoryLink> GetOrCreateIndex(EntityUid entity)
    {
        if (!_entityIndex.TryGetValue(entity, out var list))
        {
            list = new List<MemoryLink>();
            _entityIndex[entity] = list;
        }

        return list;
    }

    public bool TryForceWeaveMemory(EntityUid entityA, EntityUid entityB, ProtoId<AshfallMemoryTemplatePrototype>? templateId, out string message)
    {
        if (entityA == entityB)
        {
            message = "Cannot weave memories with self.";
            return false;
        }

        var compA = EnsureComp<CharacterMemoryComponent>(entityA);
        var compB = EnsureComp<CharacterMemoryComponent>(entityB);

        EnsureBaselineTags(entityA, compA);
        EnsureBaselineTags(entityB, compB);

        if (templateId != null)
        {
            if (!_proto.TryIndex(templateId.Value, out var template))
            {
                message = $"Template prototype '{templateId.Value}' not found.";
                return false;
            }

            var link = new MemoryLink
            {
                EntityA = entityA,
                EntityB = entityB,
                TemplateId = template.ID,
                Tier = template.Tier,
                Category = template.Category,
                SideATextLoc = template.SideAText,
                SideBTextLoc = template.SideBText,
                SideASummaryLoc = template.SideASummary,
                SideBSummaryLoc = template.SideBSummary,
            };

            _allLinks.Add(link);
            GetOrCreateIndex(entityA).Add(link);
            GetOrCreateIndex(entityB).Add(link);

            message = $"Force-wove memory '{template.ID}' between {ToPrettyString(entityA)} (Side A) and {ToPrettyString(entityB)} (Side B).";
            return true;
        }

        if (TryCreateMemoryLink(entityA, compA, entityB, compB))
        {
            var links = _entityIndex[entityA];
            var created = links[^1];
            message = $"Successfully wove memory '{created.TemplateId}' between {ToPrettyString(entityA)} and {ToPrettyString(entityB)}.";
            return true;
        }

        message = $"Failed to find a matching memory template between {ToPrettyString(entityA)} and {ToPrettyString(entityB)} with current tags.";
        return false;
    }

    private void EnsureBaselineTags(EntityUid uid, CharacterMemoryComponent comp)
    {
        if (comp.MatchingTags.Count > 0)
            return;

        comp.MatchingTags.Add("species-Human");
        comp.MatchingTags.Add("age-middle");
    }

    public IReadOnlyList<(EntityUid Partner, string TemplateId, bool Discovered, string Summary)> GetEntityMemories(EntityUid uid)
    {
        if (!_entityIndex.TryGetValue(uid, out var links))
            return Array.Empty<(EntityUid, string, bool, string)>();

        var result = new List<(EntityUid Partner, string TemplateId, bool Discovered, string Summary)>();
        foreach (var link in links)
        {
            var isSideA = link.EntityA == uid;
            var partner = isSideA ? link.EntityB : link.EntityA;
            var discovered = isSideA ? link.DiscoveredByA : link.DiscoveredByB;
            var summary = GetSideSummary(link, isSideA);
            result.Add((partner, link.TemplateId.Id, discovered, summary));
        }

        return result;
    }
}
