#if UNITY_EDITOR
using System.Collections;
using AS.Combat;
using AS.Core;
using AS.Enemies;
using AS.Pickups;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AS.Tests
{
    /// <summary>Pickups, drops and the power-up effects.</summary>
    public class PowerUpTests : ArenaTestFixture
    {
        const string SpreadPath = "Assets/_Project/Data/PowerUps/PowerUp_Spread.asset";
        const string ShieldPath = "Assets/_Project/Data/PowerUps/PowerUp_Shield.asset";
        const string HealthPath = "Assets/_Project/Data/PowerUps/PowerUp_Health.asset";
        const string BombPath = "Assets/_Project/Data/PowerUps/PowerUp_Bomb.asset";
        const string HomingPath = "Assets/_Project/Data/PowerUps/PowerUp_Homing.asset";
        const string EnemyBulletPath = "Assets/_Project/Prefabs/Projectiles/EnemyBullet.prefab";

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

        [UnityTest]
        public IEnumerator HealthPickup_RestoresOnePoint_AndUpdatesTheHealthBar()
        {
            yield return LoadArenaWithDropper();
            var health = player.GetComponent<Health>();
            health.TakeDamage(new DamageInfo(1f, Vector2.zero, null));
            Assert.Less(health.Current, health.Max);

            _dropper.Spawn(LoadAsset<PowerUp>(HealthPath), player.transform.position);
            yield return PhysicsSteps();

            Assert.AreEqual(health.Max, health.Current);
            Assert.AreEqual(1f, FindImage("UI/HUD/HealthBarBg/HealthFill").fillAmount, 0.001f);
            Assert.AreEqual("", FindText("UI/HUD/PowerUps").text, "Instant power-ups have no timer to show.");
            var popup = FindText("UI/HUD/PickupPopup");
            Assert.IsTrue(popup.enabled, "The pickup's name should pop up.");
            Assert.AreEqual("HEALTH", popup.text);
        }

        [UnityTest]
        public IEnumerator BombPickup_KillsWeakEnemies_HurtsTanks_AndClearsEnemyBullets()
        {
            yield return LoadArenaWithDropper();
            var chaserA = spawner.Spawn(chaserPrefab, new Vector2(-12f, 8f));
            var chaserB = spawner.Spawn(chaserPrefab, new Vector2(12f, -8f));
            var tank = spawner.Spawn(LoadAsset<EnemyBase>(TankPrefabPath), new Vector2(12f, 8f));

            // A bullet heading away from the player, so only the bomb can remove it early.
            var bullet = Object.Instantiate(LoadAsset<Projectile>(EnemyBulletPath), new Vector2(-6f, -6f), Quaternion.identity, arena.transform);
            bullet.Launch(new Vector2(-1f, -1f).normalized, 1f, 10f, new DamageInfo(1f, Vector2.zero, null), null);
            yield return PhysicsSteps();

            _dropper.Spawn(LoadAsset<PowerUp>(BombPath), player.transform.position);
            yield return PhysicsSteps();

            Assert.IsFalse(chaserA.gameObject.activeSelf, "Chasers should die.");
            Assert.IsFalse(chaserB.gameObject.activeSelf, "Even in the far corner.");
            Assert.IsTrue(tank.gameObject.activeSelf, "A Tank survives a bomb...");
            Assert.Less(tank.Health.Current, tank.Health.Max, "...but takes the damage.");
            Assert.IsTrue(bullet == null, "Enemy bullets should be cleared.");
            Assert.IsTrue(FindImage("UI/HUD/BombFlash").enabled, "The screen should flash.");
        }

        [UnityTest]
        public IEnumerator HomingShots_CurveIntoAnEnemyOffToTheSide()
        {
            yield return LoadArenaWithDropper();
            var tank = spawner.Spawn(LoadAsset<EnemyBase>(TankPrefabPath), player.transform.position + new Vector3(6f, 4f));
            _dropper.Spawn(LoadAsset<PowerUp>(HomingPath), player.transform.position);
            yield return PhysicsSteps();
            StringAssert.StartsWith("HOMING", FindText("UI/HUD/PowerUps").text);

            // Aimed straight right: a plain bullet passes well below the Tank.
            AimAt(player.transform.position + Vector3.right * 5f);
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);

            float giveUpAt = Time.time + 1.5f;
            while (tank.Health.Current == tank.Health.Max && Time.time < giveUpAt) yield return null;
            Assert.Less(tank.Health.Current, tank.Health.Max, "The homing shot never reached the Tank.");
        }
    }
}
#endif
