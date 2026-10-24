using System.Collections.Generic;
using System.Linq;
using AS.Combat;
using AS.Enemies;
using AS.Feedback;
using AS.Pickups;
using AS.Waves;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AS.Tests
{
    /// <summary>
    /// Checks the project's data assets and prefabs are wired up: the kind of mistake that compiles
    /// fine but breaks in play (an empty slot, a zero, a missing scene). One test case per asset, so a
    /// failure names the asset.
    /// </summary>
    public class AssetValidationTests
    {
        const string Root = "Assets/_Project";

        static IEnumerable<T> Assets<T>(string folder) where T : Object =>
            AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { $"{Root}/{folder}" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(a => a != null);

        static IEnumerable<TestCaseData> Cases<T>(IEnumerable<T> assets) where T : Object =>
            assets.Select(a => new TestCaseData(a).SetName($"{{m}}({a.name})"));

        static IEnumerable<T> PrefabsWith<T>(string folder) where T : Component =>
            Assets<GameObject>(folder).Select(go => go.GetComponent<T>()).Where(c => c != null);

        static IEnumerable<TestCaseData> Waves() => Cases(Assets<WaveDefinition>("Data/Waves"));
        static IEnumerable<TestCaseData> Weapons() => Cases(Assets<WeaponData>("Data/Weapons"));
        static IEnumerable<TestCaseData> PowerUps() => Cases(Assets<PowerUp>("Data/PowerUps"));
        static IEnumerable<TestCaseData> Enemies() => Cases(PrefabsWith<EnemyBase>("Prefabs/Enemies"));
        static IEnumerable<TestCaseData> Effects() => Cases(PrefabsWith<PooledEffect>("Prefabs/Effects"));

        [Test]
        public void TheExpectedAssetsExist()
        {
            Assert.AreEqual(4, Assets<WaveDefinition>("Data/Waves").Count(), "waves");
            Assert.AreEqual(3, PrefabsWith<EnemyBase>("Prefabs/Enemies").Count(), "enemy prefabs");
            Assert.AreEqual(3, Assets<PowerUp>("Data/PowerUps").Count(), "power-ups");
            Assert.AreEqual(5, PrefabsWith<PooledEffect>("Prefabs/Effects").Count(), "effect prefabs");
        }

        [TestCaseSource(nameof(Waves))]
        public void Wave_HasEnemies_AndEveryGroupIsFilledIn(WaveDefinition wave)
        {
            Assert.IsNotEmpty(wave.Groups);
            Assert.Greater(wave.SpawnInterval, 0f);
            foreach (var group in wave.Groups)
            {
                Assert.IsNotNull(group.Enemy, "A spawn group has no enemy prefab.");
                Assert.GreaterOrEqual(group.Count, 1);
            }
        }

        [TestCaseSource(nameof(Enemies))]
        public void EnemyPrefab_HasData_WithSensibleStats(EnemyBase enemy)
        {
            Assert.IsNotNull(enemy.Data, "No EnemyData assigned.");
            Assert.Greater(enemy.Data.MaxHealth, 0f);
            Assert.Greater(enemy.Data.MoveSpeed, 0f);
            Assert.Greater(enemy.Data.ScoreValue, 0);
            Assert.IsNotNull(enemy.GetComponent<Rigidbody2D>());
            Assert.IsNotNull(enemy.GetComponent<Collider2D>());
        }

        [TestCaseSource(nameof(Weapons))]
        public void Weapon_HasAProjectile_AndCanFire(WeaponData weapon)
        {
            Assert.IsNotNull(weapon.ProjectilePrefab, "No projectile prefab.");
            Assert.Greater(weapon.FireRate, 0f);
            Assert.Greater(weapon.ProjectileSpeed, 0f);
            Assert.Greater(weapon.Damage, 0f);
        }

        [TestCaseSource(nameof(PowerUps))]
        public void PowerUp_HasALabel_AndADuration(PowerUp powerUp)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(powerUp.Label));
            Assert.Greater(powerUp.Duration, 0f);
            if (powerUp is WeaponPowerUp weaponPowerUp)
                Assert.IsNotNull(weaponPowerUp.Weapon, "Weapon power-up has no WeaponData.");
        }

        [TestCaseSource(nameof(Effects))]
        public void EffectPrefab_IsAOneShotBurst_ThatReturnsToItsPool(PooledEffect effect)
        {
            var particles = effect.GetComponent<ParticleSystem>();
            Assert.IsFalse(particles.main.loop, "A looping burst would never finish and never return to its pool.");
            Assert.IsFalse(particles.main.playOnAwake);
            Assert.AreEqual(ParticleSystemStopAction.Callback, particles.main.stopAction);
            Assert.IsNotNull(effect.GetComponent<ParticleSystemRenderer>().sharedMaterial, "No material: it would render pink.");
        }

        [Test]
        public void BuildSettings_StartWithTheArenaScene()
        {
            var enabled = EditorBuildSettings.scenes.Where(s => s.enabled).ToArray();
            Assert.IsNotEmpty(enabled);
            Assert.AreEqual($"{Root}/Scenes/Arena.unity", enabled[0].path);
        }

        [Test]
        public void AllSoundsExist()
        {
            foreach (string name in new[] { "PlayerShot", "EnemyShot", "EnemyHit", "EnemyDeath", "PlayerHurt", "PlayerDeath", "Pickup", "WaveStarted", "WaveCleared" })
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/Audio/SFX/{name}.wav"), name);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/Audio/Music/ArenaLoop.wav"), "music");
        }
    }
}
