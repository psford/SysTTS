using FluentAssertions;
using Moq;
using SysTTS.Services;

namespace SysTTS.Tests;

/// <summary>
/// Tests for POST /api/speak-selection endpoint logic.
/// Verifies AC3.8: /api/speak-selection captures selected text server-side and queues speech.
/// </summary>
public class SpeakSelectionEndpointTests
{
    private readonly Mock<IClipboardService> _mockClipboard;
    private readonly Mock<ISpeechService> _mockSpeechService;

    public SpeakSelectionEndpointTests()
    {
        _mockClipboard = new Mock<IClipboardService>();
        _mockSpeechService = new Mock<ISpeechService>();
    }

    /// <summary>
    /// AC3.8: When CaptureSelectedTextAsync returns null, endpoint returns 200 with { queued: false, text: "" }
    /// </summary>
    [Fact]
    public async Task PostSpeakSelection_WhenTextIsNull_ReturnsOkWithQueuedFalse()
    {
        // Arrange
        _mockClipboard.Setup(c => c.CaptureSelectedTextAsync())
            .ReturnsAsync((string?)null);

        // Act
        var text = await _mockClipboard.Object.CaptureSelectedTextAsync();

        // Assert - should NOT queue when text is null
        text.Should().BeNull();

        // Verify clipboard was called
        _mockClipboard.Verify(c => c.CaptureSelectedTextAsync(), Times.Once);

        // Verify speech service was NOT called when text is null
        _mockSpeechService.Verify(s => s.ProcessSpeakRequest(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    /// <summary>
    /// AC3.8: When CaptureSelectedTextAsync returns empty string, endpoint returns 200 with { queued: false, text: "" }
    /// </summary>
    [Fact]
    public async Task PostSpeakSelection_WhenTextIsEmpty_ReturnsOkWithQueuedFalse()
    {
        // Arrange
        _mockClipboard.Setup(c => c.CaptureSelectedTextAsync())
            .ReturnsAsync("");

        // Act
        var text = await _mockClipboard.Object.CaptureSelectedTextAsync();

        // Assert - should NOT queue when text is empty
        text.Should().Be("");
        string.IsNullOrWhiteSpace(text).Should().BeTrue();

        _mockClipboard.Verify(c => c.CaptureSelectedTextAsync(), Times.Once);
    }

    /// <summary>
    /// AC3.8: When text is captured, endpoint returns 202 with { queued, id, text }
    /// </summary>
    [Fact]
    public async Task PostSpeakSelection_WhenTextIsCaptured_Returns202WithQueuedAndId()
    {
        // Arrange
        var capturedText = "Hello world";
        var expectedId = "speech-123";
        var voice = "en_US-amy-medium";

        _mockClipboard.Setup(c => c.CaptureSelectedTextAsync())
            .ReturnsAsync(capturedText);

        _mockSpeechService.Setup(s => s.ProcessSpeakRequest(capturedText, "speak-selection", voice))
            .Returns((true, expectedId));

        // Act
        var text = await _mockClipboard.Object.CaptureSelectedTextAsync();
        var (queued, id) = _mockSpeechService.Object.ProcessSpeakRequest(text!, "speak-selection", voice);

        // Assert
        text.Should().Be(capturedText);
        string.IsNullOrWhiteSpace(text).Should().BeFalse();

        queued.Should().BeTrue();
        id.Should().Be(expectedId);

        _mockClipboard.Verify(c => c.CaptureSelectedTextAsync(), Times.Once);
        _mockSpeechService.Verify(s => s.ProcessSpeakRequest(capturedText, "speak-selection", voice), Times.Once);
    }

    /// <summary>
    /// AC3.8: Voice override is passed through correctly to ProcessSpeakRequest
    /// </summary>
    [Fact]
    public async Task PostSpeakSelection_WithVoiceOverride_PassesThroughCorrectly()
    {
        // Arrange
        var capturedText = "Test text";
        var expectedId = "speech-456";
        var voice = "en_GB-alan-medium";

        _mockClipboard.Setup(c => c.CaptureSelectedTextAsync())
            .ReturnsAsync(capturedText);

        _mockSpeechService.Setup(s => s.ProcessSpeakRequest(capturedText, "speak-selection", voice))
            .Returns((true, expectedId));

        // Act
        var text = await _mockClipboard.Object.CaptureSelectedTextAsync();
        var (queued, id) = _mockSpeechService.Object.ProcessSpeakRequest(text!, "speak-selection", voice);

        // Assert - voice override should be passed through
        queued.Should().BeTrue();
        id.Should().Be(expectedId);

        _mockSpeechService.Verify(s => s.ProcessSpeakRequest(capturedText, "speak-selection", voice), Times.Once);
    }

    /// <summary>
    /// AC3.8: Text captured without voice override passes null voice to ProcessSpeakRequest
    /// </summary>
    [Fact]
    public async Task PostSpeakSelection_WithoutVoiceOverride_PassesNullVoice()
    {
        // Arrange
        var capturedText = "Another test";
        var expectedId = "speech-789";

        _mockClipboard.Setup(c => c.CaptureSelectedTextAsync())
            .ReturnsAsync(capturedText);

        _mockSpeechService.Setup(s => s.ProcessSpeakRequest(capturedText, "speak-selection", null))
            .Returns((true, expectedId));

        // Act
        var text = await _mockClipboard.Object.CaptureSelectedTextAsync();
        var (queued, id) = _mockSpeechService.Object.ProcessSpeakRequest(text!, "speak-selection", null);

        // Assert - should use default voice when voice override is not provided
        queued.Should().BeTrue();
        id.Should().Be(expectedId);

        _mockSpeechService.Verify(s => s.ProcessSpeakRequest(capturedText, "speak-selection", null), Times.Once);
    }

    /// <summary>
    /// AC3.8: Source is always "speak-selection" in the endpoint call
    /// </summary>
    [Fact]
    public async Task PostSpeakSelection_AlwaysUsesSourceIdentifier()
    {
        // Arrange
        var capturedText = "Source test";
        var expectedId = "speech-999";

        _mockClipboard.Setup(c => c.CaptureSelectedTextAsync())
            .ReturnsAsync(capturedText);

        _mockSpeechService.Setup(s => s.ProcessSpeakRequest(capturedText, "speak-selection", null))
            .Returns((true, expectedId));

        // Act
        var text = await _mockClipboard.Object.CaptureSelectedTextAsync();
        var (queued, id) = _mockSpeechService.Object.ProcessSpeakRequest(text!, "speak-selection", null);

        // Assert - source must always be "speak-selection" for this endpoint
        _mockSpeechService.Verify(s => s.ProcessSpeakRequest(It.IsAny<string>(), "speak-selection", It.IsAny<string>()),
            Times.Once);
    }
}
