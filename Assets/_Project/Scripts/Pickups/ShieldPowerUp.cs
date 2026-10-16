using UnityEngine;

namespace AS.Pickups
{
    /// <summary>Makes the player ignore all damage for the duration.</summary>
    [CreateAssetMenu(menuName = "AS/Power-Up/Shield", fileName = "PowerUp_Shield")]
    public class ShieldPowerUp : PowerUp
    {
        public override void Apply(PowerUpReceiver receiver) => receiver.RaiseShield(Duration, Label);
    }
}
