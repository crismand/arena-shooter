using AS.Combat;
using UnityEngine;

namespace AS.Pickups
{
    /// <summary>Temporarily swaps the player's weapon for different <see cref="WeaponData"/> (spread shot, rapid fire).</summary>
    [CreateAssetMenu(menuName = "AS/Power-Up/Weapon", fileName = "PowerUp_Weapon")]
    public class WeaponPowerUp : PowerUp
    {
        [SerializeField] WeaponData weapon;

        public WeaponData Weapon => weapon;

        public override void Apply(PowerUpReceiver receiver) => receiver.BoostWeapon(weapon, Duration, Label);
    }
}
