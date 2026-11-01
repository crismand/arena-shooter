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
        public bool IsInvulnerable => Time.time < _invulnerableUntil;

        public event Action<DamageInfo> Damaged;
        public event Action Died;
        /// <summary>Health went up (not on a reset).</summary>
        public event Action Healed;

        void Awake() => Current = maxHealth;

        /// <summary>Refills to <paramref name="max"/>. Pooled objects call this when reused.</summary>
        public void ResetHealth(float max)
        {
            maxHealth = max;
            Current = max;
            _invulnerableUntil = 0f;
        }

        /// <summary>Ignore all damage for <paramref name="seconds"/>. Never shortens a longer window already running.</summary>
        public void GrantInvulnerability(float seconds) =>
            _invulnerableUntil = Mathf.Max(_invulnerableUntil, Time.time + seconds);

        /// <summary>Adds up to <paramref name="amount"/>, never past the maximum. False if already full or dead.</summary>
        public bool Heal(float amount)
        {
            if (!IsAlive || amount <= 0f || Current >= maxHealth) return false;
            Current = Mathf.Min(maxHealth, Current + amount);
            Healed?.Invoke();
            return true;
        }

        public void TakeDamage(in DamageInfo info)
        {
            if (!IsAlive || info.Amount <= 0f || IsInvulnerable) return;

            Current = Mathf.Max(0f, Current - info.Amount);
            _invulnerableUntil = Time.time + invulnerabilityTime;
            Damaged?.Invoke(info);
            if (!IsAlive) Died?.Invoke();
        }
    }
}
