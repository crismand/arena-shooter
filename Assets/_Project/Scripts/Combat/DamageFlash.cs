using UnityEngine;

namespace AS.Combat
{
    /// <summary>Briefly tints a sprite whenever its <see cref="Health"/> takes damage.</summary>
    public class DamageFlash : MonoBehaviour
    {
        [SerializeField] Health health;
        [SerializeField] SpriteRenderer target;
        [SerializeField] Color flashColor = Color.white;
        [SerializeField, Min(0f)] float duration = 0.08f;

        Color _baseColor;
        float _restoreTime;

        void Awake() => _baseColor = target.color;

        void OnEnable() => health.Damaged += OnDamaged;

        void OnDisable()
        {
            health.Damaged -= OnDamaged;
            target.color = _baseColor;
            _restoreTime = 0f;
        }

        void Update()
        {
            if (_restoreTime > 0f && Time.time >= _restoreTime)
            {
                target.color = _baseColor;
                _restoreTime = 0f;
            }
        }

        void OnDamaged(DamageInfo _)
        {
            target.color = flashColor;
            _restoreTime = Time.time + duration;
        }
    }
}
