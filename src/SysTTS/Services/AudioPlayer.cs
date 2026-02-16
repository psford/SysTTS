using NAudio.Wave;
using Microsoft.Extensions.Logging;

namespace SysTTS.Services;

public class AudioPlayer : IAudioPlayer
{
    private readonly ILogger<AudioPlayer> _logger;

    public AudioPlayer(ILogger<AudioPlayer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Converts float32 samples in range [-1.0, 1.0] to int16 PCM bytes.
    /// Clamps out-of-range values and returns a byte array suitable for WaveStream.
    /// </summary>
    public static byte[] ConvertFloat32ToInt16Pcm(float[] samples)
    {
        if (samples == null)
            throw new ArgumentNullException(nameof(samples));

        byte[] pcmBytes = new byte[samples.Length * 2]; // 2 bytes per sample (int16)

        for (int i = 0; i < samples.Length; i++)
        {
            // Clamp sample to [-1.0, 1.0]
            float clamped = Math.Clamp(samples[i], -1.0f, 1.0f);

            // Scale to int16 range [-32768, 32767]
            // Use absolute value approach: multiply by appropriate max, preserve sign
            short sample;
            if (clamped < 0)
            {
                // For negative values, multiply absolute value by 32768 and negate
                sample = (short)(clamped * 32768f);
            }
            else
            {
                // For positive values, multiply by 32767
                sample = (short)(clamped * 32767f);
            }

            // Write as little-endian int16 (2 bytes)
            int byteIndex = i * 2;
            pcmBytes[byteIndex] = (byte)(sample & 0xFF);
            pcmBytes[byteIndex + 1] = (byte)((sample >> 8) & 0xFF);
        }

        return pcmBytes;
    }

    public async Task PlayAsync(float[] samples, int sampleRate, CancellationToken cancellationToken = default)
    {
        if (samples == null)
            throw new ArgumentNullException(nameof(samples));

        if (samples.Length == 0)
        {
            _logger.LogWarning("PlayAsync called with empty sample array");
            return;
        }

        if (sampleRate <= 0)
            throw new ArgumentException("Sample rate must be positive", nameof(sampleRate));

        try
        {
            byte[] pcmBytes = ConvertFloat32ToInt16Pcm(samples);

            using var memoryStream = new MemoryStream(pcmBytes, false);
            using var waveStream = new RawSourceWaveStream(memoryStream, new WaveFormat(sampleRate, 16, 1));
            using var waveOutEvent = new WaveOutEvent();

            var playbackCompletionSource = new TaskCompletionSource<bool>();

            void PlaybackStoppedHandler(object? sender, StoppedEventArgs e)
            {
                try
                {
                    playbackCompletionSource.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in PlaybackStopped handler");
                    playbackCompletionSource.TrySetException(ex);
                }
            }

            waveOutEvent.PlaybackStopped += PlaybackStoppedHandler;

            try
            {
                waveOutEvent.Init(waveStream);
                waveOutEvent.Play();

                // Register cancellation token
                using var registration = cancellationToken.Register(() =>
                {
                    try
                    {
                        waveOutEvent.Stop();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error stopping playback on cancellation");
                    }
                });

                // Wait for playback to complete or cancellation
                await playbackCompletionSource.Task;
            }
            finally
            {
                waveOutEvent.PlaybackStopped -= PlaybackStoppedHandler;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during audio playback");
            throw;
        }
    }

    public void Stop()
    {
        // Stop is called from cancellation token registration or caller
        // The actual stop happens through the waveOutEvent instance in PlayAsync
        // This method provides explicit stop capability if needed
    }
}
