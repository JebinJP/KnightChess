using System.IO;
using System.Text;
using KnightLabChess.Core.Audio;

namespace KnightLabChess.Infrastructure.Audio;

/// <summary>
/// High-fidelity procedural audio synthesizer for chess sound effects.
/// Generates standard 16-bit PCM WAV audio streams in memory with zero external dependencies.
/// </summary>
public static class SoundEffectGenerator
{
    private const int SampleRate = 44100;

    /// <summary>
    /// Generates a valid RIFF/WAVE 16-bit mono 44.1kHz audio stream for the given sound effect.
    /// </summary>
    public static byte[] Generate(ChessSound sound)
    {
        return sound switch
        {
            ChessSound.Move => GenerateMoveSound(),
            ChessSound.Capture => GenerateCaptureSound(),
            ChessSound.Check => GenerateCheckSound(),
            ChessSound.Castle => GenerateCastleSound(),
            ChessSound.Promote => GeneratePromoteSound(),
            ChessSound.Checkmate => GenerateCheckmateSound(),
            ChessSound.GameOver => GenerateGameOverSound(),
            ChessSound.PuzzleSolved => GeneratePuzzleSolvedSound(),
            _ => GenerateMoveSound()
        };
    }

    /// <summary>
    /// Adjusts the volume of a 16-bit PCM WAV byte array by scaling sample amplitudes.
    /// </summary>
    public static byte[] AdjustVolume(byte[] wavBytes, int volumePercent)
    {
        if (wavBytes.Length <= 44) return wavBytes;
        if (volumePercent >= 100) return wavBytes;
        if (volumePercent <= 0) volumePercent = 0;

        float factor = volumePercent / 100.0f;
        byte[] copy = new byte[wavBytes.Length];
        Buffer.BlockCopy(wavBytes, 0, copy, 0, 44); // Keep standard 44-byte header unchanged

        for (int i = 44; i < wavBytes.Length - 1; i += 2)
        {
            short sample = BitConverter.ToInt16(wavBytes, i);
            short scaled = (short)Math.Clamp((int)(sample * factor), short.MinValue, short.MaxValue);
            copy[i] = (byte)(scaled & 0xFF);
            copy[i + 1] = (byte)((scaled >> 8) & 0xFF);
        }

        return copy;
    }

    // 1. Move: subtle tactile wood knock (80ms)
    private static byte[] GenerateMoveSound()
    {
        int samples = (int)(SampleRate * 0.08);
        double[] buffer = new double[samples];
        var rng = new Random(42);

        for (int i = 0; i < samples; i++)
        {
            double t = (double)i / SampleRate;
            double decay = Math.Exp(-t * 55.0);
            double sine = (Math.Sin(2.0 * Math.PI * 240.0 * t) * 0.7) + (Math.Sin(2.0 * Math.PI * 480.0 * t) * 0.2);
            double noise = (rng.NextDouble() * 2.0 - 1.0) * Math.Exp(-t * 180.0) * 0.3;
            buffer[i] = (sine + noise) * decay;
        }

        return CreateWavBytes(buffer);
    }

    // 2. Capture: sharp dual impact (130ms)
    private static byte[] GenerateCaptureSound()
    {
        int samples = (int)(SampleRate * 0.13);
        double[] buffer = new double[samples];
        var rng = new Random(1337);

        for (int i = 0; i < samples; i++)
        {
            double t = (double)i / SampleRate;
            // Primary high transient strike
            double h1 = (Math.Sin(2.0 * Math.PI * 460.0 * t) * Math.Exp(-t * 50.0) * 0.7) +
                        ((rng.NextDouble() * 2.0 - 1.0) * Math.Exp(-t * 160.0) * 0.35);

            // Rebound impact at 35ms
            double h2 = 0.0;
            if (t > 0.035)
            {
                double t2 = t - 0.035;
                h2 = Math.Sin(2.0 * Math.PI * 280.0 * t2) * Math.Exp(-t2 * 60.0) * 0.6;
            }

            buffer[i] = Math.Clamp(h1 + h2, -1.0, 1.0);
        }

        return CreateWavBytes(buffer);
    }

    // 3. Check: resonant alert chime / ping (240ms)
    private static byte[] GenerateCheckSound()
    {
        int samples = (int)(SampleRate * 0.24);
        double[] buffer = new double[samples];

        for (int i = 0; i < samples; i++)
        {
            double t = (double)i / SampleRate;
            double decay = Math.Exp(-t * 14.0);
            double s1 = Math.Sin(2.0 * Math.PI * 880.0 * t) * 0.6;   // A5
            double s2 = Math.Sin(2.0 * Math.PI * 1320.0 * t) * 0.35; // E6
            double s3 = Math.Sin(2.0 * Math.PI * 1760.0 * t) * 0.15; // A6
            buffer[i] = (s1 + s2 + s3) * decay;
        }

        return CreateWavBytes(buffer);
    }

    // 4. Castle: double wood slide/tap (220ms)
    private static byte[] GenerateCastleSound()
    {
        int samples = (int)(SampleRate * 0.22);
        double[] buffer = new double[samples];
        var rng = new Random(101);

        for (int i = 0; i < samples; i++)
        {
            double t = (double)i / SampleRate;
            double h1 = (Math.Sin(2.0 * Math.PI * 260.0 * t) * Math.Exp(-t * 50.0) * 0.6) +
                        ((rng.NextDouble() * 2.0 - 1.0) * Math.Exp(-t * 140.0) * 0.25);

            double h2 = 0.0;
            if (t > 0.08)
            {
                double t2 = t - 0.08;
                h2 = (Math.Sin(2.0 * Math.PI * 220.0 * t2) * Math.Exp(-t2 * 50.0) * 0.65) +
                     ((rng.NextDouble() * 2.0 - 1.0) * Math.Exp(-t2 * 140.0) * 0.25);
            }

            buffer[i] = Math.Clamp(h1 + h2, -1.0, 1.0);
        }

        return CreateWavBytes(buffer);
    }

    // 5. Promote: ascending fanfare triad (360ms)
    private static byte[] GeneratePromoteSound()
    {
        int samples = (int)(SampleRate * 0.36);
        double[] buffer = new double[samples];

        for (int i = 0; i < samples; i++)
        {
            double t = (double)i / SampleRate;
            double val = 0.0;
            if (t >= 0.0) val += Math.Sin(2.0 * Math.PI * 523.25 * t) * Math.Exp(-t * 12.0) * 0.35; // C5
            if (t >= 0.08)
            {
                double t2 = t - 0.08;
                val += Math.Sin(2.0 * Math.PI * 659.25 * t2) * Math.Exp(-t2 * 12.0) * 0.35; // E5
            }
            if (t >= 0.16)
            {
                double t3 = t - 0.16;
                val += Math.Sin(2.0 * Math.PI * 783.99 * t3) * Math.Exp(-t3 * 9.0) * 0.45; // G5
            }
            if (t >= 0.22)
            {
                double t4 = t - 0.22;
                val += Math.Sin(2.0 * Math.PI * 1046.50 * t4) * Math.Exp(-t4 * 10.0) * 0.3; // C6
            }

            buffer[i] = Math.Clamp(val, -1.0, 1.0);
        }

        return CreateWavBytes(buffer);
    }

    // 6. Checkmate: dramatic cadence (550ms)
    private static byte[] GenerateCheckmateSound()
    {
        int samples = (int)(SampleRate * 0.55);
        double[] buffer = new double[samples];

        for (int i = 0; i < samples; i++)
        {
            double t = (double)i / SampleRate;
            double decay = Math.Exp(-t * 6.0);
            double s1 = Math.Sin(2.0 * Math.PI * 220.0 * t) * 0.45;   // A3
            double s2 = Math.Sin(2.0 * Math.PI * 330.0 * t) * 0.3;    // E4
            double s3 = Math.Sin(2.0 * Math.PI * 440.0 * t) * 0.25;   // A4
            double s4 = Math.Sin(2.0 * Math.PI * 554.37 * t) * 0.2;   // C#5
            buffer[i] = (s1 + s2 + s3 + s4) * decay;
        }

        return CreateWavBytes(buffer);
    }

    // 7. GameOver: soft resolving chord (450ms)
    private static byte[] GenerateGameOverSound()
    {
        int samples = (int)(SampleRate * 0.45);
        double[] buffer = new double[samples];

        for (int i = 0; i < samples; i++)
        {
            double t = (double)i / SampleRate;
            double decay = Math.Exp(-t * 7.0);
            double s1 = Math.Sin(2.0 * Math.PI * 392.00 * t) * 0.5;   // G4
            double s2 = Math.Sin(2.0 * Math.PI * 329.63 * t) * 0.4;   // E4
            double s3 = Math.Sin(2.0 * Math.PI * 261.63 * t) * 0.3;   // C4
            buffer[i] = (s1 + s2 + s3) * decay;
        }

        return CreateWavBytes(buffer);
    }

    // 8. PuzzleSolved: celebratory double chime (380ms)
    private static byte[] GeneratePuzzleSolvedSound()
    {
        int samples = (int)(SampleRate * 0.38);
        double[] buffer = new double[samples];

        for (int i = 0; i < samples; i++)
        {
            double t = (double)i / SampleRate;
            double val = 0.0;
            if (t >= 0.0) val += Math.Sin(2.0 * Math.PI * 659.25 * t) * Math.Exp(-t * 14.0) * 0.5; // E5
            if (t >= 0.12)
            {
                double t2 = t - 0.12;
                val += Math.Sin(2.0 * Math.PI * 880.0 * t2) * Math.Exp(-t2 * 10.0) * 0.6;   // A5
                val += Math.Sin(2.0 * Math.PI * 1320.0 * t2) * Math.Exp(-t2 * 12.0) * 0.2; // E6
            }

            buffer[i] = Math.Clamp(val, -1.0, 1.0);
        }

        return CreateWavBytes(buffer);
    }

    private static byte[] CreateWavBytes(double[] samples)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        int numSamples = samples.Length;
        int byteRate = SampleRate * 2;
        short blockAlign = 2;
        int dataChunkSize = numSamples * 2;
        int riffChunkSize = 36 + dataChunkSize;

        bw.Write(Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(riffChunkSize);
        bw.Write(Encoding.ASCII.GetBytes("WAVE"));
        bw.Write(Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16);          // Subchunk1Size
        bw.Write((short)1);    // AudioFormat (PCM)
        bw.Write((short)1);    // NumChannels (Mono)
        bw.Write(SampleRate);  // SampleRate
        bw.Write(byteRate);    // ByteRate
        bw.Write(blockAlign);  // BlockAlign
        bw.Write((short)16);   // BitsPerSample
        bw.Write(Encoding.ASCII.GetBytes("data"));
        bw.Write(dataChunkSize);

        for (int i = 0; i < numSamples; i++)
        {
            short val = (short)Math.Clamp((int)(samples[i] * 32767.0), short.MinValue, short.MaxValue);
            bw.Write(val);
        }

        return ms.ToArray();
    }
}
