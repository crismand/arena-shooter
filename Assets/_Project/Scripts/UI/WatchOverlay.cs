using AS.Core;

namespace AS.UI
{
    /// <summary>Visible exactly while watching the AI play: says so, and how to get back to the title.</summary>
    public class WatchOverlay : UIPanel
    {
        void OnEnable() => EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
        void OnDisable() => EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);

        void OnGameStateChanged(GameStateChanged evt) => SetVisible(evt.Current == GameState.Watching);
    }
}
