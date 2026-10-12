using System;
using System.Collections.Generic;
using AS.Enemies;
using UnityEngine;

namespace AS.Waves
{
    /// <summary>One authored wave: which enemies, how many of each, and how quickly they arrive.</summary>
    [CreateAssetMenu(menuName = "AS/Wave Definition", fileName = "Wave")]
    public class WaveDefinition : ScriptableObject
    {
        [Serializable]
        public struct SpawnGroup
        {
            [SerializeField] EnemyBase enemy;
            [SerializeField, Min(1)] int count;

            public EnemyBase Enemy => enemy;
            public int Count => count;
        }

        [Tooltip("Enemies are shuffled together, not spawned group by group.")]
        [SerializeField] SpawnGroup[] groups = Array.Empty<SpawnGroup>();
        [Tooltip("Seconds between individual spawns.")]
        [SerializeField, Min(0.05f)] float spawnInterval = 1f;

        public IReadOnlyList<SpawnGroup> Groups => groups;
        public float SpawnInterval => spawnInterval;
    }
}
