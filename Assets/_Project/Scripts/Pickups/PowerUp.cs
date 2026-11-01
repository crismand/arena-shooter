using UnityEngine;

namespace AS.Pickups
{
    /// <summary>
    /// A kind of power-up, as an asset. Each subclass decides what it does to the player in
    /// <see cref="Apply"/>; the pickup in the world just carries one of these and calls Apply on touch.
    /// Timed ones run for <see cref="Duration"/> and show on the HUD; instant ones (health, bomb) don't.
    /// </summary>
    public abstract class PowerUp : ScriptableObject
    {
        [Tooltip("Shown on the HUD while active.")]
        [SerializeField] string label = "POWER";
        [Tooltip("Tint of the pickup in the world.")]
        [SerializeField] Color color = Color.white;
        [Tooltip("Shape of the pickup in the world. Empty uses the default spinning diamond.")]
        [SerializeField] Sprite icon;
        [Tooltip("Seconds a timed power-up lasts. Ignored by instant ones.")]
        [SerializeField, Min(0.1f)] float duration = 8f;
        [Tooltip("Relative chance of being the one dropped. 2 drops twice as often as 1.")]
        [SerializeField, Min(0f)] float dropWeight = 1f;

        public string Label => label;
        public Color Color => color;
        public Sprite Icon => icon;
        public float Duration => duration;
        public float DropWeight => dropWeight;
        /// <summary>Takes effect once on pickup, with no duration.</summary>
        public virtual bool IsInstant => false;

        public abstract void Apply(PowerUpReceiver receiver);
    }
}
