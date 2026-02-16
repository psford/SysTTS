using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using SysTTS.Models;
using SysTTS.Services;

namespace SysTTS.Tests.Services;

public class SpeechServiceTests
{
    private readonly Mock<ISpeechQueue> _mockQueue;
    private readonly Mock<IVoiceManager> _mockVoiceManager;
    private readonly Mock<ILogger<SpeechService>> _mockLogger;
    private readonly SpeechService _service;

    public SpeechServiceTests()
    {
        _mockQueue = new Mock<ISpeechQueue>();
        _mockVoiceManager = new Mock<IVoiceManager>();
        _mockLogger = new Mock<ILogger<SpeechService>>();

        // Set up default mock returns
        _mockQueue.Setup(q => q.Enqueue(It.IsAny<SpeechRequest>()))
            .Returns<SpeechRequest>(r => r.Id);

        _mockVoiceManager.Setup(vm => vm.ResolveVoiceId(It.IsAny<string>()))
            .Returns<string?>(voiceId => voiceId ?? "default-voice");

        _service = new SpeechService(
            _mockQueue.Object,
            _mockVoiceManager.Object,
            CreateTestConfiguration(),
            _mockLogger.Object
        );
    }

    // AC2.1: ProcessSpeakRequest with source resolves voice from source config
    [Fact]
    public void ProcessSpeakRequest_WithSource_ResolvesVoiceFromSourceConfig()
    {
        // Arrange
        var text = "Hello world";
        var source = "default";
        var expectedVoice = "en_US-amy-medium";

        _mockVoiceManager.Setup(vm => vm.ResolveVoiceId(expectedVoice))
            .Returns(expectedVoice);

        // Act
        var (queued, id) = _service.ProcessSpeakRequest(text, source, voiceOverride: null);

        // Assert
        queued.Should().BeTrue();
        id.Should().NotBeNullOrEmpty();

        _mockQueue.Verify(q => q.Enqueue(It.Is<SpeechRequest>(
            r => r.Text == text &&
                 r.VoiceId == expectedVoice &&
                 r.Priority == 3 &&
                 r.Source == source
        )), Times.Once);
    }

    // AC2.2: ProcessSpeakRequest with explicit voice overrides source config
    [Fact]
    public void ProcessSpeakRequest_WithVoiceOverride_UsesOverride()
    {
        // Arrange
        var text = "Override test";
        var source = "default";
        var voiceOverride = "en_GB-alan-medium";

        _mockVoiceManager.Setup(vm => vm.ResolveVoiceId(voiceOverride))
            .Returns(voiceOverride);

        // Act
        var (queued, id) = _service.ProcessSpeakRequest(text, source, voiceOverride);

        // Assert
        queued.Should().BeTrue();
        id.Should().NotBeNullOrEmpty();

        _mockQueue.Verify(q => q.Enqueue(It.Is<SpeechRequest>(
            r => r.VoiceId == voiceOverride &&
                 r.Text == text
        )), Times.Once);
    }

    // AC2.3: ProcessSpeakRequest with text not matching filters returns false
    [Fact]
    public void ProcessSpeakRequest_TextNotMatchingFilters_ReturnsFalse()
    {
        // Arrange
        var text = "random stuff";
        var source = "t-tracker";

        // Act
        var (queued, id) = _service.ProcessSpeakRequest(text, source, voiceOverride: null);

        // Assert
        queued.Should().BeFalse();
        id.Should().BeNull();

        // Verify nothing was queued
        _mockQueue.Verify(q => q.Enqueue(It.IsAny<SpeechRequest>()), Times.Never);
    }

    // AC2.3: ProcessSpeakRequest with text matching filter returns true and queues
    [Fact]
    public void ProcessSpeakRequest_TextMatchingFilter_ReturnsTrue()
    {
        // Arrange
        var text = "train approaching station";
        var source = "t-tracker";

        // Act
        var (queued, id) = _service.ProcessSpeakRequest(text, source, voiceOverride: null);

        // Assert
        queued.Should().BeTrue();
        id.Should().NotBeNullOrEmpty();

        _mockQueue.Verify(q => q.Enqueue(It.Is<SpeechRequest>(
            r => r.Text == text
        )), Times.Once);
    }

    // AC2.4: ProcessSpeakRequest with null filters speaks all text
    [Fact]
    public void ProcessSpeakRequest_NullFilters_SpeaksAllText()
    {
        // Arrange
        var text = "anything goes here";
        var source = "default"; // default has null filters

        // Act
        var (queued, id) = _service.ProcessSpeakRequest(text, source, voiceOverride: null);

        // Assert
        queued.Should().BeTrue();
        id.Should().NotBeNullOrEmpty();

        _mockQueue.Verify(q => q.Enqueue(It.Is<SpeechRequest>(
            r => r.Text == text
        )), Times.Once);
    }

    // Additional test: Unknown source falls back to default config
    [Fact]
    public void ProcessSpeakRequest_UnknownSource_FallsBackToDefault()
    {
        // Arrange
        var text = "Hello world";
        var unknownSource = "unknown-source-name";

        // Act
        var (queued, id) = _service.ProcessSpeakRequest(text, unknownSource, voiceOverride: null);

        // Assert - should use default config (null filters, priority 3)
        queued.Should().BeTrue();
        id.Should().NotBeNullOrEmpty();

        _mockQueue.Verify(q => q.Enqueue(It.Is<SpeechRequest>(
            r => r.Priority == 3  // default priority
        )), Times.Once);
    }

    // Additional test: Empty text is rejected
    [Fact]
    public void ProcessSpeakRequest_EmptyText_ReturnsFalse()
    {
        // Arrange
        var emptyText = "";

        // Act
        var (queued, id) = _service.ProcessSpeakRequest(emptyText, "default", voiceOverride: null);

        // Assert
        queued.Should().BeFalse();
        id.Should().BeNull();

        _mockQueue.Verify(q => q.Enqueue(It.IsAny<SpeechRequest>()), Times.Never);
    }

    // Additional test: Case-insensitive filter matching
    [Fact]
    public void ProcessSpeakRequest_FilterMatchIsCaseInsensitive()
    {
        // Arrange
        var text = "TRAIN ARRIVED at station";
        var source = "t-tracker";

        // Act
        var (queued, id) = _service.ProcessSpeakRequest(text, source, voiceOverride: null);

        // Assert - should match "arrived" filter despite uppercase
        queued.Should().BeTrue();
        id.Should().NotBeNullOrEmpty();

        _mockQueue.Verify(q => q.Enqueue(It.IsAny<SpeechRequest>()), Times.Once);
    }

    // Helper: Create test configuration with source settings
    private IConfiguration CreateTestConfiguration()
    {
        var configBuilder = new ConfigurationBuilder();

        var configDict = new Dictionary<string, string?>
        {
            // Default source: no filters, voice amy, priority 3
            { "Sources:default:Voice", "en_US-amy-medium" },
            { "Sources:default:Priority", "3" },

            // t-tracker source: filters for "approaching" and "arrived", voice bob, priority 2
            { "Sources:t-tracker:Voice", "en_US-bob-medium" },
            { "Sources:t-tracker:Filters:0", "approaching" },
            { "Sources:t-tracker:Filters:1", "arrived" },
            { "Sources:t-tracker:Priority", "2" },
        };

        configBuilder.AddInMemoryCollection(configDict);
        return configBuilder.Build();
    }
}
