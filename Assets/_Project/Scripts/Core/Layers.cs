namespace AS.Core
{
    /// <summary>Physics layer names. The collision matrix between them decides what can hit what.</summary>
    public static class Layers
    {
        public const string Player = "Player";
        public const string Enemy = "Enemy";
        public const string PlayerShot = "PlayerShot";
        public const string EnemyShot = "EnemyShot";
        public const string Wall = "Wall";
        public const string Pickup = "Pickup";
    }
}
