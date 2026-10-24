using AS.Combat;
using AS.Core;
using UnityEngine;

namespace AS.Enemies
{
    /// <summary>
    /// Ranged enemy: closes in until it's about <see cref="preferredDistance"/> away, backs off if
    /// the target gets closer, circles at that range, and fires its <see cref="Weapon"/> at the target.
    /// </summary>
    public class Shooter : EnemyBase
    {
        [SerializeField] Weapon weapon;
        [SerializeField, Min(0f)] float preferredDistance = 7f;
        [Tooltip("Within ± this of the preferred distance it circles instead of closing in or backing off.")]
        [SerializeField, Min(0f)] float distanceBand = 1.5f;
        [Tooltip("Circling speed as a fraction of move speed.")]
        [SerializeField, Range(0f, 1f)] float strafeFactor = 0.6f;
        [SerializeField, Min(0f)] float fireRange = 11f;
        [Tooltip("Seconds after spawning before the first shot, so it never fires the instant it appears.")]
        [SerializeField, Min(0f)] float firstShotDelay = 1f;

        float _canFireAt;
        float _strafeSign;

        Vector2 ToTarget => Target.position - transform.position;

        protected override void OnSpawned()
        {
            _canFireAt = Time.time + firstShotDelay;
            _strafeSign = Random.value < 0.5f ? -1f : 1f;
        }

        protected override Vector2 DesiredVelocity()
        {
            Vector2 toTarget = ToTarget;
            float distance = toTarget.magnitude;
            Vector2 direction = distance > 0.001f ? toTarget / distance : Vector2.right;

            if (distance > preferredDistance + distanceBand) return direction * data.MoveSpeed;
            if (distance < preferredDistance - distanceBand) return -direction * data.MoveSpeed;
            // Perpendicular to the target direction: circle around it.
            return new Vector2(-direction.y, direction.x) * (_strafeSign * strafeFactor * data.MoveSpeed);
        }

        protected override Vector2 FacingDirection(Vector2 desiredVelocity) => HasTarget ? ToTarget : desiredVelocity;

        void Update()
        {
            if (!HasTarget || Time.time < _canFireAt) return;
            Vector2 toTarget = ToTarget;
            if (toTarget.sqrMagnitude <= fireRange * fireRange && weapon.TryFire(toTarget))
                EventBus.Publish(new ShotFired(transform.position, byPlayer: false));
        }
    }
}
