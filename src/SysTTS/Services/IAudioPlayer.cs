namespace SysTTS.Services;

public interface IAudioPlayer
{
    Task PlayAsync(float[] samples, int sampleRate, CancellationToken cancellationToken = default);
    void Stop();
}
