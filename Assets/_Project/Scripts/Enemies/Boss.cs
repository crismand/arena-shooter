using AS.Combat;
using AS.Core;
using UnityEngine;

namespace AS.Enemies
{
    /// <summary>
    /// The boss of every few waves. Keeps a middle distance from the target and circles it, like a
    /// slow <see cref="Shooter"/>, and fires rings of slow bullets with gaps that turn each time.
    /// Below <see cref="phaseTwoAt"/> of its health it fires rings less often and summons Chasers, and,
    /// if <see cref="AimedShots"/> is on (later bosses), fires aimed bursts as well. Reports its health
    /// as <see cref="BossHealthChanged"/> for the HUD.
    /// </summary>
    public class Boss : EnemyBase
    {
        public enum Phase { One, Two }

        [Header("Movement")]
        [SerializeField, Min(0f)] float preferredDistance = 6f;
        [SerializeField, Min(0f)] float distanceBand = 2f;
        [SerializeField, Range(0f, 1f)] float strafeFactor = 0.7f;

        [Header("Attacks")]
        [Tooltip("Fires the rings. Its data's projectile count is ignored; rings use Ring Count.")]
        [SerializeField] Weapon ringWeapon;
        [Tooltip("Fires the aimed bursts in phase two, when Aimed Shots is on.")]
        [SerializeField] Weapon burstWeapon;
        [SerializeField, Min(3)] int ringCount = 16;
        [Tooltip("Degrees each ring turns from the last, so the gaps move.")]
        [SerializeField] float ringTurn = 11.25f;
        [SerializeField, Min(0.1f)] float ringInterval = 2.2f;
        [SerializeField, Min(0.1f)] float phaseTwoRingInterval = 3f;
        [Tooltip("Fraction of max health below which phase two starts.")]
        [SerializeField, Range(0f, 1f)] float phaseTwoAt = 0.5f;
        [SerializeField, Min(0.1f)] float burstInterval = 1.6f;
        [Tooltip("Seconds after spawning before the first attack.")]
        [SerializeField, Min(0f)] float firstAttackDelay = 1.5f;

        [Header("Summons (phase two)")]
        [SerializeField] EnemyBase minionPrefab;
        [SerializeField, Min(0)] int minionsPerSummon = 2;
        [Tooltip("Seconds between summons. The first comes as phase two starts.")]
        [SerializeField, Min(0.5f)] float summonInterval = 6f;

        [Header("Look")]
        [Tooltip("Recoloured in phase two. Not the sprite the damage flash uses.")]
        [SerializeField] SpriteRenderer core;
        [SerializeField] Color phaseTwoCoreColor = new(1f, 0.15f, 0.15f);

        float _nextRingAt;
        float _nextBurstAt;
        float _nextSummonAt;
        float _ringAngle;
        float _strafeSign;
        Color _coreColor;

        public Phase Current { get; private set; }
        /// <summary>Whether phase two adds aimed bursts. Off on every spawn; the wave director turns it on for later bosses.</summary>
        public bool AimedShots { get; set; }

        Vector2 ToTarget => Target.position - transform.position;

        protected override void Awake()
        {
            base.Awake();
            if (core != null) _coreColor = core.color;
            Health.Damaged += _ => PublishHealth();
        }

        protected override void OnSpawned()
        {
            Current = Phase.One;
            AimedShots = false;
            if (core != null) core.color = _coreColor;
            _nextRingAt = Time.time + firstAttackDelay;
            _nextBurstAt = float.MaxValue;
            _nextSummonAt = float.MaxValue;
            _ringAngle = Random.Range(0f, 360f);
            _strafeSign = Random.value < 0.5f ? -1f : 1f;
            PublishHealth();
        }

        /// <summary>Sets max health to the data's times <paramref name="multiplier"/>, for later, tougher bosses.</summary>
        public void ScaleHealth(float multiplier)
        {
            Health.ResetHealth(data.MaxHealth * Mathf.Max(0.01f, multiplier));
            PublishHealth();
        }

        protected override Vector2 DesiredVelocity()
        {
            Vector2 toTarget = ToTarget;
            float distance = toTarget.magnitude;
            Vector2 direction = distance > 0.001f ? toTarget / distance : Vector2.right;

            if (distance > preferredDistance + distanceBand) return direction * data.MoveSpeed;
            if (distance < preferredDistance - distanceBand) return -direction * data.MoveSpeed;
            return new Vector2(-direction.y, direction.x) * (_strafeSign * strafeFactor * data.MoveSpeed);
        }

        protected override Vector2 FacingDirection(Vector2 desiredVelocity) => HasTarget ? ToTarget : desiredVelocity;

        void Update()
        {
            if (!HasTarget) return;
            if (Current == Phase.One && Health.Current <= Health.Max * phaseTwoAt) EnterPhaseTwo();

            if (Time.time >= _nextRingAt)
            {
                ringWeapon.FireRing(ringCount, _ringAngle);
                _ringAngle += ringTurn;
                _nextRingAt = Time.time + (Current == Phase.One ? ringInterval : phaseTwoRingInterval);
                Events.Publish(new ShotFired(transform.position, byPlayer: false));
            }

            if (Time.time >= _nextBurstAt)
            {
                // One volley of the burst weapon's spread. Its fire rate only has to be faster than this interval.
                if (burstWeapon.TryFire(ToTarget)) Events.Publish(new ShotFired(transform.position, byPlayer: false));
                _nextBurstAt = Time.time + burstInterval;
            }

            if (Time.time >= _nextSummonAt)
            {
                Summon();
                _nextSummonAt = Time.time + summonInterval;
            }
        }

        void EnterPhaseTwo()
        {
            Current = Phase.Two;
            if (core != null) core.color = phaseTwoCoreColor;
            if (AimedShots) _nextBurstAt = Time.time + 0.5f;
            _nextSummonAt = Time.time;
            _nextRingAt = Mathf.Max(_nextRingAt, Time.time + 1f);
        }

        void Summon()
        {
            // Without a spawner (placed by hand) nothing would count the minions toward the wave.
            if (Spawner == null || minionPrefab == null) return;
            for (int i = 0; i < minionsPerSummon; i++)
            {
                float angle = (360f * i / minionsPerSummon + Random.Range(0f, 360f)) * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var minion = Spawner.Spawn(minionPrefab, (Vector2)transform.position + direction * 1.8f);
                minion.Launch(direction * 5f);
            }
        }

        protected override void OnKilled() => Events.Publish(new BossHealthChanged(0f, Health.Max));

        void PublishHealth() => Events.Publish(new BossHealthChanged(Health.Current, Health.Max));
    }
}
