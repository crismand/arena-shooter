using UnityEngine;

namespace AS.Enemies
{
    /// <summary>
    /// Charger: closes in until the target is within <see cref="dashRange"/>, then stops and winds up
    /// (facing the target and showing <see cref="telegraph"/> along the line it will take), charges in
    /// a straight line it can't change, and skids to a stop before it can do it again. Sidestepping
    /// during the wind-up beats it. A wall ends a charge early.
    /// </summary>
    public class Dasher : EnemyBase
    {
        public enum Phase { Approach, WindUp, Dash, Recover }

        [Tooltip("Starts a wind-up when the target is this close.")]
        [SerializeField, Min(0f)] float dashRange = 9f;
        [Tooltip("Seconds it stands still and telegraphs before charging.")]
        [SerializeField, Min(0f)] float windUpTime = 0.6f;
        [SerializeField, Min(0f)] float dashSpeed = 12f;
        [SerializeField, Min(0.05f)] float dashTime = 0.5f;
        [Tooltip("Seconds of skidding to a stop after a charge, before it can approach again.")]
        [SerializeField, Min(0f)] float recoverTime = 0.8f;
        [Tooltip("Deceleration while skidding, in units per second squared. Low enough to slide a little.")]
        [SerializeField, Min(0f)] float skidDeceleration = 25f;
        [Tooltip("Seconds after spawning before the first wind-up, so it never charges the instant it appears.")]
        [SerializeField, Min(0f)] float firstDashDelay = 0.8f;
        [Tooltip("Shown during the wind-up, pointing along the charge. Optional.")]
        [SerializeField] GameObject telegraph;
        [Tooltip("Layers that stop a charge on contact.")]
        [SerializeField] LayerMask stopsDash;

        float _phaseEndsAt;
        float _canDashAt;
        Vector2 _dashDirection;

        public Phase Current { get; private set; }

        Vector2 ToTarget => Target.position - transform.position;

        // Entering Approach also hides a telegraph left showing by a death mid wind-up. (No OnDisable
        // here: Unity would call it instead of EnemyBase's, which unhooks Health.)
        protected override void OnSpawned()
        {
            _canDashAt = Time.time + firstDashDelay;
            Enter(Phase.Approach, 0f);
        }

        protected override float Acceleration => Current switch
        {
            // Instant: a charge starts at full speed and a wind-up stops dead.
            Phase.Dash or Phase.WindUp => float.MaxValue,
            Phase.Recover => skidDeceleration,
            _ => data.Acceleration,
        };

        protected override void FixedUpdate()
        {
            UpdatePhase();
            base.FixedUpdate();
        }

        void UpdatePhase()
        {
            switch (Current)
            {
                case Phase.Approach:
                    if (HasTarget && Time.time >= _canDashAt && ToTarget.sqrMagnitude <= dashRange * dashRange)
                        Enter(Phase.WindUp, windUpTime);
                    break;
                case Phase.WindUp:
                    if (Time.time < _phaseEndsAt) break;
                    // Locked in at the end of the wind-up, so moving during it dodges the charge.
                    _dashDirection = HasTarget && ToTarget.sqrMagnitude > 0.0001f ? ToTarget.normalized : (Vector2)transform.right;
                    Enter(Phase.Dash, dashTime);
                    break;
                case Phase.Dash:
                    if (Time.time >= _phaseEndsAt) Enter(Phase.Recover, recoverTime);
                    break;
                case Phase.Recover:
                    if (Time.time >= _phaseEndsAt) Enter(Phase.Approach, 0f);
                    break;
            }
        }

        void Enter(Phase phase, float duration)
        {
            Current = phase;
            _phaseEndsAt = Time.time + duration;
            if (telegraph != null) telegraph.SetActive(phase == Phase.WindUp);
        }

        protected override Vector2 DesiredVelocity() => Current switch
        {
            Phase.Approach => ToTarget.normalized * data.MoveSpeed,
            Phase.Dash => _dashDirection * dashSpeed,
            _ => Vector2.zero,
        };

        protected override Vector2 FacingDirection(Vector2 desiredVelocity) => Current switch
        {
            Phase.WindUp => HasTarget ? ToTarget : Vector2.zero,
            Phase.Dash => _dashDirection,
            Phase.Recover => Vector2.zero,
            _ => desiredVelocity,
        };

        void OnCollisionEnter2D(Collision2D collision)
        {
            if (Current == Phase.Dash && (stopsDash.value & (1 << collision.gameObject.layer)) != 0)
                Enter(Phase.Recover, recoverTime);
        }
    }
}
