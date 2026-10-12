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

    public readonly struct PlayerDied { }

    public readonly struct EnemyKilled
    {
        public readonly int ScoreValue;
        public readonly Vector2 Position;
        public EnemyKilled(int scoreValue, Vector2 position) { ScoreValue = scoreValue; Position = position; }
    }

    public readonly struct ScoreChanged
    {
        public readonly int Score;
        public ScoreChanged(int score) { Score = score; }
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
