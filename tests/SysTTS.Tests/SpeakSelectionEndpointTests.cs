using FluentAssertions;
using Moq;
using SysTTS.Handlers;
using SysTTS.Models;
using SysTTS.Services;

namespace SysTTS.Tests;

/// <summary>
/// Tests for POST /api/speak-selection endpoint handler.
/// Verifies AC3.8: /api/speak-selection captures selected text server-side and queues speech.
///
/// These tests exercise the ACTUAL endpoint handler logic via SpeakSelectionHandler.Handle(),
/// not mock behavior. They verify the handler calls dependencies with correct parameters
/// and handles edge cases properly.
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
    /// AC3.8: When CaptureSelectedTextAsync returns null, handler should NOT queue
    /// (verified by verifying speech service is NOT called)
    /// </summary>
    [Fact]
    public async Task Handle_WhenTextIsNull_DoesNotCallSpeechService()
    {
        // Arrange
        _mockClipboard.Setup(c => c.CaptureSelectedTextAsync())
            .ReturnsAsync((string?)null);

        var request = new SpeakSelectionRequestDto(null);

        // Act
        var result = await SpeakSelectionHandler.Handle(request, _mockClipboard.Object, _mockSpeechService.Object);

        // Assert - handler should return IResult (not null)
        result.Should().NotBeNull();

        // Verify clipboard was called
        _mockClipboard.Verify(c => c.CaptureSelectedTextAsync(), Times.Once);

        // Verify speech service was NOT called when text is null (this is the core assertion)
        _mockSpeechService.Verify(s => s.ProcessSpeakRequest(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    /// <summary>
    /// AC3.8: When CaptureSelectedTextAsync returns empty string, handler should NOT queue
    /// </summary>
    [Fact]
    public async Task Handle_WhenTextIsEmpty_DoesNotCallSpeechService()
    {
        // Arrange
        _mockClipboard.Setup(c => c.CaptureSelectedTextAsync())
            .ReturnsAsync("");

        var request = new SpeakSelectionRequestDto(null);

        // Act
        var result = await SpeakSelectionHandler.Handle(request, _mockClipboard.Object, _mockSpeechService.Object);

        // Assert - handler should return IResult (not null)
        result.Should().NotBeNull();

        // Verify clipboard was called
        _mockClipboard.Verify(c => c.CaptureSelectedTextAsync(), Times.Once);

        // Verify speech service was NOT called when text is empty
        _mockSpeechService.Verify(s => s.ProcessSpeakRequest(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    /// <summary>
    /// AC3.8: When text is captured, handler calls speech service to queue
    /// </summary>
    [Fact]
    public async Task Handle_WhenTextIsCaptured_CallsSpeechService()
    {
        // Arrange
        var capturedText = "Hello world";
        var expectedId = "speech-123";
        var voice = "en_US-amy-medium";

        _mockClipboard.Setup(c => c.CaptureSelectedTextAsync())
            .ReturnsAsync(capturedText);

        _mockSpeechService.Setup(s => s.ProcessSpeakRequest(capturedText, "speak-selection", voice))
            .Returns((true, expectedId));

        var request = new SpeakSelectionRequestDto(voice);

        // Act
        var result = await SpeakSelectionHandler.Handle(request, _mockClipboard.Object, _mockSpeechService.Object);

        // Assert - handler should return IResult (not null)
        result.Should().NotBeNull();

        // Verify correct parameters were passed to dependencies
        _mockClipboard.Verify(c => c.CaptureSelectedTextAsync(), Times.Once);
        _mockSpeechService.Verify(s => s.ProcessSpeakRequest(capturedText, "speak-selection", voice), Times.Once);
    }

    /// <summary>
    /// AC3.8: Voice override is passed through correctly to ProcessSpeakRequest
    /// </summary>
    [Fact]
    public async Task Handle_WithVoiceOverride_PassesThroughToSpeechService()
    {
        // Arrange
        var capturedText = "Test text";
        var expectedId = "speech-456";
        var voice = "en_GB-alan-medium";

        _mockClipboard.Setup(c => c.CaptureSelectedTextAsync())
            .ReturnsAsync(capturedText);

        _mockSpeechService.Setup(s => s.ProcessSpeakRequest(capturedText, "speak-selection", voice))
            .Returns((true, expectedId));

        var request = new SpeakSelectionRequestDto(voice);

        // Act
        var result = await SpeakSelectionHandler.Handle(request, _mockClipboard.Object, _mockSpeechService.Object);

        // Assert - handler should return IResult
        result.Should().NotBeNull();

        // Verify correct voice was passed to speech service
        _mockSpeechService.Verify(
            s => s.ProcessSpeakRequest(capturedText, "speak-selection", voice),
            Times.Once);
    }

    /// <summary>
    /// AC3.8: Text captured without voice override passes null voice to ProcessSpeakRequest
    /// </summary>
    [Fact]
    public async Task Handle_WithoutVoiceOverride_PassesNullVoiceToSpeechService()
    {
        // Arrange
        var capturedText = "Another test";
        var expectedId = "speech-789";

        _mockClipboard.Setup(c => c.CaptureSelectedTextAsync())
            .ReturnsAsync(capturedText);

        _mockSpeechService.Setup(s => s.ProcessSpeakRequest(capturedText, "speak-selection", null))
            .Returns((true, expectedId));

        var request = new SpeakSelectionRequestDto(null);

        // Act
        var result = await SpeakSelectionHandler.Handle(request, _mockClipboard.Object, _mockSpeechService.Object);

        // Assert - handler should return IResult
        result.Should().NotBeNull();

        // Verify null voice was passed to speech service
        _mockSpeechService.Verify(
            s => s.ProcessSpeakRequest(capturedText, "speak-selection", null),
            Times.Once);
    }

    /// <summary>
    /// AC3.8: Source is always "speak-selection" in the handler call (hardcoded, not from request)
    /// </summary>
    [Fact]
    public async Task Handle_AlwaysUsesSourceIdentifier_Hardcoded()
    {
        // Arrange
        var capturedText = "Source test";
        var expectedId = "speech-999";

        _mockClipboard.Setup(c => c.CaptureSelectedTextAsync())
            .ReturnsAsync(capturedText);

        _mockSpeechService.Setup(s => s.ProcessSpeakRequest(It.IsAny<string>(), "speak-selection", null))
            .Returns((true, expectedId));

        var request = new SpeakSelectionRequestDto(null);

        // Act
        var result = await SpeakSelectionHandler.Handle(request, _mockClipboard.Object, _mockSpeechService.Object);

        // Assert - handler should return IResult
        result.Should().NotBeNull();

        // Verify EXACT source value is always "speak-selection"
        _mockSpeechService.Verify(
            s => s.ProcessSpeakRequest(capturedText, "speak-selection", It.IsAny<string>()),
            Times.Once);

        // Verify it wasn't called with any other source
        _mockSpeechService.Verify(
            s => s.ProcessSpeakRequest(It.IsAny<string>(), It.IsNotIn("speak-selection"), It.IsAny<string>()),
            Times.Never);
    }
}
