#if UNITY_EDITOR
using System.Collections;
using AS.Combat;
using AS.Core;
using AS.Enemies;
using AS.Player;
using AS.UI;
using AS.Waves;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AS.Tests
{
    /// <summary>
    /// Drives the real Arena scene with simulated keyboard and mouse input. The wave director is
    /// switched off unless a test is about waves, so enemies only appear where a test puts them.
    /// </summary>
    public class PlayerTests : InputTestFixture
    {
        const string ChaserPrefabPath = "Assets/_Project/Prefabs/Enemies/Chaser.prefab";

        Keyboard _keyboard;
        Mouse _mouse;
        GameManager _game;
        PlayerController _player;
        EnemySpawner _spawner;
        WaveDirector _director;
        Chaser _chaserPrefab;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
            _chaserPrefab = AssetDatabase.LoadAssetAtPath<Chaser>(ChaserPrefabPath);
        }

        public override void TearDown()
        {
            Time.timeScale = 1f;
            base.TearDown();
        }

        // ------------------------------------------------------------------ player

        [UnityTest]
        public IEnumerator HoldingD_MovesPlayerRight()
        {
            yield return LoadArena();
            float startX = _player.transform.position.x;

            Press(_keyboard.dKey);
            yield return new WaitForSeconds(0.5f);
            Release(_keyboard.dKey);

            Assert.Greater(_player.transform.position.x, startX + 1f);
        }

        [UnityTest]
        public IEnumerator HoldingFire_ShootsTowardMouse_AndWallsDespawnBullets()
        {
            yield return LoadArena();
            AimAt(_player.transform.position + Vector3.right * 5f);
            yield return null;

            Press(_mouse.leftButton);
            yield return new WaitForSeconds(0.3f);

            var bullets = Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None);
            Assert.Greater(bullets.Length, 0, "No projectiles in flight while fire is held.");
            foreach (var bullet in bullets)
                Assert.Greater(bullet.GetComponent<Rigidbody2D>().linearVelocity.x, 10f, "Projectile not travelling toward the mouse.");

            Release(_mouse.leftButton);
            // Shorter than the blaster's 1.5 s lifetime, so only a wall hit can have removed them.
            yield return new WaitForSeconds(1.2f);
            Assert.AreEqual(0, Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length);
        }

        // ------------------------------------------------------------------ enemies and score

        [UnityTest]
        public IEnumerator ShootingChaser_KillsIt_AndScores()
        {
            yield return LoadArena();
            Vector3 spawnAt = _player.transform.position + Vector3.right * 6f;
            var chaser = _spawner.Spawn(_chaserPrefab, spawnAt);
            AimAt(spawnAt);
            yield return null;

            Press(_mouse.leftButton);
            float timeout = Time.time + 2f;
            while (chaser.gameObject.activeSelf && Time.time < timeout) yield return null;
            Release(_mouse.leftButton);

            Assert.IsFalse(chaser.gameObject.activeSelf, "Chaser still alive after 2 s of fire.");
            Assert.AreEqual(0, _spawner.ActiveCount);
            Assert.AreEqual(chaser.Data.ScoreValue, _game.Score);
            Assert.AreEqual($"SCORE {_game.Score}", FindText("UI/HUD/Score").text);
        }

        [UnityTest]
        public IEnumerator TouchingChaser_HurtsPlayer_OncePerInvulnerabilityWindow()
        {
            yield return LoadArena();
            var health = _player.GetComponent<Health>();
            _spawner.Spawn(_chaserPrefab, _player.transform.position + Vector3.right * 1.5f);

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

            Assert.AreEqual(1, _director.CurrentWave);
            Assert.GreaterOrEqual(_spawner.ActiveCount, 2);
            Assert.AreEqual("WAVE 1", FindText("UI/HUD/Wave").text);
        }

        [UnityTest]
        public IEnumerator ClearingAWave_StartsTheNext()
        {
            yield return LoadArena(waves: true);

            // Kill everything the moment it appears until wave 2 begins (~9.5 s with default tuning).
            float timeout = Time.time + 15f;
            while (_director.CurrentWave < 2 && Time.time < timeout)
            {
                foreach (var chaser in Object.FindObjectsByType<Chaser>(FindObjectsSortMode.None))
                    chaser.Health.TakeDamage(new DamageInfo(1000f, Vector2.zero, null));
                yield return null;
            }

            Assert.AreEqual(2, _director.CurrentWave, "Wave 2 never started.");
            Assert.AreEqual(6 * _chaserPrefab.Data.ScoreValue, _game.Score, "Wave 1 should be exactly 6 chasers.");
        }

        // ------------------------------------------------------------------ game flow

        [UnityTest]
        public IEnumerator PressingEscape_PausesAndBlocksFire_ThenResumes()
        {
            yield return LoadArena();

            PressAndRelease(_keyboard.escapeKey);
            yield return null;
            Assert.IsTrue(_game.State.Is(GameState.Paused));
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsTrue(Object.FindAnyObjectByType<PauseMenu>().IsVisible);

            Press(_mouse.leftButton);
            for (int i = 0; i < 5; i++) yield return null;
            Release(_mouse.leftButton);
            Assert.AreEqual(0, Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length, "Fired while paused.");

            PressAndRelease(_keyboard.escapeKey);
            yield return null;
            Assert.IsTrue(_game.State.Is(GameState.Playing));
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(Object.FindAnyObjectByType<PauseMenu>().IsVisible);
        }

        [UnityTest]
        public IEnumerator PlayerDeath_ShowsGameOver_AndRestartButtonStartsAFreshRun()
        {
            yield return LoadArena();
            _spawner.Spawn(_chaserPrefab, _player.transform.position + Vector3.up * 6f);
            _player.GetComponent<Health>().TakeDamage(new DamageInfo(100f, Vector2.zero, null));
            yield return null;

            Assert.IsFalse(_player.gameObject.activeSelf);
            Assert.IsTrue(_game.State.Is(GameState.GameOver));
            var screen = Object.FindAnyObjectByType<GameOverScreen>();
            Assert.IsTrue(screen.IsVisible);
            Assert.IsFalse(_director.enabled, "Waves should stop on game over.");

            screen.GetComponentInChildren<Button>().onClick.Invoke();
            yield return null;
            yield return null;
            FindSceneObjects();

            Assert.IsTrue(_game.State.Is(GameState.Playing));
            Assert.IsTrue(_player.gameObject.activeSelf);
            Assert.AreEqual(0, _game.Score);
            Assert.AreEqual(0, _spawner.ActiveCount);
            Assert.IsFalse(Object.FindAnyObjectByType<GameOverScreen>().IsVisible);

            // Input must survive the reload (old GameManager disables it, new one re-enables it).
            float startX = _player.transform.position.x;
            Press(_keyboard.dKey);
            yield return new WaitForSeconds(0.3f);
            Release(_keyboard.dKey);
            Assert.Greater(_player.transform.position.x, startX + 0.5f, "Player can't move after restart.");
        }

        // ------------------------------------------------------------------ helpers

        IEnumerator LoadArena(bool waves = false)
        {
            SceneManager.LoadScene("Arena");
            yield return null;
            FindSceneObjects();
            _director.enabled = waves;
            yield return null;
        }

        void FindSceneObjects()
        {
            _game = Object.FindAnyObjectByType<GameManager>();
            _player = Object.FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
            _spawner = Object.FindAnyObjectByType<EnemySpawner>();
            _director = Object.FindAnyObjectByType<WaveDirector>(FindObjectsInactive.Include);
        }

        void AimAt(Vector3 world) => Set(_mouse.position, (Vector2)Camera.main.WorldToScreenPoint(world));

        static Text FindText(string path) => GameObject.Find(path).GetComponent<Text>();
        static Image FindImage(string path) => GameObject.Find(path).GetComponent<Image>();
    }
}
#endif
