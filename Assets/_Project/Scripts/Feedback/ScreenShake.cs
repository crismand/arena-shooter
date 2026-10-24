using AS.Core;
using Unity.Cinemachine;
using UnityEngine;

namespace AS.Feedback
{
    /// <summary>
    /// Kicks the camera on hits and deaths through a Cinemachine impulse (the camera needs a
    /// CinemachineImpulseListener). Each kick goes in a random direction so repeats don't feel mechanical.
    /// </summary>
    public class ScreenShake : MonoBehaviour
    {
        [SerializeField] CinemachineImpulseSource source;
        [SerializeField, Min(0f)] float playerHitForce = 0.35f;
        [SerializeField, Min(0f)] float playerDeathForce = 0.9f;
        [Tooltip("Force for a 10-point kill. Enemies worth more shake harder, by the square root of their value.")]
        [SerializeField, Min(0f)] float killForce = 0.1f;

        void OnEnable()
        {
            EventBus.Subscribe<PlayerDamaged>(OnPlayerDamaged);
            EventBus.Subscribe<PlayerDied>(OnPlayerDied);
            EventBus.Subscribe<EnemyKilled>(OnEnemyKilled);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<PlayerDamaged>(OnPlayerDamaged);
            EventBus.Unsubscribe<PlayerDied>(OnPlayerDied);
            EventBus.Unsubscribe<EnemyKilled>(OnEnemyKilled);
        }

        void OnPlayerDamaged(PlayerDamaged _) => Shake(playerHitForce);
        void OnPlayerDied(PlayerDied _) => Shake(playerDeathForce);
        void OnEnemyKilled(EnemyKilled evt) => Shake(killForce * Mathf.Sqrt(Mathf.Max(1, evt.ScoreValue) / 10f));

        public void Shake(float force)
        {
            if (source == null || force <= 0f) return;
            source.GenerateImpulseWithVelocity(Random.insideUnitCircle.normalized * force);
        }
    }
}
