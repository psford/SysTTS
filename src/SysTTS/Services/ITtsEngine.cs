namespace SysTTS.Services;

public interface ITtsEngine : IDisposable
{
    (float[] Samples, int SampleRate) Synthesize(string text, string voiceId, float speed = 1.0f);
}
