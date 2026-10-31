using UnityEngine;
using UnityEngine.SceneManagement;

namespace AS.Core
{
    /// <summary>
    /// Owns the run: game state, input, pausing, score, high score and restart. Lives in the Arena
    /// scene. The game opens on the title screen with the arena frozen behind it; Play starts a run,
    /// and Watch AI hands the arena to the trained agent until any button is pressed. A restart
    /// resets the <see cref="arena"/> in place instead of reloading the scene.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class GameManager : MonoBehaviour
    {
        [SerializeField] InputReader input;
        [Tooltip("The arena on screen. Restart resets it in place; without one, Restart reloads the scene.")]
        [SerializeField] Arena arena;
        [Tooltip("Open on the title screen. Off starts a run straight away.")]
        [SerializeField] bool showTitle = true;
        [Tooltip("Seconds after Watch AI before a button press returns to the title, so the press that chose it can't.")]
        [SerializeField, Min(0f)] float watchExitDelay = 0.5f;

        public GameStateMachine State { get; } = new();
        public int Score { get; private set; }
        /// <summary>Best score before this run, or this run's score once game over saves a new record.</summary>
        public int HighScore { get; private set; }

        float _watchStartTime;

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
            input.AnyButtonPressed -= OnButtonWhileWatching;
        }

        void Start()
        {
            if (showTitle) ShowTitle();
            else BeginRun();
        }

        /// <summary>The title screen: the arena frozen behind it and no gameplay input.</summary>
        void ShowTitle()
        {
            Time.timeScale = 0f;
            input.GameplayEnabled = false;
            ResetScore();
            State.ChangeState(GameState.Title);
        }

        /// <summary>For UI buttons. From the title screen, the agent plays a fresh arena until any button is pressed.</summary>
        public void WatchAI()
        {
            if (!State.Is(GameState.Title)) return;
            if (arena != null) arena.ResetArena();
            Time.timeScale = 1f;
            input.GameplayEnabled = false;
            ResetScore();
            _watchStartTime = Time.unscaledTime;
            input.AnyButtonPressed += OnButtonWhileWatching;
            State.ChangeState(GameState.Watching);
        }

        void OnButtonWhileWatching()
        {
            if (!State.Is(GameState.Watching) || Time.unscaledTime < _watchStartTime + watchExitDelay) return;
            input.AnyButtonPressed -= OnButtonWhileWatching;
            // State first, so the agent lets go of the player before the arena resets behind the title.
            ShowTitle();
            if (arena != null) arena.ResetArena();
        }

        void BeginRun()
        {
            Time.timeScale = 1f;
            input.GameplayEnabled = true;
            ResetScore();
            State.ChangeState(GameState.Playing);
        }

        void ResetScore()
        {
            Score = 0;
            HighScore = HighScoreStore.Load();
            EventBus.Publish(new HighScoreChanged(HighScore, false));
            EventBus.Publish(new ScoreChanged(Score));
        }

        /// <summary>For UI buttons. Starts a run from the title screen.</summary>
        public void Play()
        {
            if (!State.Is(GameState.Title)) return;
            if (arena != null) arena.ResetArena();
            BeginRun();
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

        /// <summary>For UI buttons. Works from game over and from the pause menu.</summary>
        public void Restart()
        {
            if (arena == null)
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                return;
            }
            // Arena first, so the player is back and the old enemies gone before the state says Playing.
            arena.ResetArena();
            BeginRun();
        }

        /// <summary>Closes the game. In the editor, where Application.Quit does nothing, it leaves Play mode instead.</summary>
        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void OnPlayerDied(PlayerDied _)
        {
            // The agent starts its next go by itself; nothing is recorded.
            if (State.Is(GameState.Watching))
            {
                ResetScore();
                return;
            }
            input.GameplayEnabled = false;
            // Before the state change, so the game over screen already knows about the record.
            if (HighScoreStore.TrySubmit(Score))
            {
                HighScore = Score;
                EventBus.Publish(new HighScoreChanged(HighScore, true));
            }
            State.ChangeState(GameState.GameOver);
        }

        void OnEnemyKilled(EnemyKilled evt)
        {
            if (!State.Is(GameState.Playing) && !State.Is(GameState.Watching)) return;
            Score += evt.ScoreValue;
            EventBus.Publish(new ScoreChanged(Score));
        }
    }
}
