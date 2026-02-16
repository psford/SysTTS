namespace SysTTS.Models;

public record SpeechRequest(
    string Id,
    string Text,
    string VoiceId,
    int Priority,
    string? Source);
