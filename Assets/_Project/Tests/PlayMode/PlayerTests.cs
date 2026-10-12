using System.Collections;
using AS.Combat;
using AS.Enemies;
using AS.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AS.Tests
{
    /// <summary>
    /// Drives the real Arena scene with simulated keyboard and mouse input. The spawner is switched
    /// off unless a test is about spawning, so enemies only appear where a test puts them.
    /// </summary>
    public class PlayerTests : InputTestFixture
    {
        Keyboard _keyboard;
        Mouse _mouse;
        PlayerController _player;
        EnemySpawner _spawner;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
        }

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

        [UnityTest]
        public IEnumerator ShootingChaser_KillsIt_AndReturnsItToThePool()
        {
            yield return LoadArena();
            Vector3 spawnAt = _player.transform.position + Vector3.right * 6f;
            var chaser = _spawner.Spawn(spawnAt);
            AimAt(spawnAt);
            yield return null;

            Press(_mouse.leftButton);
            float timeout = Time.time + 2f;
            while (chaser.gameObject.activeSelf && Time.time < timeout) yield return null;
            Release(_mouse.leftButton);

            Assert.IsFalse(chaser.gameObject.activeSelf, "Chaser still alive after 2 s of fire.");
            Assert.AreEqual(0, _spawner.ActiveCount);
            Assert.IsTrue(_player.gameObject.activeSelf, "Player should not have died.");
        }

        [UnityTest]
        public IEnumerator TouchingChaser_HurtsPlayer_OncePerInvulnerabilityWindow()
        {
            yield return LoadArena();
            var health = _player.GetComponent<Health>();
            _spawner.Spawn(_player.transform.position + Vector3.right * 1.5f);

            // Contact happens within ~0.3 s; the 1 s invulnerability window blocks a second hit.
            yield return new WaitForSeconds(0.7f);

            Assert.AreEqual(health.Max - 1f, health.Current);
        }

        [UnityTest]
        public IEnumerator Player_LeavesPlay_WhenHealthRunsOut()
        {
            yield return LoadArena();
            _player.GetComponent<Health>().TakeDamage(new DamageInfo(100f, Vector2.zero, null));
            yield return null;

            Assert.IsFalse(_player.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator Spawner_SpawnsChasersOverTime()
        {
            yield return LoadArena(spawning: true);
            // 1 s initial delay, then one every 1.5 s: spawns at ~1.0 s and ~2.5 s.
            yield return new WaitForSeconds(3f);

            Assert.GreaterOrEqual(Object.FindObjectsByType<Chaser>(FindObjectsSortMode.None).Length, 2);
        }

        IEnumerator LoadArena(bool spawning = false)
        {
            SceneManager.LoadScene("Arena");
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerController>();
            _spawner = Object.FindAnyObjectByType<EnemySpawner>();
            _spawner.enabled = spawning;
            yield return null;
        }

        void AimAt(Vector3 world) => Set(_mouse.position, (Vector2)Camera.main.WorldToScreenPoint(world));
    }
}
