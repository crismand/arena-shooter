using AS.Core;
using UnityEngine;

namespace AS.Feedback
{
    /// <summary>
    /// Loops the music track and follows the game state: full volume while playing, ducked while
    /// paused, faded out on game over. Fades use unscaled time so they still run while paused.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class MusicPlayer : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] float volume = 0.45f;
        [SerializeField, Range(0f, 1f)] float pausedVolume = 0.15f;
        [Tooltip("Seconds to fade between silence and full volume.")]
        [SerializeField, Min(0.01f)] float fadeTime = 1.5f;

        AudioSource _source;
        float _target;

        void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.loop = true;
            _source.volume = 0f;
            _target = volume;
        }

        void OnEnable() => EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
        void OnDisable() => EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);

        void Start()
        {
            if (!_source.isPlaying && _source.clip != null) _source.Play();
        }

        void Update() =>
            _source.volume = Mathf.MoveTowards(_source.volume, _target, volume / fadeTime * Time.unscaledDeltaTime);

        void OnGameStateChanged(GameStateChanged evt) => _target = evt.Current switch
        {
            GameState.Paused => pausedVolume,
            GameState.GameOver => 0f,
            _ => volume,
        };
    }
}
