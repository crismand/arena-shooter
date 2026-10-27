#if UNITY_EDITOR
using System.Collections;
using AS.Combat;
using AS.Core;
using AS.Pickups;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AS.Tests
{
    /// <summary>Pickups, drops and the three power-up effects.</summary>
    public class PowerUpTests : ArenaTestFixture
    {
        const string SpreadPath = "Assets/_Project/Data/PowerUps/PowerUp_Spread.asset";
        const string ShieldPath = "Assets/_Project/Data/PowerUps/PowerUp_Shield.asset";

        PickupDropper _dropper;

        IEnumerator LoadArenaWithDropper()
        {
            yield return LoadArena();
            _dropper = Object.FindAnyObjectByType<PickupDropper>();
        }

        [UnityTest]
        public IEnumerator KilledEnemy_DropsAPickup_AccordingToDropChance()
        {
            yield return LoadArenaWithDropper();

            arena.Events.Publish(new EnemyKilled(0, new Vector2(5f, 0f), dropChance: 0f));
            Assert.AreEqual(0, _dropper.ActiveCount);

            arena.Events.Publish(new EnemyKilled(0, new Vector2(5f, 0f), dropChance: 1f));
            Assert.AreEqual(1, _dropper.ActiveCount);
        }

        [UnityTest]
        public IEnumerator SpreadPickup_FiresFiveBulletsPerShot_AndShowsOnHud()
        {
            yield return LoadArenaWithDropper();
            var pickup = _dropper.Spawn(LoadAsset<PowerUp>(SpreadPath), player.transform.position);
            yield return PhysicsSteps();

            Assert.IsFalse(pickup.gameObject.activeSelf, "Pickup should be collected on touch.");
            Assert.IsTrue(player.GetComponent<PowerUpReceiver>().HasWeaponBoost);
            StringAssert.StartsWith("SPREAD", FindText("UI/HUD/PowerUps").text);

            AimAt(player.transform.position + Vector3.right * 5f);
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);

            Assert.AreEqual(5, Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length);
        }

        [UnityTest]
        public IEnumerator WeaponBoost_ExpiresBackToTheBaseWeapon()
        {
            yield return LoadArena();
            var weapon = player.GetComponent<Weapon>();
            var receiver = player.GetComponent<PowerUpReceiver>();
            var baseData = weapon.Data;
            var spread = ((WeaponPowerUp)LoadAsset<PowerUp>(SpreadPath)).Weapon;

            receiver.BoostWeapon(spread, 0.3f, "TEST");
            Assert.AreSame(spread, weapon.Data);

            yield return new WaitForSeconds(0.5f);
            Assert.AreSame(baseData, weapon.Data);
            Assert.IsFalse(receiver.HasWeaponBoost);
        }

        [UnityTest]
        public IEnumerator ShieldPickup_BlocksContactDamage()
        {
            yield return LoadArenaWithDropper();
            var health = player.GetComponent<Health>();
            _dropper.Spawn(LoadAsset<PowerUp>(ShieldPath), player.transform.position);
            yield return PhysicsSteps();

            Assert.IsTrue(player.transform.Find("Shield").gameObject.activeSelf, "Shield visual should show.");

            spawner.Spawn(chaserPrefab, player.transform.position + Vector3.right * 1.5f);
            yield return new WaitForSeconds(1f);

            Assert.AreEqual(health.Max, health.Current, "Shielded player took damage.");
        }
    }
}
#endif
