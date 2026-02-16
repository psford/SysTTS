using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SysTTS.Models;
using SysTTS.Settings;
using System.Text.RegularExpressions;

namespace SysTTS.Services;

/// <summary>
/// Orchestrates between HTTP requests and the speech queue.
///
/// Key behaviors:
/// - Reads source configuration from IConfiguration by looking up Sources:{sourceName}
/// - Falls back to Sources:default if source not found
/// - Applies regex filters: if source Filters is non-null, text must match at least one pattern
/// - If source Filters is null, all text passes through
/// - Resolves voice from voiceOverride or source config, with fallback via IVoiceManager.ResolveVoiceId
/// - Constructs SpeechRequest and enqueues via ISpeechQueue
/// - Returns (true, requestId) if queued, (false, null) if filtered out
/// </summary>
public class SpeechService : ISpeechService
{
    private readonly ISpeechQueue _queue;
    private readonly IVoiceManager _voiceManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SpeechService> _logger;

    public SpeechService(
        ISpeechQueue queue,
        IVoiceManager voiceManager,
        IConfiguration configuration,
        ILogger<SpeechService> logger)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _voiceManager = voiceManager ?? throw new ArgumentNullException(nameof(voiceManager));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public (bool Queued, string? Id) ProcessSpeakRequest(string text, string? source, string? voiceOverride)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning("ProcessSpeakRequest called with empty text");
            return (false, null);
        }

        // Get source configuration, falling back to 'default'
        var sourceConfig = GetSourceSettings(source ?? "default");

        if (sourceConfig == null)
        {
            _logger.LogWarning("Source configuration not found for source: {Source}", source ?? "default");
            return (false, null);
        }

        // Apply regex filters if configured
        if (!PassesFilters(text, sourceConfig.Filters))
        {
            _logger.LogInformation("Text filtered out for source {Source}: '{Text}'", source ?? "default", text);
            return (false, null);
        }

        // Resolve voice (override takes precedence over source config)
        string resolvedVoice = ResolveVoice(voiceOverride, sourceConfig.Voice);

        // Create speech request
        var request = new SpeechRequest(
            Id: Guid.NewGuid().ToString(),
            Text: text,
            VoiceId: resolvedVoice,
            Priority: sourceConfig.Priority,
            Source: source
        );

        // Enqueue and return
        var requestId = _queue.Enqueue(request);
        _logger.LogInformation("Queued speech request {RequestId} for source {Source} with voice {VoiceId}",
            requestId, source ?? "default", resolvedVoice);

        return (true, requestId);
    }

    private SourceSettings? GetSourceSettings(string sourceName)
    {
        // Try to bind the source configuration
        var sourceConfig = new SourceSettings();
        var section = _configuration.GetSection($"Sources:{sourceName}");

        if (!section.Exists())
        {
            _logger.LogDebug("Source configuration not found for '{Source}', trying 'default'", sourceName);

            // Fall back to default
            section = _configuration.GetSection("Sources:default");
            if (!section.Exists())
            {
                _logger.LogWarning("Neither source '{Source}' nor 'default' configuration found", sourceName);
                return null;
            }
        }

        section.Bind(sourceConfig);
        return sourceConfig;
    }

    private bool PassesFilters(string text, string[]? filters)
    {
        // If filters is null, all text passes through
        if (filters == null || filters.Length == 0)
        {
            return true;
        }

        // Text must match at least one regex pattern (case-insensitive)
        foreach (var pattern in filters)
        {
            try
            {
                if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)))
                {
                    _logger.LogDebug("Text matched filter pattern: {Pattern}", pattern);
                    return true;
                }
            }
            catch (RegexParseException ex)
            {
                _logger.LogWarning(ex, "Invalid regex filter pattern: {Pattern}", pattern);
            }
        }

        return false;
    }

    private string ResolveVoice(string? voiceOverride, string? sourceVoice)
    {
        // Voice override takes precedence
        if (!string.IsNullOrWhiteSpace(voiceOverride))
        {
            var resolved = _voiceManager.ResolveVoiceId(voiceOverride);
            _logger.LogDebug("Using voice override: {Override} -> {Resolved}", voiceOverride, resolved);
            return resolved;
        }

        // Fall back to source voice
        var sourceFallback = _voiceManager.ResolveVoiceId(sourceVoice);
        _logger.LogDebug("Using source voice: {Source} -> {Resolved}", sourceVoice, sourceFallback);
        return sourceFallback;
    }
}
