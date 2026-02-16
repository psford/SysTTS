namespace SysTTS.Models;

public record VoiceInfo(string Id, string Name, string ModelPath, string ConfigPath, string TokensPath, int SampleRate);
