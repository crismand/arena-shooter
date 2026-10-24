using System;
using UnityEngine;

namespace AS.Feedback
{
    /// <summary>A clip plus how to play it: volume, random pitch spread and a minimum gap between plays.</summary>
    [Serializable]
    public class SoundCue
    {
        [SerializeField] AudioClip clip;
        [SerializeField, Range(0f, 1f)] float volume = 1f;
        [Tooltip("Pitch is randomised by up to ± this much, so repeats don't sound identical.")]
        [SerializeField, Range(0f, 0.5f)] float pitchJitter = 0.05f;
        [Tooltip("Seconds that must pass before this cue can play again. Stops rapid fire turning into noise.")]
        [SerializeField, Min(0f)] float minInterval = 0.03f;

        public AudioClip Clip => clip;
        public float Volume => volume;
        public float PitchJitter => pitchJitter;
        public float MinInterval => minInterval;
    }
}
