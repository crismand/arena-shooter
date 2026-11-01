#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using AS.Combat;
using AS.Enemies;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AS.Tests
{
    /// <summary>Shooter, Tank, Dasher and Splitter behaviour.</summary>
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

        [UnityTest]
        public IEnumerator Dasher_WindsUpStandingStill_ThenChargesAtWhereThePlayerWas()
        {
            yield return LoadArena();
            var dasher = (Dasher)spawner.Spawn(LoadAsset<Dasher>(DasherPrefabPath), player.transform.position + Vector3.right * 6f);
            var body = dasher.GetComponent<Rigidbody2D>();

            yield return WaitForPhase(dasher, Dasher.Phase.WindUp, 3f);
            yield return new WaitForFixedUpdate();
            Assert.Less(body.linearVelocity.magnitude, 0.01f, "It should stand still while winding up.");
            Assert.IsTrue(dasher.transform.Find("Visual/Telegraph").gameObject.activeSelf, "The charge should be telegraphed.");

            yield return WaitForPhase(dasher, Dasher.Phase.Dash, 2f);
            yield return new WaitForFixedUpdate();
            Assert.Greater(body.linearVelocity.magnitude, 10f, "A charge should be much faster than walking.");
            Assert.Greater(Vector2.Dot(body.linearVelocity.normalized, Vector2.left), 0.95f, "It should charge straight at the player.");
            Assert.IsFalse(dasher.transform.Find("Visual/Telegraph").gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator Dasher_Misses_APlayerWhoSidestepsDuringTheWindUp()
        {
            yield return LoadArena();
            var health = player.GetComponent<Health>();
            var dasher = (Dasher)spawner.Spawn(LoadAsset<Dasher>(DasherPrefabPath), player.transform.position + Vector3.right * 6f);

            yield return WaitForPhase(dasher, Dasher.Phase.Dash, 3f);
            // Sidestep the moment the charge starts: its direction is already locked in.
            var playerBody = player.GetComponent<Rigidbody2D>();
            playerBody.position += Vector2.up * 4f;
            player.transform.position = playerBody.position;
            yield return WaitForPhase(dasher, Dasher.Phase.Recover, 2f);

            Assert.AreEqual(health.Max, health.Current, "The charge should have gone straight past.");
            Assert.Less(Mathf.Abs(dasher.transform.position.y - (playerBody.position.y - 4f)), 0.5f, "It should not steer mid-charge.");
        }

        [UnityTest]
        public IEnumerator Splitter_BreaksIntoThreeSplitlings_ThatKeepTheWaveGoing()
        {
            yield return LoadArena();
            var splitterPrefab = LoadAsset<Splitter>(SplitterPrefabPath);
            var splitter = spawner.Spawn(splitterPrefab, new Vector2(10f, 0f));
            Assert.AreEqual(1, spawner.ActiveCount);

            splitter.Health.TakeDamage(new DamageInfo(splitter.Data.MaxHealth, Vector2.zero, null));
            yield return null;

            Assert.IsFalse(splitter.gameObject.activeSelf);
            var children = Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None)
                .Where(e => e.Data == splitterPrefab.ChildPrefab.Data).ToArray();
            Assert.AreEqual(3, children.Length, "Splitlings alive");
            Assert.AreEqual(3, spawner.ActiveCount, "The spawner should count the Splitlings, so the wave waits for them.");
            foreach (var child in children)
                Assert.Less(Vector2.Distance(child.transform.position, new Vector2(10f, 0f)), 2f, "Splitlings appear where the Splitter died.");
        }

        static IEnumerator WaitForPhase(Dasher dasher, Dasher.Phase phase, float timeout)
        {
            float giveUpAt = Time.time + timeout;
            while (dasher.Current != phase && Time.time < giveUpAt) yield return new WaitForFixedUpdate();
            Assert.AreEqual(phase, dasher.Current, $"The Dasher never reached {phase}.");
        }
    }
}
#endif
