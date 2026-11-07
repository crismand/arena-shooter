#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using AS.Agents;
using AS.Combat;
using AS.Core;
using AS.Enemies;
using AS.Feedback;
using AS.Pickups;
using AS.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AS.Tests
{
    /// <summary>
    /// The Training scene the agent learns in. It isn't in the build settings, so it's loaded by path.
    /// With no trainer connected, the agent acts from its heuristic (random by default).
    /// </summary>
    public class TrainingSceneTests
    {
        const string TrainingScenePath = "Assets/_Project/Scenes/Training.unity";

        Arena _arena;
        ArenaAgent _agent;
        PlayerController _player;
        EnemySpawner _spawner;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(TrainingScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            _arena = Object.FindAnyObjectByType<Arena>();
            _agent = Object.FindAnyObjectByType<ArenaAgent>();
            _player = Object.FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
            _spawner = Object.FindAnyObjectByType<EnemySpawner>();
            yield return null;
        }

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

        [Test]
        public void TheScene_HasAnAgentDrivenPlayer_AndNothingThatSetsTheTimeScale()
        {
            Assert.IsNotNull(_agent);
            Assert.IsNotNull(_player.GetComponent<AgentInput>());
            Assert.IsNull(_player.GetComponent<HumanPlayerInput>());
            Assert.IsNull(Object.FindAnyObjectByType<GameManager>(), "GameManager stops time on game over.");
            Assert.IsNull(Object.FindAnyObjectByType<HitStop>(), "Hit-stop freezes time on hits.");
        }

        // One slot per kind, so the agent can tell every enemy and pickup apart.
        [UnityTest]
        public IEnumerator TheAgent_TellsEveryEnemyAndPickupKindApart()
        {
            int kinds = ArenaAgent.EnemyKindCount + ArenaAgent.PickupKindCount;
            var seen = new System.Collections.Generic.HashSet<int>();

            void Check(float[] observed, string name)
            {
                var hot = Enumerable.Range(0, kinds).Where(i => observed[i] == 1f).ToArray();
                Assert.AreEqual(1, hot.Length, $"{name} should fill exactly one kind slot.");
                Assert.IsTrue(seen.Add(hot[0]), $"{name} shares a kind slot.");
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs/Enemies" }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<EnemyBase>(AssetDatabase.GUIDToAssetPath(guid));
                var enemy = _spawner.Spawn(prefab, new Vector2(12f, 8f));
                Check(_agent.Observe(enemy), prefab.name);
            }
            var dropper = Object.FindAnyObjectByType<PickupDropper>();
            foreach (var guid in AssetDatabase.FindAssets("t:PowerUp", new[] { "Assets/_Project/Data/PowerUps" }))
            {
                var powerUp = AssetDatabase.LoadAssetAtPath<PowerUp>(AssetDatabase.GUIDToAssetPath(guid));
                Check(_agent.Observe(dropper.Spawn(powerUp, new Vector2(-12f, 8f))), powerUp.name);
            }
            Assert.AreEqual(kinds, seen.Count, "Every kind slot should be used.");
            yield return null;
        }

        // Training starts episodes at later waves; everything else resets to wave 1.
        [UnityTest]
        public IEnumerator ResettingTheArena_StartsAtWave1_OrAtTheWaveAskedFor()
        {
            int started = 0;
            bool boss = false;
            void OnStarted(WaveStarted e) { if (started == 0) { started = e.Number; boss = e.IsBoss; } }
            _arena.Events.Subscribe<WaveStarted>(OnStarted);
            try
            {
                foreach (var (asked, expected) in new[] { (0, 1), (5, 5) })
                {
                    started = 0;
                    if (asked == 0) _arena.ResetArena(); else _arena.ResetArena(asked);
                    float giveUpAt = Time.time + 3f;
                    while (started == 0 && Time.time < giveUpAt) yield return null;
                    Assert.AreEqual(expected, started);
                    Assert.AreEqual(expected == 5, boss, "Wave 5 is a boss wave.");
                }
            }
            finally { _arena.Events.Unsubscribe<WaveStarted>(OnStarted); }
        }

        // The bullet sensor's list: a Shooter's bullets join it when fired and leave it on a reset.
        [UnityTest]
        public IEnumerator EnemyBullets_AreListedForTheAgent_UntilTheArenaResets()
        {
            var shooter = AssetDatabase.LoadAssetAtPath<EnemyBase>("Assets/_Project/Prefabs/Enemies/Shooter.prefab");
            _spawner.Spawn(shooter, new Vector2(12f, 8f));
            var live = new System.Collections.Generic.List<Projectile>();
            float giveUpAt = Time.time + 5f;
            while (Time.time < giveUpAt)
            {
                Projectile.GetLive(_arena, live);
                if (live.Any(p => p.IsEnemyShot)) break;
                yield return null;
            }
            Assert.IsTrue(live.Any(p => p.IsEnemyShot), "The Shooter's bullets should be listed.");

            _arena.ResetArena();
            yield return null;
            Projectile.GetLive(_arena, live);
            Assert.IsFalse(live.Any(p => p.IsEnemyShot), "A reset should clear the list.");
        }

        [Test]
        public void ClearingAWave_Pays_AndABossWavePaysMore()
        {
            float before = _agent.GetCumulativeReward();
            _arena.Events.Publish(new WaveStarted(4, 18));
            _arena.Events.Publish(new WaveCleared(4));
            Assert.AreEqual(before + 3f, _agent.GetCumulativeReward(), 1e-4f);

            _arena.Events.Publish(new WaveStarted(5, 1, isBoss: true));
            _arena.Events.Publish(new WaveCleared(5));
            Assert.AreEqual(before + 9f, _agent.GetCumulativeReward(), 1e-4f);
        }

        [Test]
        public void CollectingAPickup_Pays()
        {
            float before = _agent.GetCumulativeReward();
            _arena.Events.Publish(new PickupCollected(Vector2.zero, Color.white));
            Assert.AreEqual(before + 0.1f, _agent.GetCumulativeReward(), 1e-4f);
        }

        // A Tank in a far corner: too tough to kill and too slow to reach the player in a few steps.
        [UnityTest]
        public IEnumerator EachStep_WithEnemiesLeft_CostsALittle()
        {
            var tank = AssetDatabase.LoadAssetAtPath<EnemyBase>("Assets/_Project/Prefabs/Enemies/Tank.prefab");
            _spawner.Spawn(tank, new Vector2(15f, 9f));
            float before = _agent.GetCumulativeReward();
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
            Assert.Less(_agent.GetCumulativeReward(), before);
        }

        [Test]
        public void EveryWall_IsTaggedForTheSensor()
        {
            var walls = Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None)
                .Where(c => c.gameObject.layer == LayerMask.NameToLayer(Layers.Wall)).ToArray();
            Assert.IsNotEmpty(walls);
            foreach (var wall in walls) Assert.AreEqual(Tags.Wall, wall.tag, wall.name);
        }

        [UnityTest]
        public IEnumerator TheTrainingArena_DoesNotForwardToTheGlobalBus()
        {
            int heard = 0;
            void OnWave(WaveStarted _) => heard++;
            EventBus.Subscribe<WaveStarted>(OnWave);
            try
            {
                _arena.Events.Publish(new WaveStarted(1, 1));
                yield return null;
                Assert.AreEqual(0, heard);
            }
            finally
            {
                EventBus.Unsubscribe<WaveStarted>(OnWave);
            }
        }

        [UnityTest]
        public IEnumerator TheAgent_FollowsThePlayer()
        {
            _player.GetComponent<Rigidbody2D>().position = new Vector2(5f, -3f);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.Less(Vector2.Distance(_agent.transform.position, _player.transform.position), 0.5f);
        }

        [UnityTest]
        public IEnumerator Dying_EndsTheEpisode_AndLeavesTheArenaClean()
        {
            var chaser = AssetDatabase.LoadAssetAtPath<Chaser>("Assets/_Project/Prefabs/Enemies/Chaser.prefab");
            for (int i = 0; i < 3; i++) _spawner.TrySpawn(chaser);
            Assert.Greater(_spawner.ActiveCount, 0);
            int episodes = _agent.CompletedEpisodes;
            var health = _player.GetComponent<Health>();

            health.TakeDamage(new DamageInfo(health.Max, Vector2.zero, null));
            Assert.IsFalse(_player.gameObject.activeSelf, "The player should be down until the episode ends.");

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.AreEqual(episodes + 1, _agent.CompletedEpisodes);
            Assert.IsTrue(_player.gameObject.activeSelf, "The reset should bring the player back.");
            Assert.AreEqual(health.Max, health.Current);
            Assert.AreEqual(0, _spawner.ActiveCount);
        }
    }
}
#endif
