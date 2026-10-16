using UnityEngine;

namespace AS.Enemies
{
    [CreateAssetMenu(menuName = "AS/Enemy Data", fileName = "Enemy")]
    public class EnemyData : ScriptableObject
    {
        [SerializeField, Min(1f)] float maxHealth = 3f;
        [SerializeField, Min(0f)] float moveSpeed = 3.5f;
        [Tooltip("Units per second squared. Lower makes the enemy drift and overshoot.")]
        [SerializeField, Min(0f)] float acceleration = 20f;
        [Tooltip("Damage dealt to the player on touch.")]
        [SerializeField, Min(0f)] float contactDamage = 1f;
        [SerializeField, Min(0)] int scoreValue = 10;
        [Tooltip("Chance (0-1) that this enemy drops a power-up when killed.")]
        [SerializeField, Range(0f, 1f)] float dropChance = 0.08f;

        public float MaxHealth => maxHealth;
        public int ScoreValue => scoreValue;
        public float DropChance => dropChance;
        public float MoveSpeed => moveSpeed;
        public float Acceleration => acceleration;
        public float ContactDamage => contactDamage;
    }
}
