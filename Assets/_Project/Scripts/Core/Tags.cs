namespace AS.Core
{
    /// <summary>
    /// Tag names. Physics sorts objects by <see cref="Layers"/>; the agent's ray sensor tells what it
    /// sees apart by tag, so anything it should recognise carries one of these.
    /// </summary>
    public static class Tags
    {
        public const string Enemy = "Enemy";
        public const string EnemyProjectile = "EnemyProjectile";
        public const string Wall = "Wall";
        public const string Pickup = "Pickup";

        public static readonly string[] All = { Enemy, EnemyProjectile, Wall, Pickup };
    }
}
