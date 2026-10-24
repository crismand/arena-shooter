using AS.Core;
using TMPro;
using UnityEngine;

namespace AS.UI
{
    /// <summary>
    /// Shown on game over with the final score, wave and best score (or a new-record line).
    /// Restart is wired to GameManager in the scene.
    /// </summary>
    public class GameOverScreen : UIPanel
    {
        [SerializeField] TMP_Text summaryText;
        [SerializeField] Color recordColor = new(1f, 0.85f, 0.3f);

        int _score;
        int _wave;
        int _best;
        bool _newRecord;

        void OnEnable()
        {
            EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
            EventBus.Subscribe<ScoreChanged>(OnScoreChanged);
            EventBus.Subscribe<WaveStarted>(OnWaveStarted);
            EventBus.Subscribe<HighScoreChanged>(OnHighScoreChanged);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);
            EventBus.Unsubscribe<ScoreChanged>(OnScoreChanged);
            EventBus.Unsubscribe<WaveStarted>(OnWaveStarted);
            EventBus.Unsubscribe<HighScoreChanged>(OnHighScoreChanged);
        }

        void OnScoreChanged(ScoreChanged evt) => _score = evt.Score;
        void OnWaveStarted(WaveStarted evt) => _wave = evt.Number;

        void OnHighScoreChanged(HighScoreChanged evt)
        {
            _best = evt.HighScore;
            _newRecord = evt.IsNewRecord;
        }

        void OnGameStateChanged(GameStateChanged evt)
        {
            if (evt.Current != GameState.GameOver) return;
            string bestLine = _newRecord
                ? $"<color=#{ColorUtility.ToHtmlStringRGB(recordColor)}>NEW BEST!</color>"
                : $"Best {_best}";
            summaryText.text = $"Score {_score}\nReached wave {Mathf.Max(1, _wave)}\n{bestLine}";
            SetVisible(true);
        }
    }
}
