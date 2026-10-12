using UnityEngine;
using UnityEngine.SceneManagement;

namespace AS.Core
{
    /// <summary>
    /// Owns the run: game state, input, pausing, score and restart. Lives in the Arena scene;
    /// a restart simply reloads the scene, so nothing persists between runs.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class GameManager : MonoBehaviour
    {
        [SerializeField] InputReader input;

        public GameStateMachine State { get; } = new();
        public int Score { get; private set; }

        void OnEnable()
        {
            input.Enable();
            input.PausePressed += TogglePause;
            EventBus.Subscribe<PlayerDied>(OnPlayerDied);
            EventBus.Subscribe<EnemyKilled>(OnEnemyKilled);
        }

        void OnDisable()
        {
            input.PausePressed -= TogglePause;
            input.Disable();
            EventBus.Unsubscribe<PlayerDied>(OnPlayerDied);
            EventBus.Unsubscribe<EnemyKilled>(OnEnemyKilled);
        }

        void Start()
        {
            Time.timeScale = 1f;
            EventBus.Publish(new ScoreChanged(Score));
            State.ChangeState(GameState.Playing);
        }

        public void TogglePause()
        {
            if (State.Is(GameState.Playing)) SetPaused(true);
            else if (State.Is(GameState.Paused)) SetPaused(false);
        }

        public void SetPaused(bool paused)
        {
            if (State.Is(GameState.GameOver)) return;
            Time.timeScale = paused ? 0f : 1f;
            input.GameplayEnabled = !paused;
            State.ChangeState(paused ? GameState.Paused : GameState.Playing);
        }

        /// <summary>For UI buttons.</summary>
        public void Resume() => SetPaused(false);

        public void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        void OnPlayerDied(PlayerDied _)
        {
            input.GameplayEnabled = false;
            State.ChangeState(GameState.GameOver);
        }

        void OnEnemyKilled(EnemyKilled evt)
        {
            if (!State.Is(GameState.Playing)) return;
            Score += evt.ScoreValue;
            EventBus.Publish(new ScoreChanged(Score));
        }
    }
}
