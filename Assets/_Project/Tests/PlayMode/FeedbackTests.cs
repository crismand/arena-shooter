#if UNITY_EDITOR
using System.Collections;
using AS.Combat;
using AS.Core;
using AS.Feedback;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AS.Tests
{
    /// <summary>Step 5: hit-stop, particles, sound and the saved high score.</summary>
    public class FeedbackTests : ArenaTestFixture
    {
        HitStop hitStop;
        ParticleEffects effects;
        SoundEffects sounds;

        IEnumerator LoadArenaWithFeedback()
        {
            yield return LoadArena();
            hitStop = Object.FindAnyObjectByType<HitStop>();
            effects = Object.FindAnyObjectByType<ParticleEffects>();
            sounds = Object.FindAnyObjectByType<SoundEffects>();
        }

        void HurtPlayer(float amount = 1f) =>
            player.GetComponent<Health>().TakeDamage(new DamageInfo(amount, Vector2.right, null));

        // ------------------------------------------------------------------ hit-stop

        [UnityTest]
        public IEnumerator PlayerHit_FreezesTimeBriefly_ThenResumes()
        {
            yield return LoadArenaWithFeedback();

            HurtPlayer();
            Assert.IsTrue(hitStop.IsFrozen);
            Assert.AreEqual(0f, Time.timeScale);

            yield return new WaitForSecondsRealtime(0.2f);
            Assert.IsFalse(hitStop.IsFrozen);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator PausingDuringHitStop_StaysPaused_UntilResumed()
        {
            yield return LoadArenaWithFeedback();

            HurtPlayer();
            game.SetPaused(true);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.AreEqual(0f, Time.timeScale, "Hit-stop ending must not unpause the game.");
            Assert.IsTrue(game.State.Is(GameState.Paused));

            game.Resume();
            Assert.AreEqual(1f, Time.timeScale);
        }

        // ------------------------------------------------------------------ particles and sound

        [UnityTest]
        public IEnumerator KillingAnEnemy_PlaysDeathBurst_AndSound()
        {
            yield return LoadArenaWithFeedback();
            var chaser = spawner.Spawn(chaserPrefab, player.transform.position + Vector3.right * 6f);

            chaser.Health.TakeDamage(new DamageInfo(1000f, Vector2.right, null));

            Assert.IsFalse(chaser.gameObject.activeSelf);
            Assert.AreEqual(1, effects.ActiveCount);
            Assert.AreEqual("EnemyDeath", sounds.LastClip.name);

            // The burst returns itself to the pool once its particles have died.
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(0, effects.ActiveCount);
        }

        [UnityTest]
        public IEnumerator Scene_HasExactlyOneAudioListener_OnTheMainCamera()
        {
            yield return LoadArenaWithFeedback();

            var listeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            Assert.AreEqual(1, listeners.Length, "Without a listener nothing is audible; with two Unity warns every frame.");
            Assert.AreSame(Camera.main.gameObject, listeners[0].gameObject);
        }

        [UnityTest]
        public IEnumerator Firing_PlaysShotSound()
        {
            yield return LoadArenaWithFeedback();
            AimAt(player.transform.position + Vector3.right * 3f);
            yield return null;

            Press(mouse.leftButton);
            yield return null;
            yield return null;
            Release(mouse.leftButton);

            Assert.IsNotNull(sounds.LastClip);
            Assert.AreEqual("PlayerShot", sounds.LastClip.name);
        }

        // ------------------------------------------------------------------ high score

        [UnityTest]
        public IEnumerator BeatingTheHighScore_SavesIt_AndSaysNewBest()
        {
            HighScoreStore.TrySubmit(20);
            yield return LoadArenaWithFeedback();
            Assert.AreEqual("BEST 20", FindText("UI/HUD/Best").text);

            EventBus.Publish(new EnemyKilled(30, new Vector2(5f, 0f), dropChance: 0f));
            Assert.AreEqual("BEST 30", FindText("UI/HUD/Best").text, "The HUD best climbs live once passed.");

            HurtPlayer(1000f);
            Assert.IsTrue(game.State.Is(GameState.GameOver));
            Assert.AreEqual(30, HighScoreStore.Load());
            Assert.AreEqual(30, game.HighScore);
            StringAssert.Contains("NEW BEST!", FindText("UI/GameOverScreen/Summary").text);
        }

        [UnityTest]
        public IEnumerator AWorseRun_KeepsTheOldHighScore()
        {
            HighScoreStore.TrySubmit(100);
            yield return LoadArenaWithFeedback();

            EventBus.Publish(new EnemyKilled(10, new Vector2(5f, 0f), dropChance: 0f));
            HurtPlayer(1000f);

            Assert.AreEqual(100, HighScoreStore.Load());
            Assert.AreEqual("BEST 100", FindText("UI/HUD/Best").text);
            StringAssert.Contains("Best 100", FindText("UI/GameOverScreen/Summary").text);
            StringAssert.DoesNotContain("NEW BEST", FindText("UI/GameOverScreen/Summary").text);
        }
    }
}
#endif
