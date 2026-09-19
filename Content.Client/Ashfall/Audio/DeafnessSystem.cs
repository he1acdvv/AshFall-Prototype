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

public sealed partial class DeafnessSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IAudioManager _audio = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAudioSystem _audioSystem = default!;

    private const float BaseTinnitusGain = 0.65f;
    private const float StartSoundDuration = 0.55f;
    private const float FadeDuration = 2.0f;

    private float _originalVolume = 0.5f;
    private float _currentMasterGain = 0.5f;

    private (EntityUid Entity, AudioComponent Component)? _startStream;
    private (EntityUid Entity, AudioComponent Component)? _loopStream;
    private (EntityUid Entity, AudioComponent Component)? _endStream;

    private double _startTime;
    private bool _inLoop;
    private bool _playedEnd;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DeafenedComponent, ComponentShutdown>(OnDeafShutdown);
        SubscribeLocalEvent<DeafenedComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);

        Subs.CVar(_cfg, CCVars.AudioMasterVolume, value =>
        {
            _originalVolume = value;
            _currentMasterGain = value;
        }, true);
    }

    private void OnDeafShutdown(EntityUid uid, DeafenedComponent component, ComponentShutdown args)
    {
        if (_player.LocalEntity == uid)
            ResetAudio(playOutro: true);
    }

    private void OnPlayerDetached(EntityUid uid, DeafenedComponent component, LocalPlayerDetachedEvent args)
    {
        ResetAudio(playOutro: false);
    }

    private void ResetAudio(bool playOutro)
    {
        StopSound(ref _startStream);
        StopSound(ref _loopStream);

        if (playOutro && !_playedEnd && _inLoop)
        {
            _playedEnd = true;
            _endStream = _audioSystem.PlayGlobal(
                new SoundPathSpecifier("/Audio/Ashfall/Effects/tinnitus_end.ogg"),
                Filter.Local(),
                false,
                AudioParams.Default.WithVolume(SharedAudioSystem.GainToVolume(BaseTinnitusGain * 0.9f)));
        }

        _inLoop = false;
        _startTime = 0;
    }

    private void StopSound(ref (EntityUid Entity, AudioComponent Component)? stream)
    {
        if (stream != null)
        {
            _audioSystem.Stop(stream.Value.Entity, stream.Value.Component);
            stream = null;
        }
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_player.LocalEntity is not { } player || !TryComp<DeafenedComponent>(player, out var deaf))
        {
            if (_inLoop || _startStream != null)
                ResetAudio(playOutro: true);

            // Smoothly restore master volume back to original
            if (MathF.Abs(_currentMasterGain - _originalVolume) > 0.005f)
            {
                _currentMasterGain = MathHelper.Lerp(_currentMasterGain, _originalVolume, MathF.Min(1f, 4.0f * frameTime));
                _audio.SetMasterGain(Math.Clamp(_currentMasterGain, 0f, _originalVolume));
            }
            return;
        }

        var curTime = _timing.CurTime;
        var timeLeft = (float)(deaf.EndTime - curTime).TotalSeconds;

        if (timeLeft <= 0)
        {
            ResetAudio(playOutro: true);
            _currentMasterGain = MathHelper.Lerp(_currentMasterGain, _originalVolume, MathF.Min(1f, 4.0f * frameTime));
            _audio.SetMasterGain(Math.Clamp(_currentMasterGain, 0f, _originalVolume));
            return;
        }

        // Phase 1: Start (intro attack)
        if (_startStream == null && !_inLoop)
        {
            _startTime = curTime.TotalSeconds;
            _playedEnd = false;
            _startStream = _audioSystem.PlayGlobal(
                new SoundPathSpecifier("/Audio/Ashfall/Effects/tinnitus_start.ogg"),
                Filter.Local(),
                false,
                AudioParams.Default.WithVolume(SharedAudioSystem.GainToVolume(BaseTinnitusGain)));
            deaf.AudioStarted = true;
        }

        // Transition from Start to Loop
        if (!_inLoop && _startTime > 0 && (curTime.TotalSeconds - _startTime) >= StartSoundDuration)
        {
            StopSound(ref _startStream);
            _inLoop = true;
            _loopStream = _audioSystem.PlayGlobal(
                new SoundPathSpecifier("/Audio/Ashfall/Effects/tinnitus_loop.ogg"),
                Filter.Local(),
                false,
                AudioParams.Default.WithVolume(SharedAudioSystem.GainToVolume(BaseTinnitusGain * 0.85f)).WithLoop(true));
        }

        // Phase 3: Transition to ending tail
        float targetMasterVolume;
        if (timeLeft > FadeDuration)
        {
            // Muffled state during peak tinnitus
            targetMasterVolume = 0.06f * _originalVolume;
            if (_loopStream == null && _inLoop)
            {
                // Deafness was extended after the outro played: restart the loop.
                _playedEnd = false;
                StopSound(ref _endStream);
                _loopStream = _audioSystem.PlayGlobal(
                    new SoundPathSpecifier("/Audio/Ashfall/Effects/tinnitus_loop.ogg"),
                    Filter.Local(),
                    false,
                    AudioParams.Default.WithVolume(SharedAudioSystem.GainToVolume(BaseTinnitusGain * 0.85f)).WithLoop(true));
            }
            else if (_loopStream != null)
            {
                _audioSystem.SetVolume(_loopStream.Value.Entity, SharedAudioSystem.GainToVolume(BaseTinnitusGain * 0.85f), _loopStream.Value.Component);
            }
        }
        else
        {
            // Time left is below FadeDuration: trigger end outro tail once and stop loop
            if (!_playedEnd && _inLoop)
            {
                StopSound(ref _loopStream);
                _playedEnd = true;
                _endStream = _audioSystem.PlayGlobal(
                    new SoundPathSpecifier("/Audio/Ashfall/Effects/tinnitus_end.ogg"),
                    Filter.Local(),
                    false,
                    AudioParams.Default.WithVolume(SharedAudioSystem.GainToVolume(BaseTinnitusGain * 0.85f)));
            }

            var progress = Math.Clamp(timeLeft / FadeDuration, 0f, 1f);
            var masterFactor = 1.0f - (progress * progress);
            targetMasterVolume = MathHelper.Lerp(0.06f * _originalVolume, _originalVolume, masterFactor);
        }

        // Smooth master volume interpolation
        _currentMasterGain = MathHelper.Lerp(_currentMasterGain, targetMasterVolume, MathF.Min(1f, 6.0f * frameTime));
        _audio.SetMasterGain(Math.Clamp(_currentMasterGain, 0f, _originalVolume));
    }
}
