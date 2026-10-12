using AS.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AS.UI
{
    /// <summary>
    /// In-game overlay: health bar, score, wave number and a centre banner on wave start/clear.
    /// Listens to events only and never references gameplay objects.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        [Tooltip("Image with Image Type = Filled.")]
        [SerializeField] Image healthFill;
        [SerializeField] Text scoreText;
        [SerializeField] Text waveText;
        [SerializeField] Text bannerText;
        [SerializeField, Min(0.1f)] float bannerDuration = 1.6f;
        [Tooltip("Seconds at the end of the banner's time spent fading out.")]
        [SerializeField, Min(0f)] float bannerFade = 0.4f;

        float _bannerEndTime;

        void Awake() => bannerText.enabled = false;

        void OnEnable()
        {
            EventBus.Subscribe<PlayerHealthChanged>(OnHealthChanged);
            EventBus.Subscribe<ScoreChanged>(OnScoreChanged);
            EventBus.Subscribe<WaveStarted>(OnWaveStarted);
            EventBus.Subscribe<WaveCleared>(OnWaveCleared);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<PlayerHealthChanged>(OnHealthChanged);
            EventBus.Unsubscribe<ScoreChanged>(OnScoreChanged);
            EventBus.Unsubscribe<WaveStarted>(OnWaveStarted);
            EventBus.Unsubscribe<WaveCleared>(OnWaveCleared);
        }

        void Update()
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

        void OnScoreChanged(ScoreChanged evt) => scoreText.text = $"SCORE {evt.Score}";

        void OnWaveStarted(WaveStarted evt)
        {
            waveText.text = $"WAVE {evt.Number}";
            ShowBanner($"WAVE {evt.Number}");
        }

        void OnWaveCleared(WaveCleared evt) => ShowBanner("WAVE CLEARED");

        void ShowBanner(string message)
        {
            bannerText.text = message;
            bannerText.enabled = true;
            _bannerEndTime = Time.time + bannerDuration;
        }
    }
}
