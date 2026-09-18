// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Ashfall.Audio;
using Content.Shared.CCVar;
using Robust.Client.Audio;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client.Ashfall.Audio;

public sealed partial class DeafnessSystem : SharedDeafnessSystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IAudioManager _audio = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAudioSystem _audioSystem = default!;

    private float _originalVolume = 0.5f;
    private (EntityUid Entity, AudioComponent Component)? _tinnitusStream;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DeafenedComponent, ComponentShutdown>(OnDeafShutdown);
        SubscribeLocalEvent<DeafenedComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);

        Subs.CVar(_cfg, CCVars.AudioMasterVolume, value => _originalVolume = value, true);
    }

    private void OnDeafShutdown(EntityUid uid, DeafenedComponent component, ComponentShutdown args)
    {
        if (_player.LocalEntity == uid)
            ResetAudio();
    }

    private void OnPlayerDetached(EntityUid uid, DeafenedComponent component, LocalPlayerDetachedEvent args)
    {
        ResetAudio();
    }

    private void ResetAudio()
    {
        _audio.SetMasterGain(_originalVolume);
        if (_tinnitusStream != null)
        {
            _audioSystem.Stop(_tinnitusStream.Value.Entity, _tinnitusStream.Value.Component);
            _tinnitusStream = null;
        }
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_player.LocalEntity is not { } player || !TryComp<DeafenedComponent>(player, out var deaf))
        {
            if (_tinnitusStream != null)
                ResetAudio();
            return;
        }

        var curTime = _timing.CurTime;
        var timeLeft = (float)(deaf.EndTime - curTime).TotalSeconds;

        if (timeLeft <= 0)
        {
            ResetAudio();
            return;
        }

        if (_tinnitusStream == null)
        {
            _tinnitusStream = _audioSystem.PlayPredicted(new SoundPathSpecifier("/Audio/Effects/tinnitus.ogg", AudioParams.Default.WithVolume(2f).WithLoop(true)), player, player);
            deaf.AudioStarted = _tinnitusStream != null;
        }

        // Dampen audio heavily when deafened; smoothly fade back in during the final 1.5 seconds
        float targetVolume;
        if (timeLeft > 1.5f)
        {
            targetVolume = 0.05f * _originalVolume;
        }
        else
        {
            var fraction = 1.0f - (timeLeft / 1.5f);
            targetVolume = MathHelper.Lerp(0.05f * _originalVolume, _originalVolume, fraction);
        }

        _audio.SetMasterGain(Math.Clamp(targetVolume, 0f, _originalVolume));
    }
}
