using System.Collections;
using AS.Combat;
using AS.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AS.Tests
{
    /// <summary>Drives the real Arena scene with simulated keyboard and mouse input.</summary>
    public class PlayerTests : InputTestFixture
    {
        Keyboard _keyboard;
        Mouse _mouse;

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
            var player = Object.FindAnyObjectByType<PlayerController>();
            float startX = player.transform.position.x;

            Press(_keyboard.dKey);
            yield return new WaitForSeconds(0.5f);
            Release(_keyboard.dKey);

            Assert.Greater(player.transform.position.x, startX + 1f);
        }

        [UnityTest]
        public IEnumerator HoldingFire_ShootsTowardMouse_AndWallsDespawnBullets()
        {
            yield return LoadArena();
            var player = Object.FindAnyObjectByType<PlayerController>();
            Vector3 target = player.transform.position + Vector3.right * 5f;
            Set(_mouse.position, (Vector2)Camera.main.WorldToScreenPoint(target));
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

        static IEnumerator LoadArena()
        {
            SceneManager.LoadScene("Arena");
            yield return null;
            yield return null;
        }
    }
}
