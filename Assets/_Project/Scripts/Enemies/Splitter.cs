using UnityEngine;

namespace AS.Enemies
{
    /// <summary>
    /// Heads for the target like a <see cref="Chaser"/>, and when killed breaks into
    /// <see cref="childCount"/> smaller enemies that scatter outward. The children come from the same
    /// spawner, so they count as alive and a wave isn't cleared until they're dead too.
    /// </summary>
    public class Splitter : Chaser
    {
        [SerializeField] EnemyBase childPrefab;
        [SerializeField, Min(0)] int childCount = 3;
        [Tooltip("How far from the Splitter's centre the children appear.")]
        [SerializeField, Min(0f)] float childOffset = 0.45f;
        [Tooltip("Speed the children scatter at.")]
        [SerializeField, Min(0f)] float scatterSpeed = 6f;

        public EnemyBase ChildPrefab => childPrefab;
        public int ChildCount => childCount;

        protected override void OnKilled()
        {
            // Without a spawner (placed by hand) there's nothing to keep track of the children.
            if (Spawner == null || childPrefab == null) return;

            float startAngle = Random.Range(0f, 360f);
            for (int i = 0; i < childCount; i++)
            {
                float angle = (startAngle + 360f * i / childCount) * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var child = Spawner.Spawn(childPrefab, (Vector2)transform.position + direction * childOffset);
                child.Launch(direction * scatterSpeed);
            }
        }
    }
}
