using System;
using UnityEngine;

namespace AS.Feedback
{
    /// <summary>
    /// A one-shot particle burst that returns itself to its pool when its particles have died out
    /// (the system's Stop Action must be Callback, which <see cref="Awake"/> enforces).
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class PooledEffect : MonoBehaviour
    {
        ParticleSystem _particles;
        ParticleSystem.MinMaxGradient _baseColor;
        Action<PooledEffect> _release;

        void Awake()
        {
            _particles = GetComponent<ParticleSystem>();
            var main = _particles.main;
            main.stopAction = ParticleSystemStopAction.Callback;
            _baseColor = main.startColor;
        }

        /// <summary>Plays from the start. A fully transparent <paramref name="tint"/> keeps the authored colour.</summary>
        public void Play(Color tint, Action<PooledEffect> release)
        {
            _release = release;
            var main = _particles.main;
            main.startColor = tint.a > 0f ? new ParticleSystem.MinMaxGradient(tint) : _baseColor;
            _particles.Clear();
            _particles.Play();
        }

        void OnParticleSystemStopped()
        {
            var release = _release;
            _release = null;
            if (release != null) release(this);
            else Destroy(gameObject);
        }
    }
}
