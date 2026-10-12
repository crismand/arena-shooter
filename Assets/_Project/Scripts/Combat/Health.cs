using System;
using UnityEngine;

namespace AS.Combat
{
    /// <summary>
    /// Hit points for anything that can be shot or touched. Optional invulnerability window after
    /// each hit (the player uses it so a crowd of enemies can't drain health in one frame).
    /// </summary>
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1f)] float maxHealth = 3f;
        [Tooltip("Seconds after a hit during which further damage is ignored.")]
        [SerializeField, Min(0f)] float invulnerabilityTime;

        float _invulnerableUntil;

        public float Max => maxHealth;
        public float Current { get; private set; }
        public bool IsAlive => Current > 0f;

        public event Action<DamageInfo> Damaged;
        public event Action Died;

        void Awake() => Current = maxHealth;

        /// <summary>Refills to <paramref name="max"/>. Pooled objects call this when reused.</summary>
        public void ResetHealth(float max)
        {
            maxHealth = max;
            Current = max;
            _invulnerableUntil = 0f;
        }

        public void TakeDamage(in DamageInfo info)
        {
            if (!IsAlive || info.Amount <= 0f || Time.time < _invulnerableUntil) return;

            Current = Mathf.Max(0f, Current - info.Amount);
            _invulnerableUntil = Time.time + invulnerabilityTime;
            Damaged?.Invoke(info);
            if (!IsAlive) Died?.Invoke();
        }
    }
}
