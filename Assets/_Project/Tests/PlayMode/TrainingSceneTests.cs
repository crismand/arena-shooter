#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using AS.Agents;
using AS.Combat;
using AS.Core;
using AS.Enemies;
using AS.Feedback;
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
