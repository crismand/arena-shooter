#if UNITY_EDITOR
using System.Collections;
using AS.Agents;
using AS.Core;
using AS.Player;
using AS.UI;
using NUnit.Framework;
using Unity.MLAgents.Policies;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AS.Tests
{
    /// <summary>Watch AI: the trained agent plays the real arena from the title screen until any button is pressed.</summary>
    public class WatchAITests : ArenaTestFixture
    {
        WatchMode _watch;
        ArenaAgent _agent;

        IEnumerator StartWatching(bool waves = false)
        {
            yield return LoadArena(stayOnTitle: true);
            _watch = Object.FindAnyObjectByType<WatchMode>();
            _agent = Object.FindAnyObjectByType<ArenaAgent>(FindObjectsInactive.Include);
            game.WatchAI();
            // After WatchAI, whose arena reset turns the director back on.
            director.enabled = waves;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheArenaScene_HasAnAgent_ThatOnlyPlaysTheShippedModel_AndIsOffUntilWatched()
        {
            yield return LoadArena(stayOnTitle: true);
            var agent = Object.FindAnyObjectByType<ArenaAgent>(FindObjectsInactive.Include);
            Assert.IsNotNull(agent);
            Assert.IsFalse(agent.gameObject.activeSelf);
            var parameters = agent.GetComponent<BehaviorParameters>();
            Assert.AreEqual(BehaviorType.InferenceOnly, parameters.BehaviorType);
            Assert.IsNotNull(parameters.Model);
            Assert.IsTrue(parameters.DeterministicInference);
            Assert.AreEqual(0, agent.MaxStep, "Watching shouldn't stop after a training episode's length.");
            Assert.IsInstanceOf<HumanPlayerInput>(player.InputSource);
        }

        [UnityTest]
        public IEnumerator TitleScreen_WatchAI_IsWiredToGameManager()
        {
            yield return LoadArena(stayOnTitle: true);
            var button = Object.FindAnyObjectByType<TitleScreen>().transform.Find("WatchAIButton").GetComponent<Button>();
            Assert.AreEqual(1, button.onClick.GetPersistentEventCount());
            Assert.AreSame(game, button.onClick.GetPersistentTarget(0));
            Assert.AreEqual(nameof(GameManager.WatchAI), button.onClick.GetPersistentMethodName(0));
        }

        [UnityTest]
        public IEnumerator WatchAI_HandsThePlayerToTheAgent()
        {
            yield return StartWatching();

            Assert.IsTrue(game.State.Is(GameState.Watching));
            Assert.IsFalse(Object.FindAnyObjectByType<TitleScreen>().IsVisible);
            Assert.IsTrue(Object.FindAnyObjectByType<WatchOverlay>().IsVisible);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsTrue(_watch.IsWatching);
            Assert.IsTrue(_agent.gameObject.activeSelf);
            Assert.IsInstanceOf<AgentInput>(player.InputSource);
            Assert.IsInstanceOf<AgentInput>(player.GetComponent<PlayerAim>().InputSource);
        }

        [UnityTest]
        public IEnumerator WatchAI_TheAgentPlays()
        {
            int shots = 0;
            void OnShot(ShotFired evt) { if (evt.ByPlayer) shots++; }
            EventBus.Subscribe<ShotFired>(OnShot);
            try
            {
                yield return StartWatching(waves: true);
                Vector3 start = player.transform.position;
                yield return new WaitForSeconds(3f);

                bool moved = Vector3.Distance(start, player.transform.position) > 0.5f;
                Assert.IsTrue(moved || shots > 0, "The agent neither moved nor fired in 3 seconds.");
            }
            finally
            {
                EventBus.Unsubscribe<ShotFired>(OnShot);
            }
        }

        [UnityTest]
        public IEnumerator AnyButton_AfterAMoment_ReturnsToTheTitle_AndGivesControlBack()
        {
            yield return StartWatching();

            // Straight away it's ignored, so the press that chose Watch AI can't end it.
            Press(keyboard.spaceKey);
            Release(keyboard.spaceKey);
            Assert.IsTrue(game.State.Is(GameState.Watching));

            yield return new WaitForSecondsRealtime(0.6f);
            Press(keyboard.spaceKey);
            Release(keyboard.spaceKey);
            yield return null;

            Assert.IsTrue(game.State.Is(GameState.Title));
            Assert.IsTrue(Object.FindAnyObjectByType<TitleScreen>().IsVisible);
            Assert.IsFalse(Object.FindAnyObjectByType<WatchOverlay>().IsVisible);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsFalse(_agent.gameObject.activeSelf);
            Assert.IsInstanceOf<HumanPlayerInput>(player.InputSource);
            Assert.IsInstanceOf<HumanPlayerInput>(player.GetComponent<PlayerAim>().InputSource);

            // And the next run is the person's.
            game.Play();
            director.enabled = false;
            yield return null;
            float startX = player.transform.position.x;
            Press(keyboard.dKey);
            yield return new WaitForSeconds(0.3f);
            Release(keyboard.dKey);
            Assert.Greater(player.transform.position.x, startX + 0.5f, "Player can't move after watching.");
        }

        [UnityTest]
        public IEnumerator Dying_WhileWatching_StartsAgain_WithoutAGameOverOrAHighScore()
        {
            yield return StartWatching();
            arena.Events.Publish(new EnemyKilled(100, Vector2.zero, 0f));
            Assert.AreEqual(100, game.Score, "Kills score while watching.");

            var health = player.GetComponent<AS.Combat.Health>();
            health.TakeDamage(new AS.Combat.DamageInfo(health.Max, Vector2.zero, null));
            yield return PhysicsSteps(3);

            Assert.IsTrue(game.State.Is(GameState.Watching));
            Assert.AreEqual(0, game.Score);
            Assert.AreEqual(0, HighScoreStore.Load(), "Watching saved a high score.");
            Assert.IsTrue(player.gameObject.activeSelf, "The agent should start a new go.");
            Assert.AreEqual(health.Max, health.Current);
        }
    }
}
#endif
