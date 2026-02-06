using System;

namespace TeaLeaves
{
    /// <summary>
    /// Procedural audio generator for PIKUI.
    /// Pure C# - generates raw PCM data for AudioStreamWav.
    /// Each direction has a unique waveform timbre, tiles map to pentatonic scale notes.
    /// </summary>
    public static class PikuiSounds
    {
        private static readonly float[] Pentatonic = { 261.63f, 293.66f, 329.63f, 392.00f, 440.00f };
        private const int SampleRate = 22050;

        public enum Waveform { Sine, Square, Triangle, Sawtooth }

        public static Waveform DirectionWaveform(SlideDirection dir) => dir switch
        {
            SlideDirection.Up => Waveform.Sine,
            SlideDirection.Right => Waveform.Square,
            SlideDirection.Down => Waveform.Sawtooth,
            SlideDirection.Left => Waveform.Triangle,
            _ => Waveform.Sine
        };

        public static float GetTileFrequency(int x, int y, int gridSize)
        {
            int noteIndex = (x + y) % Pentatonic.Length;
            float freq = Pentatonic[noteIndex];
            int dist = Math.Abs(x - gridSize / 2) + Math.Abs(y - gridSize / 2);
            if (dist > gridSize / 2) freq *= 2.0f;
            return freq;
        }

        public static byte[] GenerateNote(float frequency, Waveform waveform, float duration = 0.15f, float volume = 0.3f)
        {
            int samples = (int)(SampleRate * duration);
            var data = new byte[samples * 2];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float env = 1.0f - (float)i / samples;
                env *= env;

                float s = waveform switch
                {
                    Waveform.Sine => MathF.Sin(2 * MathF.PI * frequency * t),
                    Waveform.Square => MathF.Sin(2 * MathF.PI * frequency * t) >= 0 ? 0.5f : -0.5f,
                    Waveform.Triangle => 2.0f * MathF.Abs(2.0f * (frequency * t - MathF.Floor(frequency * t + 0.5f))) - 1.0f,
                    Waveform.Sawtooth => 2.0f * (frequency * t - MathF.Floor(frequency * t + 0.5f)),
                    _ => 0f
                };

                short val = (short)(s * env * volume * 32767);
                data[i * 2] = (byte)(val & 0xFF);
                data[i * 2 + 1] = (byte)((val >> 8) & 0xFF);
            }

            return data;
        }

        public static byte[] GenerateVictory()
        {
            float[] freqs = { 261.63f, 329.63f, 392.00f, 523.25f };
            float noteDur = 0.2f;
            int totalSamples = (int)(SampleRate * noteDur * freqs.Length);
            var data = new byte[totalSamples * 2];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                int ni = Math.Min((int)(t / noteDur), freqs.Length - 1);
                float nt = t - ni * noteDur;
                float env = 1.0f - nt / noteDur;
                env *= env;
                float s = MathF.Sin(2 * MathF.PI * freqs[ni] * t) * env * 0.35f;
                short val = (short)(s * 32767);
                data[i * 2] = (byte)(val & 0xFF);
                data[i * 2 + 1] = (byte)((val >> 8) & 0xFF);
            }

            return data;
        }

        public static byte[] GenerateAmbientLoop(float progress)
        {
            float duration = 2.0f;
            int samples = (int)(SampleRate * duration);
            var data = new byte[samples * 2];
            float baseFreq = 65.41f; // C2 drone

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float fade = MathF.Sin(MathF.PI * t / duration);
                float s = MathF.Sin(2 * MathF.PI * baseFreq * t) * 0.15f;
                s += MathF.Sin(2 * MathF.PI * baseFreq * 1.5f * t) * 0.08f * progress;
                s += MathF.Sin(2 * MathF.PI * baseFreq * 2.0f * t) * 0.06f * progress;
                s *= fade;
                short val = (short)(s * 32767);
                data[i * 2] = (byte)(val & 0xFF);
                data[i * 2 + 1] = (byte)((val >> 8) & 0xFF);
            }

            return data;
        }

        public static byte[] GenerateBump()
        {
            int samples = (int)(SampleRate * 0.08f);
            var data = new byte[samples * 2];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float env = 1.0f - (float)i / samples;
                float s = MathF.Sin(2 * MathF.PI * 150f * t) * env * env * 0.2f;
                short val = (short)(s * 32767);
                data[i * 2] = (byte)(val & 0xFF);
                data[i * 2 + 1] = (byte)((val >> 8) & 0xFF);
            }
            return data;
        }

        public static int GetSampleRate() => SampleRate;
    }
}
