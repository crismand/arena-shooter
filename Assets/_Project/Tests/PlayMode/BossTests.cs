#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using AS.Combat;
using AS.Core;
using AS.Enemies;
using AS.Pickups;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace AS.Tests
{
    /// <summary>The boss: its attacks, phases, health bar, drop, and the boss wave.</summary>
    public class BossTests : ArenaTestFixture
    {
        const string BossPrefabPath = "Assets/_Project/Prefabs/Enemies/Boss.prefab";

        Boss SpawnBoss(Vector2 position) => (Boss)spawner.Spawn(LoadAsset<Boss>(BossPrefabPath), position);

        static void Hurt(EnemyBase enemy, float fraction) =>
            enemy.Health.TakeDamage(new DamageInfo(enemy.Health.Max * fraction, Vector2.zero, null));

        static int EnemyBulletsInFlight() =>
            Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).Count(p => p.gameObject.layer == LayerMask.NameToLayer(Layers.EnemyShot));

        [UnityTest]
        public IEnumerator Boss_FiresARingOfBullets()
        {
            yield return LoadArena();
            SpawnBoss(player.transform.position + Vector3.right * 7f);

            // First attack after 1.5 s.
            yield return new WaitForSeconds(1.8f);

            Assert.GreaterOrEqual(EnemyBulletsInFlight(), 16, "A full ring should be in the air.");
        }

        [UnityTest]
        public IEnumerator Boss_BelowHalfHealth_EntersPhaseTwo_AndSummonsChasers()
        {
            yield return LoadArena();
            var boss = SpawnBoss(new Vector2(10f, 0f));
            yield return null;
            Assert.AreEqual(Boss.Phase.One, boss.Current);

            Hurt(boss, 0.55f);
            yield return null;
            yield return null;

            Assert.AreEqual(Boss.Phase.Two, boss.Current);
            Assert.AreEqual(3, spawner.ActiveCount, "Two Chasers should be summoned as phase two starts, and count toward the wave.");
        }

        // Hurt into phase two straight away: the bursts start 0.5 s later, the first ring not until 1.5 s.
        [UnityTest]
        public IEnumerator PhaseTwo_FiresAimedShots_OnlyForLaterBosses([Values(false, true)] bool aimedShots)
        {
            yield return LoadArena();
            var boss = SpawnBoss(player.transform.position + Vector3.right * 7f);
            boss.AimedShots = aimedShots;
            Hurt(boss, 0.55f);

            yield return new WaitForSeconds(1f);

            if (aimedShots) Assert.AreEqual(3, EnemyBulletsInFlight(), "One three-way burst.");
            else Assert.AreEqual(0, EnemyBulletsInFlight(), "The first boss doesn't aim.");
        }

        [UnityTest]
        public IEnumerator BossBar_FollowsItsHealth_AndItAlwaysDropsAPickup()
        {
            yield return LoadArena();
            var bar = GameObject.Find("UI/HUD").transform.Find("BossBar").gameObject;
            Assert.IsFalse(bar.activeSelf, "No boss, no bar.");

            var boss = SpawnBoss(new Vector2(10f, 0f));
            yield return null;
            Assert.IsTrue(bar.activeSelf);
            Assert.AreEqual(1f, FindImage("UI/HUD/BossBar/BossFill").fillAmount, 0.001f);

            Hurt(boss, 0.25f);
            yield return null;
            Assert.AreEqual(0.75f, FindImage("UI/HUD/BossBar/BossFill").fillAmount, 0.001f);

            Hurt(boss, 1f);
            yield return null;
            Assert.IsFalse(bar.activeSelf, "The bar should go when the boss dies.");
            Assert.GreaterOrEqual(Object.FindAnyObjectByType<PickupDropper>().ActiveCount, 1, "A boss always drops a pickup.");
        }

        [UnityTest]
        public IEnumerator BossWave_SpawnsTheBoss_WithAWarning()
        {
            yield return LoadArena();
            // Every wave a boss wave, so wave 1 is one.
            var so = new SerializedObject(director);
            so.FindProperty("bossEvery").intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();
            director.enabled = true;

            // The first wave starts after 1 s.
            float giveUpAt = Time.time + 3f;
            Boss boss = null;
            while (boss == null && Time.time < giveUpAt)
            {
                yield return null;
                boss = Object.FindAnyObjectByType<Boss>();
            }

            Assert.IsNotNull(boss, "No boss spawned on the boss wave.");
            Assert.AreEqual("WARNING: BOSS", FindText("UI/HUD/Banner").text);
            Assert.AreEqual(1, spawner.ActiveCount, "A boss wave is just the boss.");
        }
    }
}
#endif
