using AS.Core;

namespace AS.UI
{
    /// <summary>Visible exactly while the game is paused. Buttons are wired to GameManager in the scene.</summary>
    public class PauseMenu : UIPanel
    {
        void OnEnable() => EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
        void OnDisable() => EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);

        void OnGameStateChanged(GameStateChanged evt) => SetVisible(evt.Current == GameState.Paused);
    }
}
