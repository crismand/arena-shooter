#if UNITY_EDITOR
using System.Collections;
using AS.Combat;
using AS.Core;
using AS.Enemies;
using AS.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AS.Tests
{
    /// <summary>Player controls, scoring, waves and the pause / game-over flow.</summary>
    public class PlayerTests : ArenaTestFixture
    {
        // ------------------------------------------------------------------ player

        [UnityTest]
        public IEnumerator HoldingD_MovesPlayerRight()
        {
            yield return LoadArena();
            float startX = player.transform.position.x;

            Press(keyboard.dKey);
            yield return new WaitForSeconds(0.5f);
            Release(keyboard.dKey);

            Assert.Greater(player.transform.position.x, startX + 1f);
        }

        /// <summary>CI runs a headless editor that never has focus; simulated input must still arrive.</summary>
        [UnityTest]
        public IEnumerator KeyboardInput_StillArrives_WhenTheEditorHasNoFocus()
        {
            yield return LoadArena();
            ScheduleFocusChangedEvent(applicationHasFocus: false);
            yield return null;
            float startX = player.transform.position.x;

            Press(keyboard.dKey);
            yield return new WaitForSeconds(0.5f);
            Release(keyboard.dKey);

            Assert.Greater(player.transform.position.x, startX + 1f);
        }

        [UnityTest]
        public IEnumerator HoldingFire_ShootsTowardMouse_AndWallsDespawnBullets()
        {
            yield return LoadArena();
            AimAt(player.transform.position + Vector3.right * 5f);
            yield return null;

            Press(mouse.leftButton);
            yield return new WaitForSeconds(0.3f);

            var bullets = Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None);
            Assert.Greater(bullets.Length, 0, "No projectiles in flight while fire is held.");
            foreach (var bullet in bullets)
                Assert.Greater(bullet.GetComponent<Rigidbody2D>().linearVelocity.x, 10f, "Projectile not travelling toward the mouse.");

            Release(mouse.leftButton);
            // Shorter than the blaster's 1.5 s lifetime, so only a wall hit can have removed them.
            yield return new WaitForSeconds(1.2f);
            Assert.AreEqual(0, Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length);
        }

        // ------------------------------------------------------------------ enemies and score

        [UnityTest]
        public IEnumerator ShootingChaser_KillsIt_AndScores()
        {
            yield return LoadArena();
            Vector3 spawnAt = player.transform.position + Vector3.right * 6f;
            var chaser = spawner.Spawn(chaserPrefab, spawnAt);
            AimAt(spawnAt);
            yield return null;

            Press(mouse.leftButton);
            float timeout = Time.time + 2f;
            while (chaser.gameObject.activeSelf && Time.time < timeout) yield return null;
            Release(mouse.leftButton);

            Assert.IsFalse(chaser.gameObject.activeSelf, "Chaser still alive after 2 s of fire.");
            Assert.AreEqual(0, spawner.ActiveCount);
            Assert.AreEqual(chaser.Data.ScoreValue, game.Score);
            Assert.AreEqual($"SCORE {game.Score}", FindText("UI/HUD/Score").text);
        }

        [UnityTest]
        public IEnumerator TouchingChaser_HurtsPlayer_OncePerInvulnerabilityWindow()
        {
            yield return LoadArena();
            var health = player.GetComponent<Health>();
            spawner.Spawn(chaserPrefab, player.transform.position + Vector3.right * 1.5f);

            // Contact happens within ~0.3 s; the 1 s invulnerability window blocks a second hit.
            yield return new WaitForSeconds(0.7f);

            Assert.AreEqual(health.Max - 1f, health.Current);
            Assert.AreEqual((health.Max - 1f) / health.Max, FindImage("UI/HUD/HealthBarBg/HealthFill").fillAmount, 0.001f);
        }

        // ------------------------------------------------------------------ waves

        [UnityTest]
        public IEnumerator FirstWave_StartsAfterDelay_AndSpawns()
        {
            yield return LoadArena(waves: true);
            // 1 s first-wave delay, then one spawn per second.
            yield return new WaitForSeconds(2.3f);

            Assert.AreEqual(1, director.CurrentWave);
            Assert.GreaterOrEqual(spawner.ActiveCount, 2);
            Assert.AreEqual("WAVE 1", FindText("UI/HUD/Wave").text);
        }

        [UnityTest]
        public IEnumerator ClearingAWave_StartsTheNext()
        {
            yield return LoadArena(waves: true);

            // Kill everything the moment it appears until wave 2 begins (~9.5 s with default tuning).
            float timeout = Time.time + 15f;
            while (director.CurrentWave < 2 && Time.time < timeout)
            {
                foreach (var enemy in Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None))
                    enemy.Health.TakeDamage(new DamageInfo(1000f, Vector2.zero, null));
                yield return null;
            }

            Assert.AreEqual(2, director.CurrentWave, "Wave 2 never started.");
            Assert.AreEqual(6 * chaserPrefab.Data.ScoreValue, game.Score, "Wave 1 should be exactly 6 chasers.");
        }

        // ------------------------------------------------------------------ game flow

        [UnityTest]
        public IEnumerator PressingEscape_PausesAndBlocksFire_ThenResumes()
        {
            yield return LoadArena();

            PressAndRelease(keyboard.escapeKey);
            yield return null;
            Assert.IsTrue(game.State.Is(GameState.Paused));
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsTrue(Object.FindAnyObjectByType<PauseMenu>().IsVisible);

            Press(mouse.leftButton);
            for (int i = 0; i < 5; i++) yield return null;
            Release(mouse.leftButton);
            Assert.AreEqual(0, Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length, "Fired while paused.");

            PressAndRelease(keyboard.escapeKey);
            yield return null;
            Assert.IsTrue(game.State.Is(GameState.Playing));
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(Object.FindAnyObjectByType<PauseMenu>().IsVisible);
        }

        [UnityTest]
        public IEnumerator PlayerDeath_ShowsGameOver_AndRestartButtonStartsAFreshRun()
        {
            yield return LoadArena();
            spawner.Spawn(chaserPrefab, player.transform.position + Vector3.up * 6f);
            player.GetComponent<Health>().TakeDamage(new DamageInfo(100f, Vector2.zero, null));
            yield return null;

            Assert.IsFalse(player.gameObject.activeSelf);
            Assert.IsTrue(game.State.Is(GameState.GameOver));
            var screen = Object.FindAnyObjectByType<GameOverScreen>();
            Assert.IsTrue(screen.IsVisible);
            Assert.IsFalse(director.enabled, "Waves should stop on game over.");

            screen.transform.Find("RestartButton").GetComponent<Button>().onClick.Invoke();
            yield return null;
            yield return null;
            FindSceneObjects();

            Assert.IsTrue(game.State.Is(GameState.Playing));
            Assert.IsTrue(player.gameObject.activeSelf);
            Assert.AreEqual(0, game.Score);
            Assert.AreEqual(0, spawner.ActiveCount);
            Assert.IsFalse(Object.FindAnyObjectByType<GameOverScreen>().IsVisible);

            // Input must survive the reload (old GameManager disables it, new one re-enables it).
            float startX = player.transform.position.x;
            Press(keyboard.dKey);
            yield return new WaitForSeconds(0.3f);
            Release(keyboard.dKey);
            Assert.Greater(player.transform.position.x, startX + 0.5f, "Player can't move after restart.");
        }

        [UnityTest]
        public IEnumerator GameOverScreen_HasAQuitButton_WiredToGameManagerQuit()
        {
            yield return LoadArena();
            player.GetComponent<Health>().TakeDamage(new DamageInfo(100f, Vector2.zero, null));
            yield return null;

            var screen = Object.FindAnyObjectByType<GameOverScreen>();
            Assert.IsTrue(screen.IsVisible);
            var quit = screen.transform.Find("QuitButton")?.GetComponent<Button>();
            Assert.IsNotNull(quit, "No Quit button on the game over screen.");
            Assert.IsTrue(quit.interactable);

            // Pressing it would stop Play mode and end the test run, so check the wiring instead.
            Assert.AreEqual(1, quit.onClick.GetPersistentEventCount());
            Assert.AreSame(game, quit.onClick.GetPersistentTarget(0));
            Assert.AreEqual(nameof(GameManager.Quit), quit.onClick.GetPersistentMethodName(0));
        }
    }
}
#endif
