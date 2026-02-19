using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SherpaOnnx;
using SysTTS.Models;
using SysTTS.Settings;

namespace SysTTS.Services;

public class TtsEngine : ITtsEngine
{
    private readonly IVoiceManager _voiceManager;
    private readonly ServiceSettings _settings;
    private readonly ILogger<TtsEngine> _logger;

    // Cache of OfflineTts instances, one per voice ID
    private readonly ConcurrentDictionary<string, OfflineTts> _ttsInstances =
        new(StringComparer.OrdinalIgnoreCase);

    // Semaphore per voice to serialize synthesis calls (Sherpa-ONNX not thread-safe per instance)
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _synthesizeLocks =
        new(StringComparer.OrdinalIgnoreCase);

    private bool _disposed;

    public TtsEngine(IVoiceManager voiceManager, IOptions<ServiceSettings> settings, ILogger<TtsEngine> logger)
    {
        _voiceManager = voiceManager ?? throw new ArgumentNullException(nameof(voiceManager));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public (float[] Samples, int SampleRate) Synthesize(string text, string voiceId, float speed = 1.0f)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text cannot be null or empty", nameof(text));
        }

        if (string.IsNullOrWhiteSpace(voiceId))
        {
            throw new ArgumentException("Voice ID cannot be null or empty", nameof(voiceId));
        }

        // Resolve voice ID in case requested voice is missing
        var resolvedVoiceId = _voiceManager.ResolveVoiceId(voiceId);
        var voice = _voiceManager.GetVoice(resolvedVoiceId);

        if (voice == null)
        {
            throw new InvalidOperationException($"Voice '{resolvedVoiceId}' not found in voice manager");
        }

        // Get or create TTS instance for this voice
        var tts = _ttsInstances.GetOrAdd(resolvedVoiceId, _ => CreateTtsInstance(voice));

        // Get or create semaphore for this voice
        var lockObject = _synthesizeLocks.GetOrAdd(resolvedVoiceId, _ => new SemaphoreSlim(1, 1));

        // Serialize synthesis calls for this voice (Sherpa-ONNX not thread-safe per instance)
        lockObject.Wait();
        try
        {
            // Speaker ID 0 is the default (single speaker for Piper models)
            var audio = tts.Generate(text, speed, speakerId: 0);
            return (audio.Samples, voice.SampleRate);
        }
        finally
        {
            lockObject.Release();
        }
    }

    /// <summary>
    /// Factory method for creating OfflineTts instances. Protected virtual to allow test subclasses
    /// to override with mocks/fakes for testing lazy-loading and caching behavior.
    /// </summary>
    protected virtual OfflineTts CreateTtsInstance(VoiceInfo voice)
    {
        var config = new OfflineTtsConfig();

        // Piper models are VITS models in Sherpa-ONNX
        config.Model.Vits.Model = voice.ModelPath;
        config.Model.Vits.Tokens = voice.TokensPath;

        // Resolve espeak-ng data directory against the application's base directory,
        // not the current working directory.
        var espeakDataPath = Path.IsPathRooted(_settings.EspeakDataPath)
            ? _settings.EspeakDataPath
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, _settings.EspeakDataPath));
        config.Model.Vits.DataDir = espeakDataPath;

        _logger.LogDebug("Creating TTS instance for voice {VoiceId}: Model={Model}, Tokens={Tokens}, DataDir={DataDir}",
            voice.Id, voice.ModelPath, voice.TokensPath, espeakDataPath);

        config.Model.NumThreads = 2;
        config.Model.Provider = "cpu";

        return new OfflineTts(config);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        // Dispose all cached TTS instances
        foreach (var kvp in _ttsInstances)
        {
            kvp.Value?.Dispose();
        }
        _ttsInstances.Clear();

        // Dispose all semaphores
        foreach (var kvp in _synthesizeLocks)
        {
            kvp.Value?.Dispose();
        }
        _synthesizeLocks.Clear();

        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().Name);
        }
    }
}
