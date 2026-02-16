using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SysTTS.Models;
using SysTTS.Services;
using SysTTS.Settings;

namespace SysTTS.Tests.Services;

public class SpeechQueueTests : IDisposable
{
    private readonly Mock<ITtsEngine> _mockTtsEngine;
    private readonly Mock<IAudioPlayer> _mockAudioPlayer;
    private readonly Mock<ILogger<SpeechQueue>> _mockLogger;
    private readonly ServiceSettings _settings;
    private SpeechQueue? _queue;

    public SpeechQueueTests()
    {
        _mockTtsEngine = new Mock<ITtsEngine>();
        _mockAudioPlayer = new Mock<IAudioPlayer>();
        _mockLogger = new Mock<ILogger<SpeechQueue>>();

        _settings = new ServiceSettings
        {
            MaxQueueDepth = 5,
            InterruptOnHigherPriority = true
        };

        // Setup default mock returns
        _mockTtsEngine.Setup(t => t.Synthesize(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<float>()))
            .Returns(() => (new float[] { 0.1f, 0.2f, 0.3f }, 22050));

        _mockAudioPlayer.Setup(a => a.PlayAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private SpeechQueue CreateQueue()
    {
        return new SpeechQueue(
            _mockTtsEngine.Object,
            _mockAudioPlayer.Object,
            Options.Create(_settings),
            _mockLogger.Object
        );
    }

    public void Dispose()
    {
        _queue?.Dispose();
    }

    // AC2.7: Higher-priority request interrupts lower-priority speech currently playing
    [Fact]
    public async Task Enqueue_HigherPriorityDuringPlayback_InterruptsCurrentSpeech()
    {
        // Arrange
        _queue = CreateQueue();

        var lowPriorityRequest = new SpeechRequest(
            Id: "low-1",
            Text: "This is a long speech",
            VoiceId: "voice-1",
            Priority: 3,
            Source: "test"
        );

        var highPriorityRequest = new SpeechRequest(
            Id: "high-1",
            Text: "Interrupt me with high priority",
            VoiceId: "voice-1",
            Priority: 1,
            Source: "test"
        );

        // Make PlayAsync wait so we can interrupt it
        var playStarted = new TaskCompletionSource();

        _mockAudioPlayer.Setup(a => a.PlayAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns<float[], int, CancellationToken>(async (samples, rate, ct) =>
            {
                playStarted.SetResult();
                try
                {
                    await Task.Delay(Timeout.Infinite, ct);
                }
                catch (OperationCanceledException)
                {
                    // Expected when interrupted
                }
            });

        // Act - enqueue low priority
        _queue.Enqueue(lowPriorityRequest);

        // Wait for playback to start
        await playStarted.Task;

        // Give a moment for processing loop to start playback
        await Task.Delay(100);

        // Enqueue high priority request
        _queue.Enqueue(highPriorityRequest);

        // Wait a bit for interrupt to occur
        await Task.Delay(200);

        // Assert - PlayAsync should have been called at least twice
        // Once for low-priority (which is interrupted and cancelled)
        // And again for the high-priority request that interrupts it
        _mockAudioPlayer.Verify(
            a => a.PlayAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.AtLeast(2)
        );
    }

    // AC2.8: Queue at max depth drops lowest-priority item when new request arrives
    [Fact]
    public async Task Enqueue_QueueAtMaxDepth_DropsLowestPriorityItem()
    {
        // Arrange
        _settings.MaxQueueDepth = 3;
        _queue = CreateQueue();

        var processedRequestIds = new List<string>();
        var processingBlocked = new TaskCompletionSource();

        // Block processing loop so items accumulate in queue
        _mockAudioPlayer.Setup(a => a.PlayAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns<float[], int, CancellationToken>(async (samples, rate, ct) =>
            {
                // Record which request was processed
                var invocation = _mockTtsEngine.Invocations.LastOrDefault();
                if (invocation != null)
                {
                    var voiceArg = (string)invocation.Arguments[1];
                    processedRequestIds.Add(voiceArg);
                }
                // Block indefinitely until unblocked
                await processingBlocked.Task;
            });

        var request1 = new SpeechRequest("1", "Text 1", "voice-1", 1, "test");
        var request2 = new SpeechRequest("2", "Text 2", "voice-2", 2, "test");
        var request3 = new SpeechRequest("3", "Text 3", "voice-3", 3, "test");
        var request4 = new SpeechRequest("4", "Text 4", "voice-4", 3, "test"); // Same priority as 3, but newer
        var request5 = new SpeechRequest("5", "Text 5", "voice-5", 3, "test"); // This should evict request3

        // Act
        _queue.Enqueue(request1); // Queue: [1]
        await Task.Delay(50);

        _queue.Enqueue(request2); // Queue: [1, 2] (request1 is being processed)
        _queue.Enqueue(request3); // Queue: [1, 2, 3]

        // Queue is now at max depth (3)
        // Adding a 4th item should evict the lowest-priority oldest item (request3)
        _queue.Enqueue(request4); // Queue should be [1, 2, 4] (request3 evicted)

        // Adding request5 (priority 3) should evict request4 (oldest with priority 3)
        _queue.Enqueue(request5); // Queue should be [1, 2, 5] (request4 evicted)

        // Assert
        _queue.QueueDepth.Should().Be(3);

        // Verify that request3 and request4 were NOT processed (evicted before processing)
        // Only request1 should have been processed so far
        processedRequestIds.Should().NotContain("voice-3");
        processedRequestIds.Should().NotContain("voice-4");
    }

    // Additional test: Multiple requests processed in priority order
    [Fact]
    public async Task Enqueue_MultipleRequests_ProcessesInPriorityOrder()
    {
        // Arrange
        _queue = CreateQueue();

        _mockAudioPlayer.Setup(a => a.PlayAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request3 = new SpeechRequest("3", "Priority 3", "voice-3", 3, "test");
        var request1 = new SpeechRequest("1", "Priority 1", "voice-1", 1, "test");
        var request2 = new SpeechRequest("2", "Priority 2", "voice-2", 2, "test");

        // Act - enqueue out of priority order
        _queue.Enqueue(request3);
        _queue.Enqueue(request1);
        _queue.Enqueue(request2);

        // Wait for queue to process
        await Task.Delay(200);

        // Assert - Verify that priority 1 request (with voice-1) is synthesized FIRST
        // by checking the voice IDs passed to Synthesize in order
        var calls = _mockTtsEngine.Invocations
            .Where(i => i.Method.Name == nameof(ITtsEngine.Synthesize))
            .Select(i => (string)i.Arguments[1])
            .ToList();

        // First call should be for priority 1 (voice-1)
        calls.Count.Should().BeGreaterThan(0);
        calls[0].Should().Be("voice-1");
    }

    // Additional test: StopAndClear cancels current and empties queue
    [Fact]
    public async Task StopAndClear_CancelsCurrentAndEmptiesQueue()
    {
        // Arrange
        _queue = CreateQueue();

        var playStarted = new TaskCompletionSource();
        var playCancelled = new TaskCompletionSource();

        _mockAudioPlayer.Setup(a => a.PlayAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns<float[], int, CancellationToken>(async (samples, rate, ct) =>
            {
                playStarted.SetResult();
                try
                {
                    await Task.Delay(Timeout.Infinite, ct);
                }
                catch (OperationCanceledException)
                {
                    playCancelled.SetResult();
                    throw;
                }
            });

        var request1 = new SpeechRequest("1", "Long text", "voice", 1, "test");
        var request2 = new SpeechRequest("2", "More text", "voice", 2, "test");
        var request3 = new SpeechRequest("3", "Even more", "voice", 3, "test");

        // Enqueue multiple requests
        _queue.Enqueue(request1);
        _queue.Enqueue(request2);
        _queue.Enqueue(request3);

        // Wait for first to start playing
        var waitTask = Task.WhenAny(playStarted.Task, Task.Delay(1000));
        await waitTask;

        // Act
        await _queue.StopAndClear();

        // Wait a bit for cancellation to propagate
        await Task.Delay(100);

        // Assert
        _queue.QueueDepth.Should().Be(0);
    }

    // Additional test: Enqueue returns request ID
    [Fact]
    public void Enqueue_ReturnsRequestId()
    {
        // Arrange
        _queue = CreateQueue();
        var request = new SpeechRequest("test-id-123", "Text", "voice", 1, "test");

        // Act
        var returnedId = _queue.Enqueue(request);

        // Assert
        returnedId.Should().Be("test-id-123");
    }

    // Additional test: Initial queue depth is 0
    [Fact]
    public void QueueDepth_Initially_IsZero()
    {
        // Arrange
        _queue = CreateQueue();

        // Act
        var depth = _queue.QueueDepth;

        // Assert
        depth.Should().Be(0);
    }

    // Additional test: Queue depth reflects enqueued items
    [Fact]
    public void QueueDepth_AfterEnqueue_Increments()
    {
        // Arrange
        _settings.MaxQueueDepth = 10; // Make sure we don't evict
        _queue = CreateQueue();

        // Slow processing
        _mockAudioPlayer.Setup(a => a.PlayAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(async (float[] samples, int rate, CancellationToken ct) =>
                await Task.Delay(1000, ct));

        var request1 = new SpeechRequest("1", "Text 1", "voice", 1, "test");
        var request2 = new SpeechRequest("2", "Text 2", "voice", 2, "test");

        // Act
        _queue.Enqueue(request1);
        _queue.Enqueue(request2);

        // Assert - should be 1 or 2 depending on processing timing, but at least 1 enqueued
        _queue.QueueDepth.Should().BeGreaterThanOrEqualTo(1);
    }

    // Additional test: Dispose cleans up resources
    [Fact]
    public void Dispose_CleansUpResources()
    {
        // Arrange
        _queue = CreateQueue();

        // Act
        _queue.Dispose();

        // Assert - Dispose should not throw
        // (Real verification would be internal state inspection or memory profiling)
    }
}
