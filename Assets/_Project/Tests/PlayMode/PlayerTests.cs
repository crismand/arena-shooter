#if UNITY_EDITOR
using System.Collections;
using AS.Combat;
using AS.Core;
using AS.Enemies;
using AS.Pickups;
using AS.Player;
using AS.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
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

        [UnityTest]
        public IEnumerator ScriptedInputSource_MovesAimsAndFires_WithoutDevices()
        {
            yield return LoadArena();
            var scripted = new ScriptedInput { Move = Vector2.left, Aim = Vector2.up, FireHeld = true };
            player.InputSource = scripted;
            player.GetComponent<PlayerAim>().InputSource = scripted;
            float startX = player.transform.position.x;

            yield return new WaitForSeconds(0.3f);

            Assert.Less(player.transform.position.x, startX - 0.5f, "Player didn't follow the scripted move.");
            var bullets = Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None);
            Assert.Greater(bullets.Length, 0, "No projectiles while the scripted source holds fire.");
            foreach (var bullet in bullets)
                Assert.Greater(bullet.GetComponent<Rigidbody2D>().linearVelocity.y, 10f, "Projectile not travelling along the scripted aim.");
        }

        [UnityTest]
        public IEnumerator GameplayObjects_AndTheBulletsTheyFire_AreInsideTheArena()
        {
            yield return LoadArena();
            Assert.IsNotNull(arena, "No Arena in the scene.");
            Assert.AreSame(arena, Arena.Of(player), "Player isn't under the Arena.");
            Assert.AreSame(arena, Arena.Of(spawner), "Spawner isn't under the Arena.");
            Assert.AreSame(arena, Arena.Of(director), "Wave director isn't under the Arena.");
            Assert.AreSame(arena, Arena.Of(Object.FindAnyObjectByType<PickupDropper>()), "Pickup dropper isn't under the Arena.");

            AimAt(player.transform.position + Vector3.right * 5f);
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);

            var bullet = Object.FindAnyObjectByType<Projectile>();
            Assert.IsNotNull(bullet, "Fire didn't spawn a projectile.");
            Assert.AreSame(arena, Arena.Of(bullet), "Projectile was pooled outside the Arena.");
        }

        /// <summary>Stands in for an agent: fixed commands, no Input System devices involved.</summary>
        class ScriptedInput : IPlayerInput
        {
            public Vector2 Move { get; set; }
            public bool FireHeld { get; set; }
            public Vector2 Aim { get; set; }

            public bool TryGetAim(out Vector2 direction)
            {
                direction = Aim;
                return true;
            }
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

            // Input must survive the restart (game over turned gameplay input off).
            float startX = player.transform.position.x;
            Press(keyboard.dKey);
            yield return new WaitForSeconds(0.3f);
            Release(keyboard.dKey);
            Assert.Greater(player.transform.position.x, startX + 0.5f, "Player can't move after restart.");
        }

        [UnityTest]
        public IEnumerator Restart_MidRun_ResetsTheArenaInPlace_WithoutReloadingTheScene()
        {
            yield return LoadArena();
            var sceneHandle = SceneManager.GetActiveScene().handle;
            var playerBefore = player;
            Vector3 startPosition = player.transform.position;
            var health = player.GetComponent<Health>();
            var receiver = player.GetComponent<PowerUpReceiver>();
            var dropper = Object.FindAnyObjectByType<PickupDropper>();

            // Make a mess: score, a hurt and moved player, a shield, enemies, a pickup and bullets in flight.
            arena.Events.Publish(new EnemyKilled(50, new Vector2(5f, 0f), dropChance: 0f));
            Assert.AreEqual(50, game.Score);
            health.TakeDamage(new DamageInfo(1f, Vector2.zero, null));
            receiver.RaiseShield(10f, "SHIELD");
            spawner.Spawn(chaserPrefab, startPosition + Vector3.up * 8f);
            spawner.Spawn(chaserPrefab, startPosition + Vector3.down * 8f);
            dropper.Spawn(LoadAsset<PowerUp>("Assets/_Project/Data/PowerUps/PowerUp_Shield.asset"), startPosition + Vector3.left * 5f);
            AimAt(startPosition + Vector3.right * 5f);
            Press(keyboard.dKey);
            Press(mouse.leftButton);
            yield return new WaitForSeconds(0.3f);
            Release(mouse.leftButton);
            Release(keyboard.dKey);
            Assert.Greater(Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length, 0);

            game.Restart();
            yield return null;

            Assert.AreEqual(sceneHandle, SceneManager.GetActiveScene().handle, "Restart reloaded the scene.");
            Assert.AreSame(playerBefore, Object.FindAnyObjectByType<PlayerController>(), "Player was replaced.");
            Assert.IsTrue(game.State.Is(GameState.Playing));
            Assert.AreEqual(0, game.Score);
            Assert.AreEqual(health.Max, health.Current);
            Assert.Less(Vector2.Distance(player.transform.position, startPosition), 0.1f, "Player not back at the start.");
            Assert.IsFalse(receiver.HasShield, "Shield survived the reset.");
            Assert.AreEqual(0, spawner.ActiveCount, "Enemies survived the reset.");
            Assert.AreEqual(0, dropper.ActiveCount, "Pickups survived the reset.");
            Assert.AreEqual(0, Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length, "Bullets survived the reset.");
            Assert.IsTrue(director.enabled);
            Assert.AreEqual(0, director.CurrentWave, "Waves didn't start over.");
            Assert.AreEqual("", FindText("UI/HUD/PowerUps").text, "HUD still lists the shield.");
        }

        [UnityTest]
        public IEnumerator Restart_FromThePauseMenu_ResumesAFreshRun()
        {
            yield return LoadArena();
            game.SetPaused(true);
            yield return null;
            Assert.IsTrue(Object.FindAnyObjectByType<PauseMenu>().IsVisible);

            game.Restart();
            yield return null;

            Assert.IsTrue(game.State.Is(GameState.Playing));
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(Object.FindAnyObjectByType<PauseMenu>().IsVisible);
            float startX = player.transform.position.x;
            Press(keyboard.dKey);
            yield return new WaitForSeconds(0.3f);
            Release(keyboard.dKey);
            Assert.Greater(player.transform.position.x, startX + 0.5f, "Player can't move after restarting from pause.");
        }

        [UnityTest]
        public IEnumerator Restart_CutsTheCameraBackToThePlayer_InsteadOfGliding()
        {
            yield return LoadArena();
            var camera = Camera.main.transform;
            yield return new WaitForSeconds(0.5f);
            Vector3 startView = camera.position;

            // Off to the side, and long enough for the follow damping to catch up.
            var body = player.GetComponent<Rigidbody2D>();
            body.position = new Vector2(12f, 0f);
            player.transform.position = body.position;
            yield return new WaitForSeconds(1.5f);
            Assert.Greater(camera.position.x, startView.x + 2f, "Camera never followed the player.");

            game.Restart();
            yield return null;
            yield return null;

            Assert.Less(Vector2.Distance(camera.position, startView), 0.1f, "Camera glided instead of cutting back.");
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
