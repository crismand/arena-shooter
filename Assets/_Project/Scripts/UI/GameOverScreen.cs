using AS.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AS.UI
{
    /// <summary>Shown on game over with the final score and wave. Restart is wired to GameManager in the scene.</summary>
    public class GameOverScreen : UIPanel
    {
        [SerializeField] Text summaryText;

        int _score;
        int _wave;

        void OnEnable()
        {
            EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
            EventBus.Subscribe<ScoreChanged>(OnScoreChanged);
            EventBus.Subscribe<WaveStarted>(OnWaveStarted);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);
            EventBus.Unsubscribe<ScoreChanged>(OnScoreChanged);
            EventBus.Unsubscribe<WaveStarted>(OnWaveStarted);
        }

        void OnScoreChanged(ScoreChanged evt) => _score = evt.Score;
        void OnWaveStarted(WaveStarted evt) => _wave = evt.Number;

        void OnGameStateChanged(GameStateChanged evt)
        {
            if (evt.Current != GameState.GameOver) return;
            summaryText.text = $"Score {_score}\nReached wave {Mathf.Max(1, _wave)}";
            SetVisible(true);
        }
    }
}
