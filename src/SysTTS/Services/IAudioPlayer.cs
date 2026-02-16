namespace SysTTS.Services;

public interface IAudioPlayer : IDisposable
{
    Task PlayAsync(float[] samples, int sampleRate, CancellationToken cancellationToken = default);
    void Stop();
}
