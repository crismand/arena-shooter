using UnityEngine;

namespace AS.Pickups
{
    /// <summary>Restores some health at once, up to the maximum.</summary>
    [CreateAssetMenu(menuName = "AS/Power-Up/Health", fileName = "PowerUp_Health")]
    public class HealthPowerUp : PowerUp
    {
        [SerializeField, Min(0.1f)] float amount = 1f;

        public float Amount => amount;
        public override bool IsInstant => true;

        public override void Apply(PowerUpReceiver receiver) => receiver.Heal(amount);
    }
}
