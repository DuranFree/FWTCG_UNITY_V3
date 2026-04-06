using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace FWTCG.Audio
{
    /// <summary>
    /// Channel-based audio system. Each channel has its own AudioSource,
    /// priority level, and independent volume control.
    ///
    /// DOT-3: FadeRoutine/CrossFadeRoutine coroutines → DOVirtual.Float tweens.
    /// </summary>
    public class AudioTool : MonoBehaviour
    {
        public static AudioTool Instance { get; private set; }

        // ── Channel names ────────────────────────────────────────────────────
        public const string CH_BGM        = "bgm";
        public const string CH_UI         = "ui";
        public const string CH_CARD_SPAWN = "card_spawn";
        public const string CH_ATTACK     = "attack";
        public const string CH_DEATH      = "death";
        public const string CH_SPELL      = "spell";
        public const string CH_AMBIENT    = "ambient";
        public const string CH_SCORE      = "score";
        public const string CH_LEGEND     = "legend";
        public const string CH_DUEL       = "duel";
        public const string CH_SYSTEM     = "system";

        // ── Priority tiers ───────────────────────────────────────────────────
        public const int PRI_AMBIENT = 10;
        public const int PRI_UI      = 20;
        public const int PRI_CARD    = 40;
        public const int PRI_COMBAT  = 60;
        public const int PRI_SPELL   = 80;
        public const int PRI_SYSTEM  = 100;

        // ── Default channel configs ──────────────────────────────────────────
        private static readonly ChannelConfig[] DefaultChannels = new[]
        {
            new ChannelConfig(CH_BGM,        PRI_AMBIENT, 0.4f, true),
            new ChannelConfig(CH_AMBIENT,    PRI_AMBIENT, 0.3f, true),
            new ChannelConfig(CH_UI,         PRI_UI,      0.7f, false),
            new ChannelConfig(CH_CARD_SPAWN, PRI_CARD,    0.7f, false),
            new ChannelConfig(CH_SCORE,      PRI_CARD,    0.7f, false),
            new ChannelConfig(CH_ATTACK,     PRI_COMBAT,  0.8f, false),
            new ChannelConfig(CH_DEATH,      PRI_COMBAT,  0.8f, false),
            new ChannelConfig(CH_SPELL,      PRI_SPELL,   0.8f, false),
            new ChannelConfig(CH_LEGEND,     PRI_SPELL,   0.8f, false),
            new ChannelConfig(CH_DUEL,       PRI_SPELL,   0.8f, false),
            new ChannelConfig(CH_SYSTEM,     PRI_SYSTEM,  1.0f, false),
        };

        // ── Internal types ───────────────────────────────────────────────────
        private struct ChannelConfig
        {
            public string Name;
            public int    DefaultPriority;
            public float  DefaultVolume;
            public bool   Loop;
            public ChannelConfig(string name, int pri, float vol, bool loop)
            {
                Name = name; DefaultPriority = pri; DefaultVolume = vol; Loop = loop;
            }
        }

        public class AudioChannel
        {
            public string      Name;
            public AudioSource Source;
            public int         DefaultPriority;
            public int         CurrentPriority;
            public float       BaseVolume;     // per-channel volume (0-1)
            public Tween       FadeTween;      // DOT-3: was Coroutine FadeRoutine
        }

        // ── State ────────────────────────────────────────────────────────────
        private readonly Dictionary<string, AudioChannel> _channels = new Dictionary<string, AudioChannel>();
        private float _masterVolume = 1f;

        public int ChannelCount => _channels.Count;

        // ── Lifecycle ────────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            InitChannels();
        }

        private void OnDestroy()
        {
            // Kill all channel fade tweens
            foreach (var ch in _channels.Values)
                CancelFade(ch);
            if (Instance == this) Instance = null;
        }

        private void InitChannels()
        {
            foreach (var cfg in DefaultChannels)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = cfg.Loop;
                src.volume = cfg.DefaultVolume;

                var ch = new AudioChannel
                {
                    Name            = cfg.Name,
                    Source          = src,
                    DefaultPriority = cfg.DefaultPriority,
                    CurrentPriority = 0,
                    BaseVolume      = cfg.DefaultVolume,
                };
                _channels[cfg.Name] = ch;
            }
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Play a clip on the named channel. Uses the channel's default priority.
        /// </summary>
        public void Play(string channelName, AudioClip clip)
        {
            if (clip == null) return;
            if (!_channels.TryGetValue(channelName, out var ch)) return;
            PlayOnChannel(ch, clip, ch.DefaultPriority);
        }

        /// <summary>
        /// Play a clip on the named channel with explicit priority.
        /// Higher or equal priority interrupts current playback on the same channel.
        /// </summary>
        public void Play(string channelName, AudioClip clip, int priority)
        {
            if (clip == null) return;
            if (!_channels.TryGetValue(channelName, out var ch)) return;
            PlayOnChannel(ch, clip, priority);
        }

        /// <summary>
        /// Play a clip as one-shot on the named channel (overlapping allowed).
        /// </summary>
        public void PlayOneShot(string channelName, AudioClip clip)
        {
            if (clip == null) return;
            if (!_channels.TryGetValue(channelName, out var ch)) return;
            ch.Source.PlayOneShot(clip, ch.BaseVolume * _masterVolume);
        }

        public void StopChannel(string channelName)
        {
            if (!_channels.TryGetValue(channelName, out var ch)) return;
            CancelFade(ch);
            ch.Source.Stop();
            ch.CurrentPriority = 0;
        }

        public void StopAll()
        {
            foreach (var ch in _channels.Values)
            {
                CancelFade(ch);
                ch.Source.Stop();
                ch.CurrentPriority = 0;
            }
        }

        // ── Fade ─────────────────────────────────────────────────────────────

        /// <summary>Fade channel volume from 0 to baseVolume over duration.</summary>
        public void FadeIn(string channelName, float duration)
        {
            if (!_channels.TryGetValue(channelName, out var ch)) return;
            CancelFade(ch);
            StartFade(ch, 0f, ch.BaseVolume, duration, false);
        }

        /// <summary>Fade channel volume from current to 0 over duration, then stop.</summary>
        public void FadeOut(string channelName, float duration)
        {
            if (!_channels.TryGetValue(channelName, out var ch)) return;
            CancelFade(ch);
            StartFade(ch, ch.Source.volume, 0f, duration, true);
        }

        /// <summary>Cross-fade: fade out current clip, swap to new clip, fade in.</summary>
        public void CrossFade(string channelName, AudioClip newClip, float duration)
        {
            if (newClip == null) return;
            if (!_channels.TryGetValue(channelName, out var ch)) return;
            CancelFade(ch);
            StartCrossFade(ch, newClip, duration);
        }

        // ── Volume control ───────────────────────────────────────────────────

        public float MasterVolume
        {
            get => _masterVolume;
            set
            {
                _masterVolume = Mathf.Clamp01(value);
                ApplyVolumes();
            }
        }

        public void SetChannelVolume(string channelName, float volume)
        {
            if (!_channels.TryGetValue(channelName, out var ch)) return;
            ch.BaseVolume = Mathf.Clamp01(volume);
            ch.Source.volume = ch.BaseVolume * _masterVolume;
        }

        public float GetChannelVolume(string channelName)
        {
            if (!_channels.TryGetValue(channelName, out var ch)) return 0f;
            return ch.BaseVolume;
        }

        /// <summary>Get a channel by name (for testing/inspection).</summary>
        public AudioChannel GetChannel(string channelName)
        {
            _channels.TryGetValue(channelName, out var ch);
            return ch;
        }

        // ── Internal ─────────────────────────────────────────────────────────

        private void PlayOnChannel(AudioChannel ch, AudioClip clip, int priority)
        {
            // If channel is playing, only interrupt if priority >= current
            if (ch.Source.isPlaying && priority < ch.CurrentPriority)
                return;

            CancelFade(ch);
            ch.Source.clip = clip;
            ch.Source.volume = ch.BaseVolume * _masterVolume;
            ch.Source.Play();
            ch.CurrentPriority = priority;
        }

        private void CancelFade(AudioChannel ch)
        {
            if (ch.FadeTween != null)
            {
                if (ch.FadeTween.IsActive()) ch.FadeTween.Kill();
                ch.FadeTween = null;
            }
        }

        private void ApplyVolumes()
        {
            foreach (var ch in _channels.Values)
            {
                // Only update if not mid-fade
                if (ch.FadeTween == null)
                    ch.Source.volume = ch.BaseVolume * _masterVolume;
            }
        }

        private void StartFade(AudioChannel ch, float from, float to, float duration, bool stopAfter)
        {
            if (duration <= 0f)
            {
                ch.Source.volume = to * _masterVolume;
                if (stopAfter) { ch.Source.Stop(); ch.CurrentPriority = 0; }
                ch.FadeTween = null;
                return;
            }

            ch.FadeTween = DOVirtual.Float(from, to, duration, vol =>
            {
                ch.Source.volume = vol * _masterVolume;
            })
            .SetEase(Ease.Linear)
            .SetUpdate(true)
            .SetTarget(gameObject)
            .OnComplete(() =>
            {
                ch.Source.volume = to * _masterVolume;
                if (stopAfter) { ch.Source.Stop(); ch.CurrentPriority = 0; }
                ch.FadeTween = null;
            });
        }

        private void StartCrossFade(AudioChannel ch, AudioClip newClip, float duration)
        {
            float halfDur   = duration * 0.5f;
            float startVol  = ch.Source.volume;
            float targetVol = ch.BaseVolume * _masterVolume;

            var seq = DOTween.Sequence()
                .SetTarget(gameObject)
                .SetUpdate(true);

            // Phase 1: fade out current
            seq.Append(DOVirtual.Float(startVol, 0f, halfDur, v =>
            {
                ch.Source.volume = v;
            }).SetEase(Ease.Linear));

            // Swap clip at midpoint
            seq.AppendCallback(() =>
            {
                ch.Source.Stop();
                ch.Source.clip = newClip;
                ch.Source.volume = 0f;
                ch.Source.Play();
            });

            // Phase 2: fade in new
            seq.Append(DOVirtual.Float(0f, targetVol, halfDur, v =>
            {
                ch.Source.volume = v;
            }).SetEase(Ease.Linear));

            seq.OnComplete(() =>
            {
                ch.Source.volume = targetVol;
                ch.FadeTween = null;
            });

            ch.FadeTween = seq;
        }
    }
}
