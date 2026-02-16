using SysTTS.Models;

namespace SysTTS.Services;

public interface IVoiceManager : IDisposable
{
    IReadOnlyList<VoiceInfo> GetAvailableVoices();
    VoiceInfo? GetVoice(string voiceId);
    string ResolveVoiceId(string? requestedVoiceId);
}
