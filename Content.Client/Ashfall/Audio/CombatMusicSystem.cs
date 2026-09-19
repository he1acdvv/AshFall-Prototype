using Content.Client.Audio;
using Content.Client.Gameplay;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Audio;
using Content.Shared.Body;
using Content.Shared.CCVar;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Content.Shared.Humanoid;
using Content.Shared.Item;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.State;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Ashfall.Audio;

/// <summary>
/// Dynamic client-side battle music system for Ashfall.
/// Triggers contextual combat music based on real battle engagement (taking/dealing damage, gunfire)
/// and player faction/role, smoothly cross-fading with ambient music.
/// </summary>
public sealed partial class CombatMusicSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IResourceCache _resourceCache = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IStateManager _state = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ContentAudioSystem _contentAudio = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedIdCardSystem _idCard = default!;

    private static readonly ProtoId<SoundCollectionPrototype> ScavengerMusicCollection = "CombatMusicScavenger";
    private static readonly ProtoId<SoundCollectionPrototype> SecurityMusicCollection = "CombatMusicSecurity";
    private static readonly ProtoId<SoundCollectionPrototype> SyndicateMusicCollection = "CombatMusicSyndicate";

    private readonly TimeSpan _combatDuration = TimeSpan.FromSeconds(20);
    private const float NearbyCombatRange = 40f;
    private TimeSpan _combatEndTime = TimeSpan.Zero;
    // The nearby-combatant scan walks every humanoid on the map; throttle it because combat
    // music only needs to know the fight is ongoing, not the exact moment of every shot.
    private static readonly TimeSpan CombatScanCooldown = TimeSpan.FromSeconds(1);
    private TimeSpan _nextCombatScan = TimeSpan.Zero;

    private EntityUid? _combatStream;
    private ProtoId<SoundCollectionPrototype>? _preparedCollection;
    private string? _preparedTrack;
    private bool _combatEnabled = true;
    private float _volumeGain = 1.0f;
    private bool _inCombat;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, CCVars.CombatMusicEnabled, OnCombatMusicToggleChanged, true);
        Subs.CVar(_cfg, CCVars.CombatMusicVolume, OnCombatMusicVolumeChanged, true);

        SubscribeLocalEvent<PlayAmbientMusicEvent>(OnPlayAmbientMusic);
        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<BodyComponent, DamageDealtEvent>(OnBodyDamageDealt);
        SubscribeLocalEvent<DamageableComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<InjurableComponent, HitscanRaycastStrikeEvent>(OnHitscanStrike);
        SubscribeLocalEvent<GunComponent, GunShotEvent>(OnGunShot);
        SubscribeLocalEvent<MeleeWeaponComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeAllEvent<MuzzleFlashEvent>(OnMuzzleFlash);
        SubscribeNetworkEvent<RoundEndMessageEvent>(OnRoundEnd);

        _state.OnStateChanged += OnStateChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _state.OnStateChanged -= OnStateChanged;
        StopCombatMusic(immediate: true);
    }

    private void OnCombatMusicToggleChanged(bool value)
    {
        _combatEnabled = value;
        if (!_combatEnabled)
        {
            StopCombatMusic(immediate: false);
            return;
        }

        PrepareCombatTrack();
    }

    private void OnCombatMusicVolumeChanged(float value)
    {
        _volumeGain = value;

        if (_combatStream != null && TryComp<AudioComponent>(_combatStream, out var audioComp))
        {
            _audio.SetVolume(_combatStream.Value, SharedAudioSystem.GainToVolume(_volumeGain), audioComp);
        }
    }

    private void OnPlayAmbientMusic(ref PlayAmbientMusicEvent ev)
    {
        if (_inCombat && _combatStream != null)
        {
            ev.Cancelled = true;
        }
    }

    private void OnPlayerAttached(LocalPlayerAttachedEvent args)
    {
        PrepareCombatTrack();
    }

    private void OnBodyDamageDealt(Entity<BodyComponent> ent, ref DamageDealtEvent args)
    {
        if (_player.LocalEntity != ent.Owner)
            return;

        if (args.ModifiedDamage.GetTotal() <= 0 ||
            args.Origin is not { } origin ||
            origin == ent.Owner ||
            !HasComp<ActorComponent>(origin))
            return;

        TriggerCombat();
    }

    private void OnDamageChanged(EntityUid uid, DamageableComponent comp, DamageChangedEvent args)
    {
        if (_player.LocalEntity != uid || HasComp<BodyComponent>(uid))
            return;

        if (!args.DamageIncreased ||
            args.DamageDelta == null ||
            args.DamageDelta.GetTotal() <= 0 ||
            args.Origin is not { } origin ||
            origin == uid ||
            !HasComp<ActorComponent>(origin))
            return;

        TriggerCombat();
    }

    private void OnHitscanStrike(Entity<InjurableComponent> ent, ref HitscanRaycastStrikeEvent args)
    {
        if (_player.LocalEntity != ent.Owner ||
            args.Data.Shooter is not { } shooter ||
            shooter == ent.Owner ||
            !HasComp<ActorComponent>(shooter))
            return;

        TriggerCombat();
    }

    private void OnGunShot(EntityUid uid, GunComponent comp, ref GunShotEvent args)
    {
        if (_player.LocalEntity is not { } player || !HasComp<ActorComponent>(args.User))
            return;

        if (player != args.User &&
            !Transform(player).Coordinates.InRange(EntityManager, Transform(args.User).Coordinates, NearbyCombatRange))
            return;

        if (_timing.CurTime < _nextCombatScan)
            return;
        _nextCombatScan = _timing.CurTime + CombatScanCooldown;

        if (!HasNearbyCombatants(player, NearbyCombatRange))
            return;

        TriggerCombat();
    }

    private void OnMeleeHit(EntityUid uid, MeleeWeaponComponent comp, MeleeHitEvent args)
    {
        if (_player.LocalEntity != args.User)
            return;

        var hitCombatant = false;
        foreach (var hit in args.HitEntities)
        {
            if (hit == args.User)
                continue;

            if (HasComp<HumanoidProfileComponent>(hit) ||
                (TryComp<MobStateComponent>(hit, out var mobState) && !_mobState.IsDead(hit, mobState) && !HasComp<ItemComponent>(hit)))
            {
                hitCombatant = true;
                break;
            }
        }

        if (hitCombatant)
        {
            TriggerCombat();
        }
    }

    private void OnMuzzleFlash(MuzzleFlashEvent args)
    {
        if (_player.LocalEntity is not { } player)
            return;

        var gun = GetEntity(args.Uid);
        if (!Exists(gun) ||
            !Transform(player).Coordinates.InRange(EntityManager, Transform(gun).Coordinates, NearbyCombatRange))
            return;

        if (_timing.CurTime < _nextCombatScan)
            return;
        _nextCombatScan = _timing.CurTime + CombatScanCooldown;

        if (!HasNearbyCombatants(player, NearbyCombatRange))
            return;

        TriggerCombat();
    }

    private bool HasNearbyCombatants(EntityUid player, float range)
    {
        var playerCoords = Transform(player).Coordinates;
        var mapId = playerCoords.GetMapId(EntityManager);
        if (mapId == MapId.Nullspace)
            return false;

        var enumerator = AllEntityQuery<HumanoidProfileComponent, TransformComponent>();
        while (enumerator.MoveNext(out var uid, out _, out var xform))
        {
            if (uid == player)
                continue;

            if (xform.MapID != mapId)
                continue;

            if (playerCoords.InRange(EntityManager, xform.Coordinates, range))
            {
                if (TryComp<MobStateComponent>(uid, out var mobState) && !_mobState.IsDead(uid, mobState))
                    return true;
            }
        }

        return false;
    }

    private void OnRoundEnd(RoundEndMessageEvent ev)
    {
        StopCombatMusic(immediate: true);
    }

    private void OnStateChanged(StateChangedEventArgs args)
    {
        if (args.NewState is not GameplayState)
        {
            StopCombatMusic(immediate: true);
        }
    }

    private void TriggerCombat()
    {
        if (!_combatEnabled)
            return;

        if (_state.CurrentState is not GameplayState)
            return;

        if (_player.LocalEntity is not { } player ||
            (TryComp<MobStateComponent>(player, out var mobState) && _mobState.IsIncapacitated(player, mobState)))
        {
            return;
        }

        _combatEndTime = _timing.CurTime + _combatDuration;

        if (!_inCombat || _combatStream == null || !Exists(_combatStream))
        {
            StartCombatMusic();
        }
    }

    private string? _lastPlayedTrack;

    private void StartCombatMusic()
    {
        var collectionId = ResolveCombatCollection();
        var track = GetPreparedCombatTrack(collectionId);
        if (track == null)
        {
            return;
        }

        _inCombat = true;
        _lastPlayedTrack = track;
        _contentAudio.DisableAmbientMusic();

        var volume = SharedAudioSystem.GainToVolume(_volumeGain);

        var stream = _audio.PlayGlobal(
            track,
            Filter.Local(),
            false,
            AudioParams.Default.WithVolume(volume));

        if (stream != null)
        {
            _combatStream = stream.Value.Entity;
            _contentAudio.FadeIn(_combatStream, stream.Value.Component, 3.5f);
        }
    }

    private void PrepareCombatTrack()
    {
        if (!_combatEnabled || _player.LocalEntity == null)
            return;

        GetPreparedCombatTrack(ResolveCombatCollection());
    }

    private string? GetPreparedCombatTrack(ProtoId<SoundCollectionPrototype> collectionId)
    {
        if (_preparedCollection == collectionId && _preparedTrack != null)
            return _preparedTrack;

        if (collectionId == ScavengerMusicCollection && _lastPlayedTrack == null)
        {
            const string defaultTrack = "/Audio/Ashfall/Ambience/Combat/Peril.ogg";
            if (_resourceCache.TryGetResource<AudioResource>(defaultTrack, out _))
            {
                _preparedCollection = collectionId;
                _preparedTrack = defaultTrack;
                return defaultTrack;
            }
        }

        if (!_proto.TryIndex<SoundCollectionPrototype>(collectionId, out var soundCollection) ||
            soundCollection.PickFiles.Count == 0)
        {
            return null;
        }

        var candidateFiles = new List<ResPath>(soundCollection.PickFiles);
        if (candidateFiles.Count > 1 && _lastPlayedTrack != null)
        {
            candidateFiles.RemoveAll(f => f.ToString() == _lastPlayedTrack);
        }

        _random.Shuffle(candidateFiles);
        string? track = null;
        foreach (var file in candidateFiles)
        {
            var path = file.ToString();
            if (_resourceCache.TryGetResource<AudioResource>(path, out _))
            {
                track = path;
                break;
            }
        }

        if (track == null)
            return null;

        _preparedCollection = collectionId;
        _preparedTrack = track;
        return track;
    }

    private ProtoId<SoundCollectionPrototype> ResolveCombatCollection()
    {
        if (_player.LocalEntity is not { } player)
            return ScavengerMusicCollection;

        // Check if player has ID with Security or Syndicate access (including inside PDA)
        EntityUid? cardUid = null;
        if (_idCard.TryFindIdCard(player, out var idCard))
        {
            cardUid = idCard.Owner;
        }
        else if (_inventory.TryGetSlotEntity(player, "id", out var idSlotEnt))
        {
            cardUid = idSlotEnt;
        }

        if (cardUid != null && TryComp<AccessComponent>(cardUid, out var access))
        {
            foreach (var tag in access.Tags)
            {
                if (tag.Id.Contains("Syndicate", StringComparison.OrdinalIgnoreCase))
                    return SyndicateMusicCollection;

                if (tag.Id.Contains("Security", StringComparison.OrdinalIgnoreCase))
                    return SecurityMusicCollection;
            }
        }

        return ScavengerMusicCollection;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_inCombat)
            return;

        // If player is incapacitated or dead, end combat music
        if (_player.LocalEntity is { } player &&
            TryComp<MobStateComponent>(player, out var mobState) &&
            _mobState.IsIncapacitated(player, mobState))
        {
            StopCombatMusic(immediate: false);
            return;
        }

        // Check if combat timeout has elapsed
        if (_timing.CurTime >= _combatEndTime)
        {
            StopCombatMusic(immediate: false);
            return;
        }

        // Check if song finished playing while combat is still going
        if (_combatStream == null || !TryComp<AudioComponent>(_combatStream, out var audioComp) || !audioComp.Playing)
        {
            _preparedTrack = null;
            StartCombatMusic();
        }
    }

    private void StopCombatMusic(bool immediate)
    {
        _inCombat = false;
        _combatEndTime = TimeSpan.Zero;
        _preparedTrack = null;
        _preparedCollection = null;

        if (_combatStream != null)
        {
            if (immediate)
            {
                _audio.Stop(_combatStream);
            }
            else
            {
                _contentAudio.FadeOut(_combatStream, duration: 2.5f);
            }
            _combatStream = null;
        }
    }
}
