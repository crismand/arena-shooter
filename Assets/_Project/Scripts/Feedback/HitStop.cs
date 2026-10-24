using AS.Core;
using UnityEngine;

namespace AS.Feedback
{
    /// <summary>
    /// Freezes the game for a few frames on big hits, which sells the impact. Overlapping requests
    /// extend the freeze rather than stacking. Never fights the pause menu: pausing cancels a freeze
    /// and leaves the time scale to <see cref="GameManager"/>.
    /// </summary>
    public class HitStop : MonoBehaviour
    {
        [Tooltip("Real-time seconds to freeze when the player is hurt.")]
        [SerializeField, Min(0f)] float playerHitDuration = 0.08f;
        [SerializeField, Min(0f)] float playerDeathDuration = 0.3f;
        [Tooltip("Freeze per kill. Keep it tiny: kills come in bunches.")]
        [SerializeField, Min(0f)] float killDuration = 0.02f;
        [Tooltip("Time scale during the freeze. 0 is a full stop; a little above gives a slow-mo feel instead.")]
        [SerializeField, Range(0f, 1f)] float frozenTimeScale;

        float _resumeAt;
        bool _paused;

        public bool IsFrozen { get; private set; }

        void OnEnable()
        {
            EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
            EventBus.Subscribe<PlayerDamaged>(OnPlayerDamaged);
            EventBus.Subscribe<PlayerDied>(OnPlayerDied);
            EventBus.Subscribe<EnemyKilled>(OnEnemyKilled);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);
            EventBus.Unsubscribe<PlayerDamaged>(OnPlayerDamaged);
            EventBus.Unsubscribe<PlayerDied>(OnPlayerDied);
            EventBus.Unsubscribe<EnemyKilled>(OnEnemyKilled);
            if (IsFrozen) Resume();
        }

        void Update()
        {
            // Unscaled: scaled time doesn't move while we're frozen.
            if (IsFrozen && Time.unscaledTime >= _resumeAt) Resume();
        }

        public void Freeze(float seconds)
        {
            if (seconds <= 0f || _paused) return;
            _resumeAt = Mathf.Max(_resumeAt, Time.unscaledTime + seconds);
            IsFrozen = true;
            Time.timeScale = frozenTimeScale;
        }

        void Resume()
        {
            IsFrozen = false;
            _resumeAt = 0f;
            if (!_paused) Time.timeScale = 1f;
        }

        void OnGameStateChanged(GameStateChanged evt)
        {
            _paused = evt.Current == GameState.Paused;
            // GameManager has just set the time scale for the pause: drop the freeze without touching it.
            if (_paused && IsFrozen)
            {
                IsFrozen = false;
                _resumeAt = 0f;
            }
        }

        void OnPlayerDamaged(PlayerDamaged _) => Freeze(playerHitDuration);
        void OnPlayerDied(PlayerDied _) => Freeze(playerDeathDuration);
        void OnEnemyKilled(EnemyKilled _) => Freeze(killDuration);
    }
}
