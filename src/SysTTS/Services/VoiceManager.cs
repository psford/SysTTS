using System.Diagnostics;
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
        // Resolve relative VoicesPath against the application's base directory,
        // not the current working directory. This ensures voices are found
        // regardless of where the exe is launched from.
        var configuredPath = options.Value.VoicesPath;
        _voicesPath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuredPath));
        _defaultVoice = options.Value.DefaultVoice;
        _logger = logger;

        // Create voices directory if it doesn't exist
        if (!Directory.Exists(_voicesPath))
        {
            Directory.CreateDirectory(_voicesPath);
        }

        // Initial scan
        ScanVoices();

        // Set up FileSystemWatcher for both ONNX and JSON files
        _watcher = new FileSystemWatcher(_voicesPath)
        {
            Filter = "*.*",
            NotifyFilter = NotifyFilters.FileName
        };

        _watcher.Created += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.Name) &&
                (e.Name.EndsWith(".onnx", StringComparison.OrdinalIgnoreCase) ||
                e.Name.EndsWith(".onnx.json", StringComparison.OrdinalIgnoreCase)))
            {
                DebounceRescan();
            }
        };
        _watcher.Deleted += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.Name) &&
                (e.Name.EndsWith(".onnx", StringComparison.OrdinalIgnoreCase) ||
                e.Name.EndsWith(".onnx.json", StringComparison.OrdinalIgnoreCase)))
            {
                DebounceRescan();
            }
        };
        _watcher.EnableRaisingEvents = true;
    }

    public IReadOnlyList<VoiceInfo> GetAvailableVoices()
    {
        ThrowIfDisposed();
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
        ThrowIfDisposed();
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
        ThrowIfDisposed();
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

        _logger.LogWarning("Requested voice '{RequestedVoiceId}' not found. Falling back to default voice '{DefaultVoice}'.", requestedVoiceId, _defaultVoice);
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
                _logger.LogWarning("Orphaned ONNX file without matching JSON: {OnnxPath}", onnxPath);
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
                _logger.LogError(ex, "Failed to parse voice metadata from {JsonPath}", jsonPath);
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

        _logger.LogInformation("Voice catalog updated. Found {VoiceCount} valid voice(s).", newCatalog.Count);
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

            // Ensure the ONNX model has required metadata (sample_rate, model_type, etc.).
            // Piper models from HuggingFace don't include this; sherpa-onnx crashes without it.
            // This auto-patches the model file once — subsequent scans skip the check.
            EnsureModelMetadata(onnxPath, jsonPath);

            // Generate tokens.txt from phoneme_id_map if it doesn't exist.
            // Sherpa-ONNX requires a tokens file for VITS/Piper models.
            var tokensPath = Path.Combine(
                Path.GetDirectoryName(onnxPath)!,
                Path.GetFileNameWithoutExtension(onnxPath) + ".tokens.txt");
            tokensPath = Path.GetFullPath(tokensPath);

            if (!File.Exists(tokensPath))
            {
                GenerateTokensFile(root, tokensPath);
            }

            var voiceId = Path.GetFileNameWithoutExtension(onnxPath);
            var voiceName = voiceId; // Use ID as name (can be enhanced later)

            return new VoiceInfo(
                Id: voiceId,
                Name: voiceName,
                ModelPath: Path.GetFullPath(onnxPath),
                ConfigPath: Path.GetFullPath(jsonPath),
                TokensPath: tokensPath,
                SampleRate: sampleRate
            );
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid JSON in {JsonPath}", jsonPath);
            return null;
        }
    }

    /// <summary>
    /// Generates a tokens.txt file from the Piper model's phoneme_id_map.
    /// Sherpa-ONNX requires this file for VITS model phoneme-to-ID mapping.
    /// Format: one line per token as "&lt;symbol&gt; &lt;id&gt;".
    /// </summary>
    private void GenerateTokensFile(JsonElement root, string tokensPath)
    {
        if (!root.TryGetProperty("phoneme_id_map", out var idMap))
        {
            _logger.LogWarning("No phoneme_id_map found in config, cannot generate tokens file: {TokensPath}", tokensPath);
            return;
        }

        // Build id-to-symbol mapping
        var tokens = new SortedDictionary<int, string>();
        foreach (var prop in idMap.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.Array && prop.Value.GetArrayLength() > 0)
            {
                var id = prop.Value[0].GetInt32();
                tokens[id] = prop.Name;
            }
        }

        if (tokens.Count == 0)
        {
            _logger.LogWarning("phoneme_id_map is empty, cannot generate tokens file: {TokensPath}", tokensPath);
            return;
        }

        // Write tokens file: <symbol> <id> per line, filling gaps with empty entries.
        // CRITICAL: Use LF-only line endings and no UTF-8 BOM.
        // The native sherpa-onnx C++ parser includes \r in parsed data if CRLF is used,
        // which corrupts the token mapping and causes a native crash.
        int maxId = tokens.Keys.Max();
        using var writer = new StreamWriter(tokensPath, false, new System.Text.UTF8Encoding(false));
        writer.NewLine = "\n";
        for (int i = 0; i <= maxId; i++)
        {
            var symbol = tokens.TryGetValue(i, out var s) ? s : "";
            writer.WriteLine($"{symbol} {i}");
        }

        _logger.LogInformation("Generated tokens file with {Count} entries: {TokensPath}", maxId + 1, tokensPath);
    }

    /// <summary>
    /// Checks if an ONNX model file has the required 'sample_rate' metadata.
    /// If missing, invokes scripts/ensure_model_metadata.py to patch the model.
    /// This is a one-time operation per model — once patched, subsequent checks are instant.
    /// </summary>
    private void EnsureModelMetadata(string onnxPath, string jsonPath)
    {
        if (ModelHasRequiredMetadata(onnxPath))
            return;

        _logger.LogWarning("ONNX model missing required metadata (sample_rate): {OnnxPath}. Attempting auto-patch...", onnxPath);

        var scriptPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "scripts", "ensure_model_metadata.py"));
        if (!File.Exists(scriptPath))
        {
            // Try relative to CWD as fallback
            scriptPath = Path.GetFullPath("scripts/ensure_model_metadata.py");
        }

        if (!File.Exists(scriptPath))
        {
            _logger.LogError("Cannot auto-patch model: ensure_model_metadata.py not found. " +
                "Run 'pip install onnx' and 'python scripts/ensure_model_metadata.py \"{OnnxPath}\" \"{JsonPath}\"' manually.",
                onnxPath, jsonPath);
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"\"{scriptPath}\" \"{Path.GetFullPath(onnxPath)}\" \"{Path.GetFullPath(jsonPath)}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                _logger.LogError("Failed to start Python process for model metadata patching");
                return;
            }

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(30_000); // 30 second timeout

            if (process.ExitCode == 0)
            {
                _logger.LogInformation("Auto-patched model metadata: {Output}", stdout.Trim());
            }
            else
            {
                _logger.LogError("Failed to patch model metadata (exit code {ExitCode}): {Stderr}", process.ExitCode, stderr.Trim());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running ensure_model_metadata.py for {OnnxPath}", onnxPath);
        }
    }

    /// <summary>
    /// Quick binary check: does the ONNX file contain 'sample_rate' as a metadata key?
    /// Reads the last portion of the file where protobuf metadata is typically stored.
    /// Skips files under 10KB as they can't be real ONNX models.
    /// </summary>
    private static bool ModelHasRequiredMetadata(string onnxPath)
    {
        try
        {
            var fileInfo = new FileInfo(onnxPath);
            if (!fileInfo.Exists || fileInfo.Length == 0)
                return false;

            // Real ONNX models are at least several KB. Files under 10KB are
            // either corrupt or test stubs — skip metadata patching for these.
            if (fileInfo.Length < 10_000)
                return true;

            // Metadata is stored at the end of the protobuf. Read the last 4KB
            // which is more than enough to find metadata entries.
            const int tailSize = 4096;
            using var stream = File.OpenRead(onnxPath);
            var readSize = (int)Math.Min(tailSize, fileInfo.Length);
            stream.Seek(-readSize, SeekOrigin.End);

            var buffer = new byte[readSize];
            stream.ReadExactly(buffer, 0, readSize);

            // Search for the ASCII bytes of "sample_rate"
            var needle = System.Text.Encoding.ASCII.GetBytes("sample_rate");
            return ContainsSequence(buffer, needle);
        }
        catch
        {
            return false; // If we can't check, assume it's missing
        }
    }

    private static bool ContainsSequence(byte[] haystack, byte[] needle)
    {
        for (int i = 0; i <= haystack.Length - needle.Length; i++)
        {
            bool found = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    found = false;
                    break;
                }
            }
            if (found) return true;
        }
        return false;
    }

    private readonly object _rescanLock = new();
    private CancellationTokenSource? _rescanCts;

    private void DebounceRescan()
    {
        if (_disposed)
            return;

        lock (_rescanLock)
        {
            if (_disposed)
                return;

            _rescanCts?.Cancel();
            _rescanCts?.Dispose();
            _rescanCts = new CancellationTokenSource();
            var cts = _rescanCts;

            Task.Delay(100, cts.Token)
                .ContinueWith(_ =>
                {
                    if (!cts.Token.IsCancellationRequested && !_disposed)
                    {
                        ScanVoices();
                    }
                }, TaskScheduler.Default);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Disable the watcher before disposing to prevent race conditions
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
        }

        _catalogLock?.Dispose();
        _rescanCts?.Dispose();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().Name);
        }
    }
}
