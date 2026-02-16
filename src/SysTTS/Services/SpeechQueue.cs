using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SysTTS.Models;
using SysTTS.Settings;

namespace SysTTS.Services;

/// <summary>
/// Manages a priority queue of speech requests and processes them serially.
///
/// Key behaviors:
/// - Processes requests one at a time (serial) using a background task
/// - Uses PriorityQueue<SpeechRequest, int> for ordering (lower number = higher priority)
/// - Supports interrupt-on-higher-priority when enabled in ServiceSettings
/// - Enforces max queue depth by evicting lowest-priority (highest number) oldest items
/// - Provides StopAndClear to cancel current playback and drain the queue
/// - Implements IDisposable to clean up background task on shutdown
/// </summary>
public class SpeechQueue : ISpeechQueue, IDisposable
{
    private readonly ITtsEngine _ttsEngine;
    private readonly IAudioPlayer _audioPlayer;
    private readonly ServiceSettings _settings;
    private readonly ILogger<SpeechQueue> _logger;

    // Priority queue stores requests with priority as the sort key
    private readonly PriorityQueue<SpeechRequest, int> _queue = new();

    // Lock for thread-safe queue access
    private readonly object _queueLock = new object();

    // Current playback cancellation token for interrupt capability
    private CancellationTokenSource? _currentPlaybackCts;

    // Current request priority for interrupt-on-higher-priority logic
    private int _currentRequestPriority = int.MaxValue; // No request playing initially

    // Signal to start processing next item
    private readonly SemaphoreSlim _processingSignal = new SemaphoreSlim(0);

    // Cancellation for the processing loop
    private readonly CancellationTokenSource _processingLoopCts = new CancellationTokenSource();

    // Background processing task
    private readonly Task _processingTask;

    public SpeechQueue(ITtsEngine ttsEngine, IAudioPlayer audioPlayer, IOptions<ServiceSettings> options, ILogger<SpeechQueue> logger)
    {
        _ttsEngine = ttsEngine ?? throw new ArgumentNullException(nameof(ttsEngine));
        _audioPlayer = audioPlayer ?? throw new ArgumentNullException(nameof(audioPlayer));
        _settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Start background processing loop
        _processingTask = ProcessingLoopAsync(_processingLoopCts.Token);
    }

    public string Enqueue(SpeechRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        lock (_queueLock)
        {
            // Check if queue is at max depth
            if (_queue.Count >= _settings.MaxQueueDepth)
            {
                _logger.LogWarning("Queue at max depth ({MaxDepth}), evicting lowest-priority oldest item", _settings.MaxQueueDepth);
                EvictLowestPriorityOldestItem();
            }

            // Enqueue with priority as sort key
            _queue.Enqueue(request, request.Priority);
            _logger.LogDebug("Enqueued speech request {RequestId} with priority {Priority}", request.Id, request.Priority);

            // Check if we should interrupt current playback
            if (_settings.InterruptOnHigherPriority && request.Priority < _currentRequestPriority)
            {
                _logger.LogInformation("Higher-priority request ({Priority}) interrupting current playback ({CurrentPriority})",
                    request.Priority, _currentRequestPriority);
                _currentPlaybackCts?.Cancel();
            }
        }

        // Signal processing loop to process next item
        _processingSignal.Release();

        return request.Id;
    }

    public Task StopAndClear()
    {
        lock (_queueLock)
        {
            // Cancel current playback
            _currentPlaybackCts?.Cancel();

            // Clear the queue
            while (_queue.Count > 0)
            {
                _queue.Dequeue();
            }

            _currentRequestPriority = int.MaxValue;
            _logger.LogInformation("Stopped playback and cleared queue");
        }

        return Task.CompletedTask;
    }

    public int QueueDepth
    {
        get
        {
            lock (_queueLock)
            {
                return _queue.Count;
            }
        }
    }

    private async Task ProcessingLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Wait for signal that there's something to process
                try
                {
                    await _processingSignal.WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                SpeechRequest? request = null;
                lock (_queueLock)
                {
                    if (_queue.Count > 0)
                    {
                        request = _queue.Dequeue();
                    }
                }

                if (request != null)
                {
                    await ProcessRequestAsync(request, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when shutting down
            _logger.LogDebug("Processing loop cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in processing loop");
        }
    }

    private async Task ProcessRequestAsync(SpeechRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Update current request priority
            lock (_queueLock)
            {
                _currentRequestPriority = request.Priority;
                _currentPlaybackCts = new CancellationTokenSource();
            }

            _logger.LogInformation("Processing speech request {RequestId}: '{Text}' with voice {VoiceId}",
                request.Id, request.Text, request.VoiceId);

            // Synthesize speech
            var (samples, sampleRate) = _ttsEngine.Synthesize(request.Text, request.VoiceId);

            // Check if the current request has been cancelled (e.g., by StopAndClear)
            // before attempting to play the synthesized audio
            lock (_queueLock)
            {
                if (_currentPlaybackCts?.IsCancellationRequested ?? false)
                {
                    _logger.LogInformation("Speech request {RequestId} was cancelled after synthesis, skipping playback", request.Id);
                    throw new OperationCanceledException();
                }
            }

            // Play audio with linked cancellation token (current playback + shutdown)
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _currentPlaybackCts?.Token ?? CancellationToken.None);

            await _audioPlayer.PlayAsync(samples, sampleRate, linkedCts.Token);

            _logger.LogDebug("Completed speech request {RequestId}", request.Id);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Speech request {RequestId} was cancelled", request.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing speech request {RequestId}", request.Id);
        }
        finally
        {
            lock (_queueLock)
            {
                _currentPlaybackCts?.Dispose();
                _currentPlaybackCts = null;
                _currentRequestPriority = int.MaxValue;
            }
        }
    }

    private void EvictLowestPriorityOldestItem()
    {
        // PriorityQueue doesn't support removing arbitrary items,
        // so we need to rebuild it without the lowest priority oldest item.
        //
        // Complexity: O(n log n) due to dequeuing all items (O(n log n)) and re-enqueueing
        // the remaining items (O(n log n)). This is acceptable because:
        // - Eviction only occurs when queue reaches MaxQueueDepth (typically small, e.g., 5-10 items)
        // - Eviction happens infrequently (only on high concurrent request load)
        // - The alternative (tracking item insertion order externally) would add complexity elsewhere

        // Find the lowest priority (highest number)
        var items = new List<(SpeechRequest request, int priority)>();
        while (_queue.Count > 0)
        {
            var request = _queue.Dequeue();
            items.Add((request, request.Priority));
        }

        if (items.Count == 0)
            return;

        // Find lowest priority (highest number) oldest item
        int lowestPriority = items.Max(x => x.priority);
        var lowestPriorityItems = items.Where(x => x.priority == lowestPriority).ToList();
        var itemToRemove = lowestPriorityItems.First(); // First one is oldest

        _logger.LogDebug("Evicting request {RequestId} with priority {Priority}", itemToRemove.request.Id, itemToRemove.priority);

        // Rebuild queue without the evicted item
        foreach (var (request, priority) in items)
        {
            if (request.Id != itemToRemove.request.Id)
            {
                _queue.Enqueue(request, priority);
            }
        }
    }

    public void Dispose()
    {
        try
        {
            // Stop the processing loop
            if (!_processingLoopCts.IsCancellationRequested)
            {
                _processingLoopCts.Cancel();
            }
            _processingLoopCts.Dispose();

            // Cancel any current playback
            lock (_queueLock)
            {
                if (_currentPlaybackCts != null && !_currentPlaybackCts.IsCancellationRequested)
                {
                    _currentPlaybackCts.Cancel();
                }
                _currentPlaybackCts?.Dispose();
                _currentPlaybackCts = null;
            }

            // Wait for processing task to complete
            try
            {
                _processingTask.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
            {
                // Expected
            }

            _processingSignal.Dispose();
            _logger.LogDebug("SpeechQueue disposed");
        }
        catch (ObjectDisposedException)
        {
            // Already disposed, that's fine
        }
    }
}
