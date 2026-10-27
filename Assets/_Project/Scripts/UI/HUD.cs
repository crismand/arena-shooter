using System.Collections.Generic;
using System.Text;
using AS.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AS.UI
{
    /// <summary>
    /// In-game overlay: health bar, active power-ups, score, best score, wave number and a centre
    /// banner on wave start/clear. The best score climbs live once this run passes it.
    /// Listens to events only and never references gameplay objects.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        [Tooltip("Image with Image Type = Filled.")]
        [SerializeField] Image healthFill;
        [SerializeField] TMP_Text scoreText;
        [SerializeField] TMP_Text bestText;
        [SerializeField] TMP_Text waveText;
        [SerializeField] TMP_Text bannerText;
        [Tooltip("Lists active power-ups with seconds remaining.")]
        [SerializeField] TMP_Text powerUpText;
        [SerializeField, Min(0.1f)] float bannerDuration = 1.6f;
        [Tooltip("Seconds at the end of the banner's time spent fading out.")]
        [SerializeField, Min(0f)] float bannerFade = 0.4f;

        float _bannerEndTime;
        int _score;
        int _best;
        // Label → time it runs out. Re-collecting the same power-up just moves its end time.
        readonly Dictionary<string, float> _powerUpEnds = new();
        readonly List<string> _expired = new();
        readonly StringBuilder _powerUpLine = new();

        void Awake()
        {
            bannerText.enabled = false;
            powerUpText.text = "";
        }

        void OnEnable()
        {
            EventBus.Subscribe<PlayerHealthChanged>(OnHealthChanged);
            EventBus.Subscribe<ScoreChanged>(OnScoreChanged);
            EventBus.Subscribe<HighScoreChanged>(OnHighScoreChanged);
            EventBus.Subscribe<WaveStarted>(OnWaveStarted);
            EventBus.Subscribe<WaveCleared>(OnWaveCleared);
            EventBus.Subscribe<PowerUpActivated>(OnPowerUpActivated);
            EventBus.Subscribe<ArenaReset>(OnArenaReset);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<PlayerHealthChanged>(OnHealthChanged);
            EventBus.Unsubscribe<ScoreChanged>(OnScoreChanged);
            EventBus.Unsubscribe<HighScoreChanged>(OnHighScoreChanged);
            EventBus.Unsubscribe<WaveStarted>(OnWaveStarted);
            EventBus.Unsubscribe<WaveCleared>(OnWaveCleared);
            EventBus.Unsubscribe<PowerUpActivated>(OnPowerUpActivated);
            EventBus.Unsubscribe<ArenaReset>(OnArenaReset);
        }

        void Update()
        {
            UpdatePowerUps();
            UpdateBanner();
        }

        void UpdatePowerUps()
        {
            if (_powerUpEnds.Count == 0) return;

            _powerUpLine.Clear();
            _expired.Clear();
            foreach (var (label, endTime) in _powerUpEnds)
            {
                float remaining = endTime - Time.time;
                if (remaining <= 0f)
                {
                    _expired.Add(label);
                    continue;
                }
                if (_powerUpLine.Length > 0) _powerUpLine.Append("   ");
                _powerUpLine.Append(label).Append(' ').Append(Mathf.CeilToInt(remaining)).Append('s');
            }
            foreach (var label in _expired) _powerUpEnds.Remove(label);
            powerUpText.text = _powerUpLine.ToString();
        }

        void UpdateBanner()
        {
            if (!bannerText.enabled) return;
            float remaining = _bannerEndTime - Time.time;
            if (remaining <= 0f)
            {
                bannerText.enabled = false;
                return;
            }
            var color = bannerText.color;
            color.a = bannerFade > 0f ? Mathf.Clamp01(remaining / bannerFade) : 1f;
            bannerText.color = color;
        }

        void OnHealthChanged(PlayerHealthChanged evt) =>
            healthFill.fillAmount = evt.Max > 0f ? evt.Current / evt.Max : 0f;

        void OnScoreChanged(ScoreChanged evt)
        {
            _score = evt.Score;
            scoreText.text = $"SCORE {evt.Score}";
            RefreshBest();
        }

        void OnHighScoreChanged(HighScoreChanged evt)
        {
            _best = evt.HighScore;
            RefreshBest();
        }

        void RefreshBest() => bestText.text = $"BEST {Mathf.Max(_best, _score)}";

        void OnWaveStarted(WaveStarted evt)
        {
            waveText.text = $"WAVE {evt.Number}";
            ShowBanner($"WAVE {evt.Number}");
        }

        void OnWaveCleared(WaveCleared evt) => ShowBanner("WAVE CLEARED");

        void OnPowerUpActivated(PowerUpActivated evt) => _powerUpEnds[evt.Label] = Time.time + evt.Duration;

        // Back to how the HUD looks at the start of a run; health and score arrive as their own events.
        void OnArenaReset(ArenaReset _)
        {
            _powerUpEnds.Clear();
            powerUpText.text = "";
            waveText.text = "";
            bannerText.enabled = false;
        }

        void ShowBanner(string message)
        {
            bannerText.text = message;
            bannerText.enabled = true;
            _bannerEndTime = Time.time + bannerDuration;
        }
    }
}
