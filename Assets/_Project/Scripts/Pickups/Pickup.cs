using System;
using AS.Core;
using UnityEngine;

namespace AS.Pickups
{
    /// <summary>
    /// A power-up lying in the arena. Spins (or, with its own icon, pulses) so it reads as collectible, blinks before it expires,
    /// and applies its <see cref="PowerUp"/> to whatever <see cref="PowerUpReceiver"/> touches it.
    /// Only the player can touch it (Pickup layer collides with Player only).
    /// </summary>
    public class Pickup : MonoBehaviour
    {
        [SerializeField] SpriteRenderer spriteRenderer;
        [SerializeField] float spinSpeed = 120f;
        [Tooltip("Pulses per second for pickups with their own icon, which stay upright instead of spinning.")]
        [SerializeField, Min(0f)] float pulseRate = 1.5f;
        [SerializeField, Range(0f, 0.5f)] float pulseSize = 0.12f;
        [Tooltip("Seconds before expiry at which it starts blinking.")]
        [SerializeField, Min(0f)] float blinkWarning = 3f;
        [SerializeField, Min(0.02f)] float blinkInterval = 0.15f;

        PowerUp _powerUp;
        EventChannel _events;
        Action<Pickup> _release;
        float _expiresAt;
        Sprite _defaultSprite;
        Quaternion _defaultVisualRotation;
        bool _spins;

        public PowerUp PowerUp => _powerUp;

        void Awake()
        {
            _events = Arena.EventsFor(this);
            _defaultSprite = spriteRenderer.sprite;
            _defaultVisualRotation = spriteRenderer.transform.localRotation;
        }

        public void Init(PowerUp powerUp, float lifetime, Action<Pickup> release)
        {
            _powerUp = powerUp;
            _release = release;
            _expiresAt = Time.time + lifetime;
            spriteRenderer.color = powerUp.Color;
            spriteRenderer.enabled = true;

            // Pooled: undo whatever the last power-up's look left behind.
            _spins = powerUp.Icon == null;
            spriteRenderer.sprite = _spins ? _defaultSprite : powerUp.Icon;
            spriteRenderer.transform.localRotation = _spins ? _defaultVisualRotation : Quaternion.identity;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        void Update()
        {
            float remaining = _expiresAt - Time.time;
            if (remaining <= 0f)
            {
                Despawn();
                return;
            }

            if (_spins) transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
            else transform.localScale = Vector3.one * (1f + pulseSize * Mathf.Sin(Time.time * pulseRate * 2f * Mathf.PI));
            spriteRenderer.enabled = remaining > blinkWarning || Mathf.FloorToInt(remaining / blinkInterval) % 2 == 0;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (_powerUp == null) return;
            var receiver = other.GetComponentInParent<PowerUpReceiver>();
            if (receiver == null) return;
            _events.Publish(new PickupCollected(transform.position, _powerUp.Color, _powerUp.Label));
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
