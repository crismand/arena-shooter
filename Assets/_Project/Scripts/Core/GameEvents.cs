using UnityEngine;

namespace AS.Core
{
    // Event payloads published through EventBus. Keep them small, immutable structs of plain
    // values, so Core stays free of references to gameplay assemblies.

    public readonly struct GameStateChanged
    {
        public readonly GameState Previous;
        public readonly GameState Current;
        public GameStateChanged(GameState previous, GameState current) { Previous = previous; Current = current; }
    }

    public readonly struct PlayerHealthChanged
    {
        public readonly float Current;
        public readonly float Max;
        public PlayerHealthChanged(float current, float max) { Current = current; Max = max; }
    }

    public readonly struct PlayerDamaged
    {
        public readonly Vector2 Position;
        public PlayerDamaged(Vector2 position) { Position = position; }
    }

    public readonly struct PlayerDied
    {
        public readonly Vector2 Position;
        public PlayerDied(Vector2 position) { Position = position; }
    }

    public readonly struct EnemyKilled
    {
        public readonly int ScoreValue;
        public readonly Vector2 Position;
        /// <summary>Chance (0-1) that this kill drops a power-up.</summary>
        public readonly float DropChance;
        /// <summary>The enemy's body colour, for effects. Fully transparent when unknown.</summary>
        public readonly Color Color;
        public EnemyKilled(int scoreValue, Vector2 position, float dropChance, Color color = default)
        {
            ScoreValue = scoreValue;
            Position = position;
            DropChance = dropChance;
            Color = color;
        }
    }

    /// <summary>An enemy took damage (including the killing blow).</summary>
    public readonly struct EnemyDamaged
    {
        public readonly Vector2 Position;
        public EnemyDamaged(Vector2 position) { Position = position; }
    }

    /// <summary>A weapon fired a volley (one event per trigger pull, however many bullets).</summary>
    public readonly struct ShotFired
    {
        public readonly Vector2 Position;
        public readonly bool ByPlayer;
        public ShotFired(Vector2 position, bool byPlayer) { Position = position; ByPlayer = byPlayer; }
    }

    /// <summary>A projectile hit something (an enemy, the player or a wall) and despawned.</summary>
    public readonly struct ProjectileImpact
    {
        public readonly Vector2 Position;
        public ProjectileImpact(Vector2 position) { Position = position; }
    }

    public readonly struct PickupCollected
    {
        public readonly Vector2 Position;
        public readonly Color Color;
        public PickupCollected(Vector2 position, Color color) { Position = position; Color = color; }
    }

    /// <summary>A power-up took effect. Picking up the same kind again republishes with a fresh duration.</summary>
    public readonly struct PowerUpActivated
    {
        public readonly string Label;
        public readonly float Duration;
        public PowerUpActivated(string label, float duration) { Label = label; Duration = duration; }
    }

    public readonly struct ScoreChanged
    {
        public readonly int Score;
        public ScoreChanged(int score) { Score = score; }
    }

    /// <summary>
    /// The best score so far. Published at the start of a run, and again at game over when the run
    /// beat it (<see cref="IsNewRecord"/>), before the state changes to GameOver.
    /// </summary>
    public readonly struct HighScoreChanged
    {
        public readonly int HighScore;
        public readonly bool IsNewRecord;
        public HighScoreChanged(int highScore, bool isNewRecord) { HighScore = highScore; IsNewRecord = isNewRecord; }
    }

    public readonly struct WaveStarted
    {
        public readonly int Number;
        public readonly int EnemyCount;
        public WaveStarted(int number, int enemyCount) { Number = number; EnemyCount = enemyCount; }
    }

    public readonly struct WaveCleared
    {
        public readonly int Number;
        public WaveCleared(int number) { Number = number; }
    }
}
