using System.Collections.Generic;
using AS.Core;
using UnityEngine;
using UnityEngine.Audio;

namespace AS.Feedback
{
    /// <summary>
    /// Plays a sound for each gameplay event on a small set of 2D voices (AudioSources) routed to
    /// one mixer group. When every voice is busy, the next one in rotation (the oldest sound) is cut off.
    /// </summary>
    public class SoundEffects : MonoBehaviour
    {
        [SerializeField] AudioMixerGroup output;
        [Tooltip("How many sounds can overlap.")]
        [SerializeField, Min(1)] int voiceCount = 12;

        [Header("Cues")]
        [SerializeField] SoundCue playerShot = new();
        [SerializeField] SoundCue enemyShot = new();
        [SerializeField] SoundCue enemyHit = new();
        [SerializeField] SoundCue enemyDeath = new();
        [SerializeField] SoundCue playerHurt = new();
        [SerializeField] SoundCue playerDeath = new();
        [SerializeField] SoundCue pickup = new();
        [SerializeField] SoundCue waveStarted = new();
        [SerializeField] SoundCue waveCleared = new();

        AudioSource[] _voices;
        int _nextVoice;
        readonly Dictionary<SoundCue, float> _lastPlayed = new();

        /// <summary>The clip most recently started, for tests and debugging.</summary>
        public AudioClip LastClip { get; private set; }

        void Awake()
        {
            _voices = new AudioSource[voiceCount];
            for (int i = 0; i < voiceCount; i++)
            {
                var source = new GameObject($"Voice_{i}").AddComponent<AudioSource>();
                source.transform.SetParent(transform, false);
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.outputAudioMixerGroup = output;
                _voices[i] = source;
            }
        }

        void OnEnable()
        {
            EventBus.Subscribe<ShotFired>(OnShotFired);
            EventBus.Subscribe<EnemyDamaged>(OnEnemyDamaged);
            EventBus.Subscribe<EnemyKilled>(OnEnemyKilled);
            EventBus.Subscribe<PlayerDamaged>(OnPlayerDamaged);
            EventBus.Subscribe<PlayerDied>(OnPlayerDied);
            EventBus.Subscribe<PickupCollected>(OnPickupCollected);
            EventBus.Subscribe<WaveStarted>(OnWaveStarted);
            EventBus.Subscribe<WaveCleared>(OnWaveCleared);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<ShotFired>(OnShotFired);
            EventBus.Unsubscribe<EnemyDamaged>(OnEnemyDamaged);
            EventBus.Unsubscribe<EnemyKilled>(OnEnemyKilled);
            EventBus.Unsubscribe<PlayerDamaged>(OnPlayerDamaged);
            EventBus.Unsubscribe<PlayerDied>(OnPlayerDied);
            EventBus.Unsubscribe<PickupCollected>(OnPickupCollected);
            EventBus.Unsubscribe<WaveStarted>(OnWaveStarted);
            EventBus.Unsubscribe<WaveCleared>(OnWaveCleared);
        }

        void OnShotFired(ShotFired evt) => Play(evt.ByPlayer ? playerShot : enemyShot);
        void OnEnemyDamaged(EnemyDamaged _) => Play(enemyHit);
        void OnEnemyKilled(EnemyKilled _) => Play(enemyDeath);
        void OnPlayerDamaged(PlayerDamaged _) => Play(playerHurt);
        void OnPlayerDied(PlayerDied _) => Play(playerDeath);
        void OnPickupCollected(PickupCollected _) => Play(pickup);
        void OnWaveStarted(WaveStarted _) => Play(waveStarted);
        void OnWaveCleared(WaveCleared _) => Play(waveCleared);

        public void Play(SoundCue cue)
        {
            if (cue == null || cue.Clip == null) return;
            // Unscaled, so a hit-stop freeze doesn't hold sounds back.
            float now = Time.unscaledTime;
            if (_lastPlayed.TryGetValue(cue, out float last) && now - last < cue.MinInterval) return;
            _lastPlayed[cue] = now;

            var voice = NextVoice();
            voice.clip = cue.Clip;
            voice.volume = cue.Volume;
            voice.pitch = 1f + Random.Range(-cue.PitchJitter, cue.PitchJitter);
            voice.Play();
            LastClip = cue.Clip;
        }

        /// <summary>A free voice if there is one, otherwise the next in rotation.</summary>
        AudioSource NextVoice()
        {
            for (int i = 0; i < _voices.Length; i++)
            {
                int index = (_nextVoice + i) % _voices.Length;
                if (_voices[index].isPlaying) continue;
                _nextVoice = (index + 1) % _voices.Length;
                return _voices[index];
            }
            var voice = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;
            return voice;
        }
    }
}
