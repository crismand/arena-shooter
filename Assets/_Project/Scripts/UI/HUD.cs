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
    /// banner on wave start/clear, the name of each pickup as it's collected, and a full-screen flash
    /// when a bomb goes off. The best score climbs
    /// live once this run passes it.
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
        [Tooltip("Shown while a boss is alive. Optional.")]
        [SerializeField] GameObject bossBar;
        [Tooltip("Image with Image Type = Filled.")]
        [SerializeField] Image bossFill;
        [SerializeField] Color bossBannerColor = new(1f, 0.3f, 0.3f);
        [SerializeField, Min(0.1f)] float bossBannerDuration = 2.5f;
        [Tooltip("Names each pickup as it's collected, in its colour. Optional.")]
        [SerializeField] TMP_Text pickupText;
        [SerializeField, Min(0.1f)] float pickupTextDuration = 1.2f;
        [SerializeField, Min(0f)] float pickupTextFade = 0.4f;
        [Tooltip("Full-screen image flashed when a bomb goes off. Optional.")]
        [SerializeField] Image flash;
        [SerializeField, Range(0f, 1f)] float flashAlpha = 0.55f;
        [SerializeField, Min(0.01f)] float flashFade = 0.35f;

        float _bannerEndTime;
        Color _bannerColor;
        float _flashEndTime;
        float _pickupTextEndTime;
        int _score;
        int _best;
        // Label → time it runs out. Re-collecting the same power-up just moves its end time.
        readonly Dictionary<string, float> _powerUpEnds = new();
        readonly List<string> _expired = new();
        readonly StringBuilder _powerUpLine = new();

        void Awake()
        {
            bannerText.enabled = false;
            _bannerColor = bannerText.color;
            powerUpText.text = "";
            if (bossBar != null) bossBar.SetActive(false);
            if (flash != null) flash.enabled = false;
            if (pickupText != null) pickupText.enabled = false;
        }

        void OnEnable()
        {
            EventBus.Subscribe<PlayerHealthChanged>(OnHealthChanged);
            EventBus.Subscribe<ScoreChanged>(OnScoreChanged);
            EventBus.Subscribe<HighScoreChanged>(OnHighScoreChanged);
            EventBus.Subscribe<WaveStarted>(OnWaveStarted);
            EventBus.Subscribe<WaveCleared>(OnWaveCleared);
            EventBus.Subscribe<PowerUpActivated>(OnPowerUpActivated);
            EventBus.Subscribe<BombDetonated>(OnBombDetonated);
            EventBus.Subscribe<PickupCollected>(OnPickupCollected);
            EventBus.Subscribe<BossHealthChanged>(OnBossHealthChanged);
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
            EventBus.Unsubscribe<BombDetonated>(OnBombDetonated);
            EventBus.Unsubscribe<PickupCollected>(OnPickupCollected);
            EventBus.Unsubscribe<BossHealthChanged>(OnBossHealthChanged);
            EventBus.Unsubscribe<ArenaReset>(OnArenaReset);
        }

        void Update()
        {
            UpdatePowerUps();
            UpdateBanner();
            UpdateFlash();
            UpdatePickupText();
        }

        void UpdatePickupText()
        {
            if (pickupText == null || !pickupText.enabled) return;
            float remaining = _pickupTextEndTime - Time.time;
            if (remaining <= 0f)
            {
                pickupText.enabled = false;
                return;
            }
            var color = pickupText.color;
            color.a = pickupTextFade > 0f ? Mathf.Clamp01(remaining / pickupTextFade) : 1f;
            pickupText.color = color;
        }

        void OnPickupCollected(PickupCollected evt)
        {
            if (pickupText == null || string.IsNullOrEmpty(evt.Label)) return;
            pickupText.text = evt.Label;
            pickupText.color = new Color(evt.Color.r, evt.Color.g, evt.Color.b, 1f);
            pickupText.enabled = true;
            _pickupTextEndTime = Time.time + pickupTextDuration;
        }

        // Unscaled, so hit-stop from the bomb's kills doesn't hold the flash on screen.
        void UpdateFlash()
        {
            if (flash == null || !flash.enabled) return;
            float remaining = _flashEndTime - Time.unscaledTime;
            if (remaining <= 0f)
            {
                flash.enabled = false;
                return;
            }
            var color = flash.color;
            color.a = flashAlpha * remaining / flashFade;
            flash.color = color;
        }

        void OnBombDetonated(BombDetonated _)
        {
            if (flash == null) return;
            _flashEndTime = Time.unscaledTime + flashFade;
            flash.enabled = true;
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
            if (evt.IsBoss) ShowBanner("WARNING: BOSS", bossBannerColor, bossBannerDuration);
            else ShowBanner($"WAVE {evt.Number}");
        }

        void OnBossHealthChanged(BossHealthChanged evt)
        {
            if (bossBar == null) return;
            bossBar.SetActive(evt.Current > 0f);
            if (bossFill != null) bossFill.fillAmount = evt.Max > 0f ? evt.Current / evt.Max : 0f;
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
            if (pickupText != null) pickupText.enabled = false;
            if (bossBar != null) bossBar.SetActive(false);
        }

        void ShowBanner(string message) => ShowBanner(message, _bannerColor, bannerDuration);

        void ShowBanner(string message, Color color, float duration)
        {
            bannerText.text = message;
            bannerText.color = color;
            bannerText.enabled = true;
            _bannerEndTime = Time.time + duration;
        }
    }
}
