#if UNITY_EDITOR
using System.Collections;
using AS.Combat;
using AS.Enemies;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AS.Tests
{
    /// <summary>Shooter and Tank behaviour.</summary>
    public class EnemyVarietyTests : ArenaTestFixture
    {
        [UnityTest]
        public IEnumerator Shooter_BacksOff_WhenThePlayerIsTooClose()
        {
            yield return LoadArena();
            var shooter = spawner.Spawn(LoadAsset<Shooter>(ShooterPrefabPath), player.transform.position + Vector3.right * 3f);

            yield return new WaitForSeconds(1.5f);

            float distance = Vector2.Distance(shooter.transform.position, player.transform.position);
            Assert.Greater(distance, 5f, "Shooter should retreat toward its preferred range (7).");
        }

        [UnityTest]
        public IEnumerator Shooter_FiresAndHitsAStationaryPlayer()
        {
            yield return LoadArena();
            var health = player.GetComponent<Health>();
            spawner.Spawn(LoadAsset<Shooter>(ShooterPrefabPath), player.transform.position + Vector3.up * 6f);

            // 1 s before the first shot, then ~0.7 s of bullet travel.
            float timeout = Time.time + 3.5f;
            while (health.Current == health.Max && Time.time < timeout) yield return null;

            Assert.Less(health.Current, health.Max, "Player never got hit by Shooter fire.");
        }

        [UnityTest]
        public IEnumerator Tank_SurvivesDamageThatKillsAChaser_AndIsSlower()
        {
            yield return LoadArena();
            var tank = spawner.Spawn(LoadAsset<Chaser>(TankPrefabPath), new Vector2(10f, 0f));
            var chaser = spawner.Spawn(chaserPrefab, new Vector2(-10f, 0f));

            var lethalForChaser = new DamageInfo(chaserPrefab.Data.MaxHealth, Vector2.zero, null);
            chaser.Health.TakeDamage(lethalForChaser);
            tank.Health.TakeDamage(lethalForChaser);
            yield return null;

            Assert.IsFalse(chaser.gameObject.activeSelf);
            Assert.IsTrue(tank.gameObject.activeSelf);
            Assert.Less(tank.Data.MoveSpeed, chaserPrefab.Data.MoveSpeed);
        }
    }
}
#endif
