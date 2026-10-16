using UnityEngine;

namespace AS.Pickups
{
    /// <summary>
    /// A kind of power-up, as an asset. Each subclass decides what it does to the player in
    /// <see cref="Apply"/>; the pickup in the world just carries one of these and calls Apply on touch.
    /// </summary>
    public abstract class PowerUp : ScriptableObject
    {
        [Tooltip("Shown on the HUD while active.")]
        [SerializeField] string label = "POWER";
        [Tooltip("Tint of the pickup in the world.")]
        [SerializeField] Color color = Color.white;
        [SerializeField, Min(0.1f)] float duration = 8f;

        public string Label => label;
        public Color Color => color;
        public float Duration => duration;

        public abstract void Apply(PowerUpReceiver receiver);
    }
}
