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
    private const float FadeDuration = 2.0f;

    private float _originalVolume = 0.5f;
    private float _currentMasterGain = 0.5f;
    private (EntityUid Entity, AudioComponent Component)? _tinnitusStream;

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
            ResetAudio();
    }

    private void OnPlayerDetached(EntityUid uid, DeafenedComponent component, LocalPlayerDetachedEvent args)
    {
        ResetAudio();
    }

    private void ResetAudio()
    {
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
            ResetAudio();
            _currentMasterGain = MathHelper.Lerp(_currentMasterGain, _originalVolume, MathF.Min(1f, 4.0f * frameTime));
            _audio.SetMasterGain(Math.Clamp(_currentMasterGain, 0f, _originalVolume));
            return;
        }

        if (_tinnitusStream == null)
        {
            _tinnitusStream = _audioSystem.PlayGlobal(
                new SoundPathSpecifier("/Audio/Ashfall/Effects/tinnitus_ring.ogg"),
                Filter.Local(),
                false,
                AudioParams.Default.WithVolume(SharedAudioSystem.GainToVolume(BaseTinnitusGain)).WithLoop(true));
            deaf.AudioStarted = _tinnitusStream != null;
        }

        float targetMasterVolume;
        if (timeLeft > FadeDuration)
        {
            targetMasterVolume = 0.05f * _originalVolume;
            if (_tinnitusStream != null)
            {
                _audioSystem.SetVolume(_tinnitusStream.Value.Entity, SharedAudioSystem.GainToVolume(BaseTinnitusGain), _tinnitusStream.Value.Component);
            }
        }
        else
        {
            var progress = Math.Clamp(timeLeft / FadeDuration, 0f, 1f);

            var tinnitusGain = BaseTinnitusGain * (progress * progress);
            var tinnitusVol = tinnitusGain > 0.005f ? SharedAudioSystem.GainToVolume(tinnitusGain) : float.NegativeInfinity;
            if (_tinnitusStream != null)
            {
                _audioSystem.SetVolume(_tinnitusStream.Value.Entity, tinnitusVol, _tinnitusStream.Value.Component);
            }

            var masterFactor = 1.0f - (progress * progress);
            targetMasterVolume = MathHelper.Lerp(0.05f * _originalVolume, _originalVolume, masterFactor);
        }

        _currentMasterGain = MathHelper.Lerp(_currentMasterGain, targetMasterVolume, MathF.Min(1f, 6.0f * frameTime));
        _audio.SetMasterGain(Math.Clamp(_currentMasterGain, 0f, _originalVolume));
    }
}
