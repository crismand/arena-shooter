using AS.Combat;
using AS.Core;
using AS.Player;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace AS.Agents
{
    /// <summary>
    /// Plays the player in its arena with ML-Agents. Observes the player's position, velocity, health,
    /// aim and weapon cooldown, plus whatever the ray sensor on this object sees (enemies, enemy
    /// bullets, walls, pickups), and acts through <see cref="AgentInput"/>: move and aim as four
    /// continuous actions, fire as one discrete branch. Rewards come from the arena's events, and each
    /// episode resets the arena in place.
    /// Lives on its own object rather than on the player, because the player is deactivated when it
    /// dies; it follows the player so the rays start there.
    /// </summary>
    public class ArenaAgent : Agent
    {
        public const string Behavior = "ArenaAgent";
        /// <summary>Position (2), velocity (2), health (1), aim (2), cooldown (1).</summary>
        public const int VectorObservationSize = 8;
        /// <summary>Move x/y and aim x/y, continuous; fire or hold, one discrete branch of two.</summary>
        public static ActionSpec Actions => new(4, new[] { 2 });

        public enum HeuristicMode { Idle, Random }

        [SerializeField] Arena arena;
        [SerializeField] AgentInput input;
        [SerializeField] Rigidbody2D body;
        [SerializeField] Health health;
        [SerializeField] PlayerAim aim;
        [SerializeField] Weapon weapon;
        [Tooltip("Half the arena's inner width and height, which observe as ±1.")]
        [SerializeField] Vector2 arenaHalfSize = new(16f, 10f);
        [Tooltip("Speed that observes as 1: the player's move speed.")]
        [SerializeField, Min(0.1f)] float maxSpeed = 7f;

        [Header("Rewards")]
        [SerializeField] float killReward = 0.5f;
        [SerializeField] float hitPenalty = -0.1f;
        [SerializeField] float deathPenalty = -1f;
        [Tooltip("Per agent step (every physics step), so it adds up over an episode.")]
        [SerializeField] float aliveReward = 0.001f;

        [Tooltip("What the agent does with no model and no trainer: stand still or act at random. The baselines for evaluation.")]
        [SerializeField] HeuristicMode heuristic = HeuristicMode.Random;

        bool _died;

        protected override void OnEnable()
        {
            base.OnEnable();
            arena.Events.Subscribe<EnemyKilled>(OnEnemyKilled);
            arena.Events.Subscribe<PlayerDamaged>(OnPlayerDamaged);
            arena.Events.Subscribe<PlayerDied>(OnPlayerDied);
        }

        protected override void OnDisable()
        {
            arena.Events.Unsubscribe<EnemyKilled>(OnEnemyKilled);
            arena.Events.Unsubscribe<PlayerDamaged>(OnPlayerDamaged);
            arena.Events.Unsubscribe<PlayerDied>(OnPlayerDied);
            base.OnDisable();
        }

        public override void OnEpisodeBegin()
        {
            _died = false;
            input.Clear();
            arena.ResetArena();
            transform.position = body.position;
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            Vector2 local = body.position - (Vector2)arena.transform.position;
            sensor.AddObservation(new Vector2(local.x / arenaHalfSize.x, local.y / arenaHalfSize.y));
            sensor.AddObservation(Vector2.ClampMagnitude(body.linearVelocity / maxSpeed, 1f));
            sensor.AddObservation(health.Current / health.Max);
            sensor.AddObservation(aim.Direction);
            sensor.AddObservation(Mathf.Clamp01(weapon.Cooldown * weapon.Data.FireRate));
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            var c = actions.ContinuousActions;
            input.Apply(new Vector2(c[0], c[1]), new Vector2(c[2], c[3]), actions.DiscreteActions[0] == 1);
            AddReward(aliveReward);
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            // Copies of the segment structs; they share the buffers, so writes go through.
            var c = actionsOut.ContinuousActions;
            var d = actionsOut.DiscreteActions;
            bool random = heuristic == HeuristicMode.Random;
            for (int i = 0; i < c.Length; i++) c[i] = random ? Random.Range(-1f, 1f) : 0f;
            d[0] = random ? Random.Range(0, 2) : 0;
        }

        void FixedUpdate()
        {
            // Ended here rather than in OnPlayerDied: resetting inside that event would restore the
            // player before PlayerController finishes dying and switches it off.
            if (_died)
            {
                _died = false;
                EndEpisode();
            }
            transform.position = body.position;
        }

        void OnEnemyKilled(EnemyKilled _) => AddReward(killReward);
        void OnPlayerDamaged(PlayerDamaged _) => AddReward(hitPenalty);

        void OnPlayerDied(PlayerDied _)
        {
            AddReward(deathPenalty);
            _died = true;
        }
    }
}
