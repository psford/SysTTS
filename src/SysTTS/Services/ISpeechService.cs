namespace SysTTS.Services;

public interface ISpeechService
{
    (bool Queued, string? Id) ProcessSpeakRequest(string text, string? source, string? voiceOverride);
}
