using System.Collections;
using System.Collections.Generic;
using AS.Core;
using AS.Enemies;
using UnityEngine;

namespace AS.Waves
{
    /// <summary>
    /// Runs the authored waves in order, then keeps going with the last one scaled up: more
    /// enemies and shorter spawn intervals each wave. A wave ends when everything it spawned is
    /// dead. Stops when the player in its arena dies, and starts over from wave 1 when the arena resets.
    /// </summary>
    public class WaveDirector : MonoBehaviour
    {
        [SerializeField] EnemySpawner spawner;
        [SerializeField] WaveDefinition[] waves;
        [SerializeField, Min(0f)] float firstWaveDelay = 1f;
        [SerializeField, Min(0f)] float timeBetweenWaves = 2.5f;
        [Tooltip("Spawning pauses while this many enemies are alive.")]
        [SerializeField, Min(1)] int maxAlive = 20;

        [Header("Endless scaling (waves past the last authored one)")]
        [Tooltip("Extra enemies per wave, as a fraction of the last authored wave's counts.")]
        [SerializeField, Min(0f)] float countGrowth = 0.25f;
        [Tooltip("Spawn interval multiplier applied once per extra wave.")]
        [SerializeField, Range(0.5f, 1f)] float intervalDecay = 0.9f;
        [SerializeField, Min(0.05f)] float minInterval = 0.25f;

        Coroutine _run;
        EventChannel _events;

        public int CurrentWave { get; private set; }

        void Awake()
        {
            _events = Arena.EventsFor(this);
            // For the object's lifetime: the director is disabled once the player dies.
            _events.Subscribe<ArenaReset>(OnArenaReset);
        }

        void OnDestroy() => _events.Unsubscribe<ArenaReset>(OnArenaReset);

        /// <summary>Back to before wave 1, then running again (restarted if it was still going).</summary>
        void OnArenaReset(ArenaReset _)
        {
            enabled = false;
            CurrentWave = 0;
            enabled = true;
        }

        void OnEnable()
        {
            _events.Subscribe<PlayerDied>(OnPlayerDied);
            _run = StartCoroutine(Run());
        }

        void OnDisable()
        {
            _events.Unsubscribe<PlayerDied>(OnPlayerDied);
            // Disabling a component doesn't stop its coroutines.
            if (_run != null) StopCoroutine(_run);
            _run = null;
        }

        void OnPlayerDied(PlayerDied _) => enabled = false;

        IEnumerator Run()
        {
            yield return new WaitForSeconds(firstWaveDelay);
            while (true)
            {
                CurrentWave++;
                var queue = BuildSpawnQueue(CurrentWave, out float interval);
                _events.Publish(new WaveStarted(CurrentWave, queue.Count));

                foreach (var prefab in queue)
                {
                    while (spawner.ActiveCount >= maxAlive) yield return null;
                    while (!spawner.TrySpawn(prefab)) yield return null;
                    yield return new WaitForSeconds(interval);
                }

                while (spawner.ActiveCount > 0) yield return null;
                _events.Publish(new WaveCleared(CurrentWave));
                yield return new WaitForSeconds(timeBetweenWaves);
            }
        }

        List<EnemyBase> BuildSpawnQueue(int waveNumber, out float interval)
        {
            var wave = waves[Mathf.Min(waveNumber, waves.Length) - 1];
            int extraWaves = Mathf.Max(0, waveNumber - waves.Length);
            interval = ScaledInterval(wave.SpawnInterval, extraWaves, intervalDecay, minInterval);

            var queue = new List<EnemyBase>();
            foreach (var group in wave.Groups)
            {
                int count = ScaledCount(group.Count, extraWaves, countGrowth);
                for (int i = 0; i < count; i++) queue.Add(group.Enemy);
            }

            // Fisher-Yates, so mixed waves interleave enemy types.
            for (int i = queue.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (queue[i], queue[j]) = (queue[j], queue[i]);
            }
            return queue;
        }

        public static int ScaledCount(int baseCount, int extraWaves, float growth) =>
            Mathf.CeilToInt(baseCount * (1f + growth * extraWaves));

        public static float ScaledInterval(float baseInterval, int extraWaves, float decay, float min) =>
            Mathf.Max(min, baseInterval * Mathf.Pow(decay, extraWaves));
    }
}
