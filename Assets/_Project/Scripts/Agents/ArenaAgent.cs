using System.Collections.Generic;
using AS.Combat;
using AS.Core;
using AS.Enemies;
using AS.Pickups;
using AS.Player;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace AS.Agents
{
    /// <summary>
    /// Plays the player in its arena with ML-Agents. Observes the player's position, velocity, health,
    /// aim and weapon cooldown, how many enemies are left and which wave it is; every enemy and pickup
    /// through one buffer sensor (see <see cref="EntityObservationSize"/>); the nearest enemy bullets
    /// through another (see <see cref="BulletObservationSize"/>); and walls and bullets through the ray
    /// sensor on this object. Acts through <see cref="AgentInput"/>: move and aim as
    /// four continuous actions, fire as one discrete branch. Rewards come from the arena's events, and
    /// each episode resets the arena in place.
    /// Lives on its own object rather than on the player, because the player is deactivated when it
    /// dies; it follows the player so the rays start there.
    /// </summary>
    public class ArenaAgent : Agent
    {
        public const string Behavior = "ArenaAgent";
        /// <summary>Per-episode stats, reported when the next episode begins: in TensorBoard and to evaluate.py.</summary>
        public const string SurvivalStat = "Arena/Survival Time";
        public const string KillsStat = "Arena/Kills";
        public const string DiedStat = "Arena/Died";
        public const string WaveStat = "Arena/Wave Reached";
        /// <summary>
        /// Environment parameter from the trainer config: each episode starts at a random wave from 1 to
        /// this, so the agent practises later waves and bosses it would rarely reach from wave 1.
        /// Unset (no trainer, Watch AI, evaluate.py) it's 1, so those always start at wave 1.
        /// </summary>
        public const string MaxStartWaveParameter = "max_start_wave";
        /// <summary>Position (2), velocity (2), health (1), aim (2), cooldown (1), enemies left (1), wave (1).</summary>
        public const int VectorObservationSize = 10;
        /// <summary>Move x/y and aim x/y, continuous; fire or hold, one discrete branch of two.</summary>
        public static ActionSpec Actions => new(4, new[] { 2 });

        /// <summary>Kinds of enemy and pickup the buffer sensor tells apart; the agent's kind lists must match.</summary>
        public const int EnemyKindCount = 7;
        public const int PickupKindCount = 6;
        /// <summary>The most enemies and pickups observed at once, nearest first.</summary>
        public const int MaxEntities = 32;
        /// <summary>
        /// Per enemy or pickup: its kind, one-hot (<see cref="EnemyKindCount"/> + <see cref="PickupKindCount"/>),
        /// position from the player (2), velocity (2), health (1), and whether it's charging (1).
        /// </summary>
        public const int EntityObservationSize = EnemyKindCount + PickupKindCount + 6;
        /// <summary>
        /// The most enemy bullets observed at once, nearest first. Their own sensor, so a boss's ring of
        /// bullets can't push enemies out of the entity list.
        /// </summary>
        public const int MaxBullets = 24;
        /// <summary>Per enemy bullet: position from the player (2) and velocity (2).</summary>
        public const int BulletObservationSize = 4;

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

        [Header("Enemies and pickups")]
        [SerializeField] EnemySpawner spawner;
        [SerializeField] PickupDropper pickups;
        [SerializeField] BufferSensorComponent entitySensor;
        [SerializeField] BufferSensorComponent bulletSensor;
        [Tooltip("Each enemy kind's data, in the order of its one-hot slot.")]
        [SerializeField] EnemyData[] enemyKinds;
        [Tooltip("Each pickup's power-up, in the order of its one-hot slot.")]
        [SerializeField] PowerUp[] pickupKinds;
        [Tooltip("Enemies that observe as 1 enemy left: the wave director's most alive at once.")]
        [SerializeField, Min(1)] int maxEnemies = 20;
        [Tooltip("Wave that observes as 1.")]
        [SerializeField, Min(1)] int maxWave = 20;

        [Header("Rewards")]
        [SerializeField] float killReward = 0.3f;
        [SerializeField] float waveClearedReward = 3f;
        [SerializeField] float bossClearedReward = 6f;
        [SerializeField] float pickupReward = 0.1f;
        [SerializeField] float hitPenalty = -0.5f;
        [SerializeField] float deathPenalty = -5f;
        [Tooltip("Per agent step (every physics step) while enemies are left, so waiting doesn't pay. Small enough that dying never beats stalling.")]
        [SerializeField] float enemiesLeftPenalty = -0.0002f;

        [Tooltip("What the agent does with no model and no trainer: stand still or act at random. The baselines for evaluation.")]
        [SerializeField] HeuristicMode heuristic = HeuristicMode.Random;

        bool _died;
        int _wave;
        bool _bossWave;
        // The episode in progress, for its stats.
        bool _episodeRunning;
        float _episodeStart;
        int _kills;
        bool _diedThisEpisode;

        readonly List<EnemyBase> _enemies = new();
        readonly List<Pickup> _pickups = new();
        readonly List<Projectile> _bullets = new();
        readonly List<(float distance, int index)> _order = new();
        float[] _entity;
        readonly float[] _bullet = new float[BulletObservationSize];

        protected override void OnEnable()
        {
            base.OnEnable();
            arena.Events.Subscribe<EnemyKilled>(OnEnemyKilled);
            arena.Events.Subscribe<PlayerDamaged>(OnPlayerDamaged);
            arena.Events.Subscribe<PlayerDied>(OnPlayerDied);
            arena.Events.Subscribe<WaveStarted>(OnWaveStarted);
            arena.Events.Subscribe<WaveCleared>(OnWaveCleared);
            arena.Events.Subscribe<PickupCollected>(OnPickupCollected);
        }

        protected override void OnDisable()
        {
            arena.Events.Unsubscribe<EnemyKilled>(OnEnemyKilled);
            arena.Events.Unsubscribe<PlayerDamaged>(OnPlayerDamaged);
            arena.Events.Unsubscribe<PlayerDied>(OnPlayerDied);
            arena.Events.Unsubscribe<WaveStarted>(OnWaveStarted);
            arena.Events.Unsubscribe<WaveCleared>(OnWaveCleared);
            arena.Events.Unsubscribe<PickupCollected>(OnPickupCollected);
            base.OnDisable();
        }

        public override void OnEpisodeBegin()
        {
            // Episodes end by death (EndEpisode) or by running out of steps (MaxStep); both land here.
            if (_episodeRunning) ReportEpisode();
            _episodeRunning = true;
            _episodeStart = Time.time;
            _kills = 0;
            _wave = 0;
            _bossWave = false;
            _diedThisEpisode = false;

            _died = false;
            input.Clear();
            int maxStartWave = Mathf.Max(1, Mathf.RoundToInt(Academy.Instance.EnvironmentParameters.GetWithDefault(MaxStartWaveParameter, 1f)));
            arena.ResetArena(Random.Range(1, maxStartWave + 1));
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
            sensor.AddObservation(Mathf.Clamp01((float)spawner.ActiveCount / maxEnemies));
            sensor.AddObservation(Mathf.Clamp01((float)_wave / maxWave));
            ObserveEntities();
            ObserveBullets();
        }

        /// <summary>The nearest enemy bullets into their buffer sensor.</summary>
        void ObserveBullets()
        {
            Projectile.GetLive(arena, _bullets);
            _order.Clear();
            for (int i = 0; i < _bullets.Count; i++)
                if (_bullets[i].IsEnemyShot)
                    _order.Add((((Vector2)_bullets[i].transform.position - body.position).sqrMagnitude, i));
            _order.Sort((a, b) => a.distance.CompareTo(b.distance));

            int count = Mathf.Min(_order.Count, MaxBullets);
            for (int n = 0; n < count; n++) bulletSensor.AppendObservation(Observe(_bullets[_order[n].index]));
        }

        /// <summary>What the bullet sensor gets for <paramref name="bullet"/>. Reuses one array, so copy it to keep it.</summary>
        public float[] Observe(Projectile bullet)
        {
            Vector2 offset = (Vector2)bullet.transform.position - body.position;
            // Bullets outrun the player, so their velocity is capped higher than an enemy's.
            Vector2 v = Vector2.ClampMagnitude(bullet.Velocity / maxSpeed, 4f);
            _bullet[0] = offset.x / arenaHalfSize.x;
            _bullet[1] = offset.y / arenaHalfSize.y;
            _bullet[2] = v.x;
            _bullet[3] = v.y;
            return _bullet;
        }

        /// <summary>Every enemy and pickup, nearest first, into the buffer sensor.</summary>
        void ObserveEntities()
        {
            _enemies.Clear();
            _pickups.Clear();
            spawner.GetActive(_enemies);
            pickups.GetActive(_pickups);

            _order.Clear();
            for (int i = 0; i < _enemies.Count; i++)
                _order.Add((((Vector2)_enemies[i].transform.position - body.position).sqrMagnitude, i));
            for (int i = 0; i < _pickups.Count; i++)
                _order.Add((((Vector2)_pickups[i].transform.position - body.position).sqrMagnitude, _enemies.Count + i));
            _order.Sort((a, b) => a.distance.CompareTo(b.distance));

            int count = Mathf.Min(_order.Count, MaxEntities);
            for (int n = 0; n < count; n++)
            {
                int index = _order[n].index;
                entitySensor.AppendObservation(index < _enemies.Count ? Observe(_enemies[index]) : Observe(_pickups[index - _enemies.Count]));
            }
        }

        /// <summary>What the buffer sensor gets for <paramref name="enemy"/>. Reuses one array, so copy it to keep it.</summary>
        public float[] Observe(EnemyBase enemy)
        {
            Clear();
            int kind = System.Array.IndexOf(enemyKinds, enemy.Data);
            if (kind >= 0) _entity[kind] = 1f;
            Describe(enemy.transform.position, enemy.Velocity);
            _entity[EntityObservationSize - 2] = enemy.Health.Current / enemy.Health.Max;
            _entity[EntityObservationSize - 1] = enemy.IsCharging ? 1f : 0f;
            return _entity;
        }

        /// <inheritdoc cref="Observe(EnemyBase)"/>
        public float[] Observe(Pickup pickup)
        {
            Clear();
            int kind = System.Array.IndexOf(pickupKinds, pickup.PowerUp);
            if (kind >= 0) _entity[EnemyKindCount + kind] = 1f;
            Describe(pickup.transform.position, Vector2.zero);
            return _entity;
        }

        void Clear()
        {
            _entity ??= new float[EntityObservationSize];
            System.Array.Clear(_entity, 0, _entity.Length);
        }

        // Position from the player in half-arenas (so ±2 across), velocity in player speeds, capped at 2.
        void Describe(Vector2 position, Vector2 velocity)
        {
            Vector2 offset = position - body.position;
            int at = EnemyKindCount + PickupKindCount;
            _entity[at] = offset.x / arenaHalfSize.x;
            _entity[at + 1] = offset.y / arenaHalfSize.y;
            Vector2 v = Vector2.ClampMagnitude(velocity / maxSpeed, 2f);
            _entity[at + 2] = v.x;
            _entity[at + 3] = v.y;
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            var c = actions.ContinuousActions;
            input.Apply(new Vector2(c[0], c[1]), new Vector2(c[2], c[3]), actions.DiscreteActions[0] == 1);
            if (spawner.ActiveCount > 0) AddReward(enemiesLeftPenalty);
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

        /// <summary>
        /// Game seconds survived (not real ones, so the time scale doesn't matter), kills, 1 if it died,
        /// and the highest wave started (in training, counting from the episode's start wave).
        /// </summary>
        void ReportEpisode()
        {
            var stats = Academy.Instance.StatsRecorder;
            stats.Add(SurvivalStat, Time.time - _episodeStart);
            stats.Add(KillsStat, _kills);
            stats.Add(DiedStat, _diedThisEpisode ? 1f : 0f);
            stats.Add(WaveStat, _wave);
        }

        void OnWaveStarted(WaveStarted e)
        {
            _wave = Mathf.Max(_wave, e.Number);
            _bossWave = e.IsBoss;
        }

        void OnWaveCleared(WaveCleared _) => AddReward(_bossWave ? bossClearedReward : waveClearedReward);

        void OnEnemyKilled(EnemyKilled _)
        {
            _kills++;
            AddReward(killReward);
        }

        void OnPickupCollected(PickupCollected _) => AddReward(pickupReward);

        void OnPlayerDamaged(PlayerDamaged _) => AddReward(hitPenalty);

        void OnPlayerDied(PlayerDied _)
        {
            AddReward(deathPenalty);
            _died = true;
            _diedThisEpisode = true;
        }
    }
}
