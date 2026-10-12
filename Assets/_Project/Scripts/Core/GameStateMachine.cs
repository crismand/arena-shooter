namespace AS.Core
{
    public enum GameState
    {
        Boot,
        Playing,
        Paused,
        GameOver,
    }

    /// <summary>Top-level game state. Every change is broadcast as a <see cref="GameStateChanged"/> event.</summary>
    public class GameStateMachine
    {
        public GameState Current { get; private set; } = GameState.Boot;

        public bool Is(GameState state) => Current == state;

        public void ChangeState(GameState next)
        {
            if (next == Current) return;

            var previous = Current;
            Current = next;
            EventBus.Publish(new GameStateChanged(previous, next));
        }
    }
}
