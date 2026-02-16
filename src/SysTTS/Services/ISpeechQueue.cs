using SysTTS.Models;

namespace SysTTS.Services;

public interface ISpeechQueue
{
    string Enqueue(SpeechRequest request);
    Task StopAndClear();
    int QueueDepth { get; }
}
