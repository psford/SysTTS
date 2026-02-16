using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SysTTS.Models;
using SysTTS.Settings;

namespace SysTTS.Services;

public class VoiceManager : IVoiceManager
{
    private readonly string _voicesPath;
    private readonly string _defaultVoice;
    private readonly ILogger<VoiceManager> _logger;
    private readonly ReaderWriterLockSlim _catalogLock = new();
    private readonly FileSystemWatcher _watcher;
    private List<VoiceInfo> _catalog = new();
    private bool _disposed = false;

    public VoiceManager(IOptions<ServiceSettings> options, ILogger<VoiceManager> logger)
    {
        _voicesPath = options.Value.VoicesPath;
        _defaultVoice = options.Value.DefaultVoice;
        _logger = logger;

        // Create voices directory if it doesn't exist
        if (!Directory.Exists(_voicesPath))
        {
            Directory.CreateDirectory(_voicesPath);
        }

        // Initial scan
        ScanVoices();

        // Set up FileSystemWatcher
        _watcher = new FileSystemWatcher(_voicesPath)
        {
            Filter = "*.onnx",
            NotifyFilter = NotifyFilters.FileName
        };

        _watcher.Created += (s, e) => DebounceRescan();
        _watcher.Deleted += (s, e) => DebounceRescan();
        _watcher.EnableRaisingEvents = true;
    }

    public IReadOnlyList<VoiceInfo> GetAvailableVoices()
    {
        _catalogLock.EnterReadLock();
        try
        {
            return _catalog.AsReadOnly();
        }
        finally
        {
            _catalogLock.ExitReadLock();
        }
    }

    public VoiceInfo? GetVoice(string voiceId)
    {
        _catalogLock.EnterReadLock();
        try
        {
            return _catalog.FirstOrDefault(v => v.Id == voiceId);
        }
        finally
        {
            _catalogLock.ExitReadLock();
        }
    }

    public string ResolveVoiceId(string? requestedVoiceId)
    {
        if (requestedVoiceId == null)
        {
            return _defaultVoice;
        }

        _catalogLock.EnterReadLock();
        try
        {
            if (_catalog.Any(v => v.Id == requestedVoiceId))
            {
                return requestedVoiceId;
            }
        }
        finally
        {
            _catalogLock.ExitReadLock();
        }

        _logger.LogWarning($"Requested voice '{requestedVoiceId}' not found. Falling back to default voice '{_defaultVoice}'.");
        return _defaultVoice;
    }

    private void ScanVoices()
    {
        var newCatalog = new List<VoiceInfo>();

        if (!Directory.Exists(_voicesPath))
        {
            _catalogLock.EnterWriteLock();
            try
            {
                _catalog = newCatalog;
            }
            finally
            {
                _catalogLock.ExitWriteLock();
            }
            return;
        }

        var onnxFiles = Directory.GetFiles(_voicesPath, "*.onnx");

        foreach (var onnxPath in onnxFiles)
        {
            var jsonPath = onnxPath + ".json";

            if (!File.Exists(jsonPath))
            {
                _logger.LogWarning($"Orphaned ONNX file without matching JSON: {onnxPath}");
                continue;
            }

            try
            {
                var voiceInfo = ParseVoiceInfo(onnxPath, jsonPath);
                if (voiceInfo != null)
                {
                    newCatalog.Add(voiceInfo);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to parse voice metadata from {jsonPath}: {ex.Message}");
            }
        }

        _catalogLock.EnterWriteLock();
        try
        {
            _catalog = newCatalog;
        }
        finally
        {
            _catalogLock.ExitWriteLock();
        }

        _logger.LogInformation($"Voice catalog updated. Found {newCatalog.Count} valid voice(s).");
    }

    private VoiceInfo? ParseVoiceInfo(string onnxPath, string jsonPath)
    {
        try
        {
            var jsonText = File.ReadAllText(jsonPath);
            using var doc = JsonDocument.Parse(jsonText);
            var root = doc.RootElement;

            int sampleRate = 22050; // Default fallback
            if (root.TryGetProperty("audio", out var audioElement) &&
                audioElement.TryGetProperty("sample_rate", out var sampleRateElement) &&
                sampleRateElement.TryGetInt32(out var sr))
            {
                sampleRate = sr;
            }

            var voiceId = Path.GetFileNameWithoutExtension(onnxPath);
            var voiceName = voiceId; // Use ID as name (can be enhanced later)

            return new VoiceInfo(
                Id: voiceId,
                Name: voiceName,
                ModelPath: Path.GetFullPath(onnxPath),
                ConfigPath: Path.GetFullPath(jsonPath),
                SampleRate: sampleRate
            );
        }
        catch (JsonException ex)
        {
            _logger.LogError($"Invalid JSON in {jsonPath}: {ex.Message}");
            return null;
        }
    }

    private object _rescanLock = new object();
    private CancellationTokenSource? _rescanCts;

    private void DebounceRescan()
    {
        lock (_rescanLock)
        {
            _rescanCts?.Cancel();
            _rescanCts = new CancellationTokenSource();
            var cts = _rescanCts;

            Task.Delay(100, cts.Token)
                .ContinueWith(_ =>
                {
                    if (!cts.Token.IsCancellationRequested)
                    {
                        ScanVoices();
                    }
                }, TaskScheduler.Default);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _watcher?.Dispose();
        _catalogLock?.Dispose();
        _rescanCts?.Dispose();
        _disposed = true;
    }
}
