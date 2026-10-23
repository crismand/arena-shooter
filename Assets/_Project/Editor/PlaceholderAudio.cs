using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AS.EditorTools
{
    /// <summary>
    /// Synthesises the placeholder sound effects and music loop as WAV files, so the game has audio
    /// without any external assets. Like the placeholder sprites, a file is only written if missing,
    /// so a clip replaced with a real recording at the same path is kept. Noise uses a fixed seed:
    /// a rebuild produces byte-identical files.
    /// </summary>
    public static class PlaceholderAudio
    {
        const int Rate = 44100;

        public enum Wave { Sine, Square, Saw, Triangle }

        public static AudioClip Ensure(string path, bool music, Func<float[]> synthesize)
        {
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                WriteWav(path, synthesize());
                AssetDatabase.ImportAsset(path);

                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                importer.forceToMono = true;
                var settings = importer.defaultSampleSettings;
                settings.loadType = music ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
                settings.quality = 0.6f;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        // ------------------------------------------------------------------ sound effects

        /// <summary>
        /// One tone gliding from <paramref name="startHz"/> to <paramref name="endHz"/>, mixed with
        /// some noise and dying away exponentially. Most retro effects are just this with different numbers.
        /// </summary>
        public static float[] Sweep(float seconds, float startHz, float endHz, Wave wave, float decay,
                                    float noise = 0f, float gain = 0.5f, int seed = 1)
        {
            var random = new System.Random(seed);
            int count = Mathf.CeilToInt(seconds * Rate);
            var samples = new float[count];
            double phase = 0;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                phase += startHz * Mathf.Pow(endHz / startHz, t) / Rate;
                float tone = Oscillator(wave, phase);
                float value = Mathf.Lerp(tone, Noise(random), noise);
                samples[i] = value * Mathf.Exp(-decay * t) * EdgeFade(i, count) * gain;
            }
            return samples;
        }

        /// <summary>Short notes played one after another (MIDI note numbers), like a pickup jingle.</summary>
        public static float[] Notes(float noteSeconds, Wave wave, float decay, float gain, params int[] midiNotes)
        {
            int noteLength = Mathf.CeilToInt(noteSeconds * Rate);
            var samples = new float[noteLength * midiNotes.Length];
            for (int n = 0; n < midiNotes.Length; n++)
                AddTone(samples, n * noteLength, noteLength, Frequency(midiNotes[n]), wave, decay, gain, wrap: false);
            return samples;
        }

        // ------------------------------------------------------------------ music

        /// <summary>
        /// A four-bar loop at 128 BPM over Am-F-C-G: kick on the beat, hi-hat off the beat, an
        /// eighth-note bass and a sixteenth-note arpeggio. Exactly four bars long, and tails that run
        /// past the end wrap to the start, so it loops without a seam.
        /// </summary>
        public static float[] MusicLoop()
        {
            const float bpm = 128f;
            const int stepsPerBar = 16;
            int stepLength = Mathf.RoundToInt(60f / bpm / 4f * Rate);
            var samples = new float[stepLength * stepsPerBar * 4];
            var random = new System.Random(7);

            int[] bassRoots = { 45, 41, 48, 43 };           // A2 F2 C3 G2
            int[][] chords =
            {
                new[] { 69, 72, 76 }, new[] { 65, 69, 72 }, // Am, F
                new[] { 72, 76, 79 }, new[] { 67, 71, 74 }, // C, G
            };
            int[] arpPattern = { 0, 1, 2, 1, 0, 2, 1, 2 };

            for (int bar = 0; bar < 4; bar++)
            {
                for (int step = 0; step < stepsPerBar; step++)
                {
                    int start = (bar * stepsPerBar + step) * stepLength;

                    if (step % 4 == 0) AddKick(samples, start, stepLength * 2);
                    if (step % 4 == 2) AddHat(samples, start, stepLength, random);
                    if (step % 2 == 0)
                    {
                        // Root on the beat, octave up off the beat.
                        int note = bassRoots[bar] + (step % 4 == 2 ? 12 : 0);
                        AddTone(samples, start, stepLength * 2, Frequency(note), Wave.Saw, 3f, 0.16f, wrap: true);
                    }
                    int arpNote = chords[bar][arpPattern[step % arpPattern.Length]];
                    AddTone(samples, start, stepLength, Frequency(arpNote), Wave.Square, 6f, 0.06f, wrap: true);
                }
            }

            LowPass(samples, 0.35f);
            Normalize(samples, 0.8f);
            return samples;
        }

        static void AddKick(float[] buffer, int start, int length)
        {
            double phase = 0;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)length;
                phase += Mathf.Lerp(130f, 40f, Mathf.Sqrt(t)) / Rate;
                Mix(buffer, start + i, Mathf.Sin((float)(phase * 2.0 * Math.PI)) * Mathf.Exp(-6f * t) * 0.55f);
            }
        }

        static void AddHat(float[] buffer, int start, int length, System.Random random)
        {
            float previous = 0f;
            for (int i = 0; i < length; i++)
            {
                // Difference of successive noise samples: a crude high-pass that keeps the hiss bright.
                float noise = Noise(random);
                float bright = noise - previous;
                previous = noise;
                Mix(buffer, start + i, bright * Mathf.Exp(-14f * i / length) * 0.05f);
            }
        }

        static void AddTone(float[] buffer, int start, int length, float hz, Wave wave, float decay, float gain, bool wrap)
        {
            for (int i = 0; i < length; i++)
            {
                int index = start + i;
                if (index >= buffer.Length && !wrap) break;
                float t = i / (float)length;
                float value = Oscillator(wave, hz * i / (double)Rate) * Mathf.Exp(-decay * t) * EdgeFade(i, length) * gain;
                Mix(buffer, index, value);
            }
        }

        // ------------------------------------------------------------------ building blocks

        static float Frequency(int midiNote) => 440f * Mathf.Pow(2f, (midiNote - 69) / 12f);

        /// <summary>One cycle per unit of <paramref name="phase"/>, output -1..1.</summary>
        static float Oscillator(Wave wave, double phase)
        {
            float p = (float)(phase - Math.Floor(phase));
            return wave switch
            {
                Wave.Sine => Mathf.Sin(p * 2f * Mathf.PI),
                Wave.Square => p < 0.5f ? 1f : -1f,
                Wave.Saw => 2f * p - 1f,
                _ => 1f - 4f * Mathf.Abs(p - 0.5f),
            };
        }

        static float Noise(System.Random random) => (float)(random.NextDouble() * 2.0 - 1.0);

        /// <summary>Ramps the first 2 ms and last 5 ms so a sound never starts or stops with a click.</summary>
        static float EdgeFade(int i, int count)
        {
            const int attack = Rate / 500, release = Rate / 200;
            return Mathf.Min(1f, i / (float)attack, (count - 1 - i) / (float)release);
        }

        /// <summary>Adds into the buffer, wrapping past the end (used for seamless loops).</summary>
        static void Mix(float[] buffer, int index, float value) => buffer[index % buffer.Length] += value;

        /// <summary>One-pole low-pass. <paramref name="amount"/> near 1 lets everything through.</summary>
        static void LowPass(float[] samples, float amount)
        {
            float state = 0f;
            for (int i = 0; i < samples.Length; i++)
                samples[i] = state += (samples[i] - state) * amount;
        }

        static void Normalize(float[] samples, float peak)
        {
            float max = 0f;
            foreach (float s in samples) max = Mathf.Max(max, Mathf.Abs(s));
            if (max <= 0f) return;
            for (int i = 0; i < samples.Length; i++) samples[i] *= peak / max;
        }

        /// <summary>Mono 16-bit PCM WAV.</summary>
        static void WriteWav(string path, float[] samples)
        {
            using var writer = new BinaryWriter(File.Create(path));
            int dataBytes = samples.Length * 2;
            writer.Write("RIFF".ToCharArray());
            writer.Write(36 + dataBytes);
            writer.Write("WAVE".ToCharArray());
            writer.Write("fmt ".ToCharArray());
            writer.Write(16);              // fmt chunk size
            writer.Write((short)1);        // PCM
            writer.Write((short)1);        // mono
            writer.Write(Rate);
            writer.Write(Rate * 2);        // byte rate
            writer.Write((short)2);        // block align
            writer.Write((short)16);       // bits per sample
            writer.Write("data".ToCharArray());
            writer.Write(dataBytes);
            foreach (float s in samples) writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(s, -1f, 1f) * short.MaxValue));
        }
    }
}
