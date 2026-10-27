#if UNITY_EDITOR
using System.Collections;
using AS.Core;
using AS.Enemies;
using AS.Player;
using AS.Waves;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AS.Tests
{
    /// <summary>
    /// Shared setup for tests that play the real Arena scene: a simulated keyboard and mouse,
    /// handles to the main scene objects, and helpers. The wave director is switched off unless a
    /// test asks for waves, so enemies only appear where the test puts them. High scores go to a
    /// throwaway PlayerPrefs key, so tests never touch the real best score.
    /// </summary>
    public abstract class ArenaTestFixture : InputTestFixture
    {
        protected const string ChaserPrefabPath = "Assets/_Project/Prefabs/Enemies/Chaser.prefab";
        protected const string ShooterPrefabPath = "Assets/_Project/Prefabs/Enemies/Shooter.prefab";
        protected const string TankPrefabPath = "Assets/_Project/Prefabs/Enemies/Tank.prefab";
        const string TestHighScoreKey = "AS.HighScore.Tests";

        protected Keyboard keyboard;
        protected Mouse mouse;
        protected GameManager game;
        protected Arena arena;
        protected PlayerController player;
        protected EnemySpawner spawner;
        protected WaveDirector director;
        protected Chaser chaserPrefab;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            chaserPrefab = LoadAsset<Chaser>(ChaserPrefabPath);
            HighScoreStore.Key = TestHighScoreKey;
            HighScoreStore.Clear();
        }

        public override void TearDown()
        {
            Time.timeScale = 1f;
            HighScoreStore.Clear();
            HighScoreStore.Key = HighScoreStore.DefaultKey;
            base.TearDown();
        }

        protected IEnumerator LoadArena(bool waves = false)
        {
            SceneManager.LoadScene("Arena");
            yield return null;
            FindSceneObjects();
            director.enabled = waves;
            yield return null;
        }

        protected void FindSceneObjects()
        {
            game = Object.FindAnyObjectByType<GameManager>();
            arena = Object.FindAnyObjectByType<Arena>();
            player = Object.FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
            spawner = Object.FindAnyObjectByType<EnemySpawner>();
            director = Object.FindAnyObjectByType<WaveDirector>(FindObjectsInactive.Include);
        }

        /// <summary>Lets the physics engine run so new overlaps raise their trigger/collision callbacks.</summary>
        protected static IEnumerator PhysicsSteps(int steps = 2)
        {
            for (int i = 0; i < steps; i++) yield return new WaitForFixedUpdate();
            yield return null;
        }

        protected void AimAt(Vector3 world) => Set(mouse.position, (Vector2)Camera.main.WorldToScreenPoint(world));

        protected static T LoadAsset<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);
        protected static TMP_Text FindText(string path) => GameObject.Find(path).GetComponent<TMP_Text>();
        protected static Image FindImage(string path) => GameObject.Find(path).GetComponent<Image>();
    }
}
#endif
