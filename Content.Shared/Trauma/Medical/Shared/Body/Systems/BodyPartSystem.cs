using System.Linq;
using Content.Medical.Common.Body;
using Content.Shared.Body;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids;
using Content.Shared.Gibbing;
using Content.Medical.Common.Wounds;
using Content.Medical.Shared.Wounds;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Medical.Shared.Body;

/// <summary>
/// System that handles bodypart logic and provides API for working with them.
/// </summary>
public sealed partial class BodyPartSystem : CommonBodyPartSystem
{
    [Dependency] private BodySystem _body = default!;
    [Dependency] private BodyCacheSystem _cache = default!;
    [Dependency] private OrganRelationSystem _organRelation = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedPuddleSystem _puddle = default!;
    [Dependency] private EntityQuery<BodyPartComponent> _query = default!;
    [Dependency] private EntityQuery<ChildOrganComponent> _childQuery = default!;
    [Dependency] private EntityQuery<OrganComponent> _organQuery = default!;

    private static readonly SoundSpecifier GibSound = new SoundCollectionSpecifier("gib", AudioParams.Default.WithVariation(0.025f));
    private static readonly ProtoId<ReagentPrototype> BloodReagent = "Blood";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyPartComponent, OrganGotInsertedEvent>(OnPartInserted);
        SubscribeLocalEvent<BodyPartComponent, OrganGotRemovedEvent>(OnPartRemoved);
        SubscribeLocalEvent<BodyPartComponent, BeingGibbedEvent>(OnBeingGibbed);
        SubscribeLocalEvent<BodyPartComponent, BodyRelayedEvent<BeingGibbedEvent>>(OnBodyRelayedBeingGibbed);
    }

    private void OnPartInserted(Entity<BodyPartComponent> ent, ref OrganGotInsertedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        // fresh part, no organs inside
        if (GetSeveredOrgansContainer(ent.AsNullable()) is not {} container)
            return;

        Entity<BodyComponent?> body = args.Target;
        var organs = new List<EntityUid>(container.ContainedEntities); // no CME
        foreach (var organ in organs)
        {
            if (_body.InsertOrgan(body, organ))
                continue;

            Log.Error($"Couldn't insert {ToPrettyString(organ)} from {ToPrettyString(ent)} back into {ToPrettyString(args.Target)} after being attached, ejecting it");
            if (!_container.Remove(organ, container))
                Log.Error($"Organ {ToPrettyString(organ)} got stuck inside of {ToPrettyString(ent)} after being inserted into {ToPrettyString(args.Target)}");
        }

        // need to mark the part so it renders!
        Dirty(ent);
    }

    private void OnPartRemoved(Entity<BodyPartComponent> ent, ref OrganGotRemovedEvent args)
    {
        // don't transfer parts if the body is being deleted
        // note that this will still transfer if the part is being deleted, so its organs will go away too
        if (TerminatingOrDeleted(args.Target) || _timing.ApplyingState)
            return;

        Entity<BodyComponent?> body = args.Target;
        if (TerminatingOrDeleted(ent))
        {
            // this part is being deleted so detach the children
            foreach (var organ in ent.Comp.Children.Values.ToArray())
            {
                _body.RemoveOrgan(body, organ);
            }
            return;
        }

        var container = EnsureSeveredOrgansContainer(ent);
        foreach (var (category, organ) in ent.Comp.Children.ToArray())
        {
            _body.RemoveOrgan(body, organ);
            // slot has an organ so try to put it in the container
            if (!_container.Insert(organ, container, force: true))
            {
                // probably from failing to be removed, suspicious
                Log.Error($"Failed to store {ToPrettyString(ent)}'s {category} organ {ToPrettyString(organ)}!");
                continue;
            }
        }
    }

    private void OnBeingGibbed(Entity<BodyPartComponent> ent, ref BeingGibbedEvent args)
    {
        SpillBodyPartOrgans(ent, args.Giblets);
    }

    private void OnBodyRelayedBeingGibbed(Entity<BodyPartComponent> ent, ref BodyRelayedEvent<BeingGibbedEvent> args)
    {
        // Only root body parts (like Torso) need to spill when relayed from body;
        // child limbs will be detached by the root part.
        if (!HasComp<ChildOrganComponent>(ent))
            SpillBodyPartOrgans(ent, args.Args.Giblets);
    }

    private void SpillBodyPartOrgans(Entity<BodyPartComponent> ent, HashSet<EntityUid> giblets)
    {
        var organsToSpill = new List<EntityUid>();

        if (GetSeveredOrgansContainer(ent.AsNullable()) is {} container)
        {
            foreach (var organ in container.ContainedEntities.ToArray())
            {
                organsToSpill.Add(organ);
            }
        }

        Entity<BodyComponent?>? body = _body.GetBody(ent.Owner) is { } bodyUid ? bodyUid : null;

        foreach (var (category, organ) in ent.Comp.Children.ToArray())
        {
            if (Deleted(organ) || organsToSpill.Contains(organ))
                continue;

            // If the child is a body part itself (e.g. arm, leg, head attached to torso),
            // sever it: gather its sub-children into its own container so they survive with the limb.
            if (_query.TryComp(organ, out var childPartComp))
            {
                if (body != null)
                {
                    var subChildren = _organRelation.AllChildren(organ).ToList();
                    var partContainer = EnsureSeveredOrgansContainer((organ, childPartComp));

                    _body.RemoveOrgan(body.Value, organ);

                    var delimbedEvent = new BodyPartDelimbedEvent(body.Value.Owner, organ, Crude: true);
                    RaiseLocalEvent(body.Value.Owner, ref delimbedEvent);

                    foreach (var subChild in subChildren)
                    {
                        _body.RemoveOrgan(body.Value, subChild.Owner);
                        // Clear severed container of sub-child parts if any, to avoid nested OnPartInserted
                        if (_container.Insert(subChild.Owner, partContainer, force: true))
                        {
                            if (_body.GetCategory(subChild.Owner) is { } subCat)
                                childPartComp.Children[subCat] = subChild.Owner;
                        }
                    }
                    DirtyField(organ, childPartComp, nameof(BodyPartComponent.Children));
                }

                if (TryComp<WoundableComponent>(organ, out var woundable))
                {
                    woundable.WoundableSeverity = WoundableSeverity.Severed;
                    DirtyField(organ, woundable, nameof(WoundableComponent.WoundableSeverity));
                }

                organsToSpill.Add(organ);
            }
            else
            {
                // Internal organ without BodyPartComponent (e.g. heart, lungs, etc.)
                if (body != null && _organQuery.TryComp(organ, out var organComp) && organComp.Body is { })
                {
                    _body.RemoveOrgan(body.Value, organ);
                }
                organsToSpill.Add(organ);
            }
        }

        ent.Comp.Children.Clear();
        DirtyField(ent.Owner, ent.Comp, nameof(BodyPartComponent.Children));

        if (organsToSpill.Count == 0)
            return;

        var dropTarget = body != null ? body.Value.Owner : ent.Owner;
        _audio.PlayPvs(GibSound, dropTarget);

        var bloodSolution = new Solution();
        bloodSolution.AddReagent(BloodReagent, FixedPoint2.New(15));
        _puddle.TrySpillAt(dropTarget, bloodSolution, out _, sound: false);

        foreach (var organ in organsToSpill)
        {
            giblets.Add(organ);
        }
    }

    internal void OrganInserted(Entity<BodyPartComponent?> part, Entity<OrganComponent?> organ)
    {
        DebugTools.Assert(part.Owner != organ.Owner);
        if (!_query.Resolve(part, ref part.Comp) ||
            _body.GetCategory(organ) is not {} category ||
            !CanInsertOrgan(part, category)) // just incase
            return;

        part.Comp.Children[category] = organ;
        DirtyField(part, part.Comp, nameof(BodyPartComponent.Children));

        var ev = new OrganInsertedIntoPartEvent(organ, category);
        RaiseLocalEvent(part, ref ev);
    }

    internal void OrganRemoved(Entity<BodyPartComponent?> part, Entity<OrganComponent?> organ)
    {
        DebugTools.Assert(part.Owner != organ.Owner);
        if (!_query.Resolve(part, ref part.Comp, logMissing: false) ||
            _body.GetCategory(organ) is not {} category)
            return;

        part.Comp.Children.Remove(category);
        DirtyField(part, part.Comp, nameof(BodyPartComponent.Children));

        var ev = new OrganRemovedFromPartEvent(organ, category);
        RaiseLocalEvent(part, ref ev);
    }
}
