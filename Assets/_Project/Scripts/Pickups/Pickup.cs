using System;
using AS.Core;
using UnityEngine;

namespace AS.Pickups
{
    /// <summary>
    /// A power-up lying in the arena. Spins so it reads as collectible, blinks before it expires,
    /// and applies its <see cref="PowerUp"/> to whatever <see cref="PowerUpReceiver"/> touches it.
    /// Only the player can touch it (Pickup layer collides with Player only).
    /// </summary>
    public class Pickup : MonoBehaviour
    {
        [SerializeField] SpriteRenderer spriteRenderer;
        [SerializeField] float spinSpeed = 120f;
        [Tooltip("Seconds before expiry at which it starts blinking.")]
        [SerializeField, Min(0f)] float blinkWarning = 3f;
        [SerializeField, Min(0.02f)] float blinkInterval = 0.15f;

        PowerUp _powerUp;
        Action<Pickup> _release;
        float _expiresAt;

        public PowerUp PowerUp => _powerUp;

        public void Init(PowerUp powerUp, float lifetime, Action<Pickup> release)
        {
            _powerUp = powerUp;
            _release = release;
            _expiresAt = Time.time + lifetime;
            spriteRenderer.color = powerUp.Color;
            spriteRenderer.enabled = true;
        }

        void Update()
        {
            float remaining = _expiresAt - Time.time;
            if (remaining <= 0f)
            {
                Despawn();
                return;
            }

            transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
            spriteRenderer.enabled = remaining > blinkWarning || Mathf.FloorToInt(remaining / blinkInterval) % 2 == 0;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (_powerUp == null) return;
            var receiver = other.GetComponentInParent<PowerUpReceiver>();
            if (receiver == null) return;
            EventBus.Publish(new PickupCollected(transform.position, _powerUp.Color));
            _powerUp.Apply(receiver);
            Despawn();
        }

        void Despawn()
        {
            _powerUp = null;
            if (_release != null) _release(this);
            else Destroy(gameObject);
        }
    }
}
