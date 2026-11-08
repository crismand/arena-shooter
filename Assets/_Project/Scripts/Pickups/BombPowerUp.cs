using UnityEngine;

namespace AS.Pickups
{
    /// <summary>Goes off on pickup: damages every enemy in the arena and clears enemy bullets.</summary>
    [CreateAssetMenu(menuName = "AS/Power-Up/Bomb", fileName = "PowerUp_Bomb")]
    public class BombPowerUp : PowerUp
    {
        [Tooltip("Enough to kill everything but Tanks (and bosses).")]
        [SerializeField, Min(0f)] float damage = 6f;
        [Tooltip("Units from the player. Large enough to reach every corner of the arena.")]
        [SerializeField, Min(0f)] float radius = 40f;

        public float Damage => damage;
        public float Radius => radius;
        public override bool IsInstant => true;

        public override void Apply(PowerUpReceiver receiver) => receiver.DetonateBomb(damage, radius);
    }
}
