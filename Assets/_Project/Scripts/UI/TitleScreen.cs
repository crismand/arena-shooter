using AS.Core;
using TMPro;
using UnityEngine;

namespace AS.UI
{
    /// <summary>
    /// Visible exactly while on the title screen, with the best score so far. Play, Settings and
    /// Quit are wired in the scene.
    /// </summary>
    public class TitleScreen : UIPanel
    {
        [SerializeField] TMP_Text bestText;

        void OnEnable()
        {
            EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
            EventBus.Subscribe<HighScoreChanged>(OnHighScoreChanged);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);
            EventBus.Unsubscribe<HighScoreChanged>(OnHighScoreChanged);
        }

        void OnGameStateChanged(GameStateChanged evt) => SetVisible(evt.Current == GameState.Title);

        void OnHighScoreChanged(HighScoreChanged evt) =>
            bestText.text = evt.HighScore > 0 ? $"BEST {evt.HighScore}" : "";
    }
}
