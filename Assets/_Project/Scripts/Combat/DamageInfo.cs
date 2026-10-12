using UnityEngine;

namespace AS.Combat
{
    /// <summary>Everything a hit carries to an <see cref="IDamageable"/>.</summary>
    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly Vector2 Direction;
        public readonly GameObject Source;

        public DamageInfo(float amount, Vector2 direction, GameObject source)
        {
            Amount = amount;
            Direction = direction;
            Source = source;
        }
    }
}
