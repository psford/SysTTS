using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SysTTS.Models;
using SysTTS.Services;
using SysTTS.Settings;

namespace SysTTS.Tests.Services;

public class VoiceManagerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Mock<ILogger<VoiceManager>> _mockLogger;
    private readonly ServiceSettings _serviceSettings;

    public VoiceManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sys-tts-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);

        _mockLogger = new Mock<ILogger<VoiceManager>>();
        _serviceSettings = new ServiceSettings { VoicesPath = _tempDir, DefaultVoice = "default-voice" };
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    // AC4.1: GetAvailableVoices returns list of .onnx + .onnx.json pairs
    [Fact]
    public void GetAvailableVoices_WithValidPairs_ReturnsList()
    {
        // Arrange
        CreateVoiceFiles("en_US-amy-medium", sampleRate: 22050);
        CreateVoiceFiles("en_US-bob-medium", sampleRate: 24000);

        var options = Options.Create(_serviceSettings);
        using var manager = new VoiceManager(options, _mockLogger.Object);

        // Act
        var voices = manager.GetAvailableVoices();

        // Assert
        voices.Should().HaveCount(2);
        voices.Should().Contain(v => v.Id == "en_US-amy-medium" && v.SampleRate == 22050);
        voices.Should().Contain(v => v.Id == "en_US-bob-medium" && v.SampleRate == 24000);
    }

    // AC4.2: FileSystemWatcher - new model dropped while running becomes available
    [Fact]
    public void FileSystemWatcher_NewModelDropped_BecomesAvailable()
    {
        // Arrange
        CreateVoiceFiles("en_US-initial", sampleRate: 22050);
        var options = Options.Create(_serviceSettings);
        using var manager = new VoiceManager(options, _mockLogger.Object);

        var initialVoices = manager.GetAvailableVoices();
        initialVoices.Should().HaveCount(1);

        // Act - drop a new model
        CreateVoiceFiles("en_US-new-model", sampleRate: 22050);

        // Wait for FileSystemWatcher debounce (100ms) + a bit extra
        System.Threading.Thread.Sleep(200);

        // Assert
        var updatedVoices = manager.GetAvailableVoices();
        updatedVoices.Should().HaveCount(2);
        updatedVoices.Should().Contain(v => v.Id == "en_US-new-model");
    }

    // AC4.4: ResolveVoiceId falls back to DefaultVoice when requested voice missing
    [Fact]
    public void ResolveVoiceId_MissingVoice_FallsBackToDefault()
    {
        // Arrange
        CreateVoiceFiles("en_US-amy-medium", sampleRate: 22050);
        var options = Options.Create(_serviceSettings);
        using var manager = new VoiceManager(options, _mockLogger.Object);

        // Act
        var resolved = manager.ResolveVoiceId("missing-voice-id");

        // Assert
        resolved.Should().Be("default-voice");
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Requested voice 'missing-voice-id' not found")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
    }

    // AC4.5: Empty directory returns empty list
    [Fact]
    public void GetAvailableVoices_EmptyDirectory_ReturnsEmptyList()
    {
        // Arrange
        var options = Options.Create(_serviceSettings);
        using var manager = new VoiceManager(options, _mockLogger.Object);

        // Act
        var voices = manager.GetAvailableVoices();

        // Assert
        voices.Should().BeEmpty();
    }

    // Additional test: Orphan .onnx without matching .json is ignored
    [Fact]
    public void GetAvailableVoices_OnnxWithoutJson_IgnoresModel()
    {
        // Arrange
        CreateVoiceFiles("voice1", sampleRate: 22050);

        // Create orphan ONNX without JSON
        var orphanOnnx = Path.Combine(_tempDir, "orphan-voice.onnx");
        File.WriteAllText(orphanOnnx, "fake onnx data");

        var options = Options.Create(_serviceSettings);
        using var manager = new VoiceManager(options, _mockLogger.Object);

        // Act
        var voices = manager.GetAvailableVoices();

        // Assert
        voices.Should().HaveCount(1);
        voices.Should().NotContain(v => v.Id == "orphan-voice");
    }

    // Additional test: Existing voice ID is returned as-is
    [Fact]
    public void ResolveVoiceId_ExistingVoice_ReturnsRequestedId()
    {
        // Arrange
        CreateVoiceFiles("en_US-amy-medium", sampleRate: 22050);
        var options = Options.Create(_serviceSettings);
        using var manager = new VoiceManager(options, _mockLogger.Object);

        // Act
        var resolved = manager.ResolveVoiceId("en_US-amy-medium");

        // Assert
        resolved.Should().Be("en_US-amy-medium");
    }

    // Additional test: Null voice ID returns DefaultVoice
    [Fact]
    public void ResolveVoiceId_NullInput_ReturnsDefault()
    {
        // Arrange
        CreateVoiceFiles("en_US-amy-medium", sampleRate: 22050);
        var options = Options.Create(_serviceSettings);
        using var manager = new VoiceManager(options, _mockLogger.Object);

        // Act
        var resolved = manager.ResolveVoiceId(null);

        // Assert
        resolved.Should().Be("default-voice");
    }

    // Additional test: GetVoice returns VoiceInfo for existing ID
    [Fact]
    public void GetVoice_ExistingId_ReturnsVoiceInfo()
    {
        // Arrange
        CreateVoiceFiles("en_US-amy-medium", sampleRate: 22050);
        var options = Options.Create(_serviceSettings);
        using var manager = new VoiceManager(options, _mockLogger.Object);

        // Act
        var voice = manager.GetVoice("en_US-amy-medium");

        // Assert
        voice.Should().NotBeNull();
        voice!.Id.Should().Be("en_US-amy-medium");
        voice.SampleRate.Should().Be(22050);
    }

    // Additional test: GetVoice returns null for missing ID
    [Fact]
    public void GetVoice_MissingId_ReturnsNull()
    {
        // Arrange
        CreateVoiceFiles("en_US-amy-medium", sampleRate: 22050);
        var options = Options.Create(_serviceSettings);
        using var manager = new VoiceManager(options, _mockLogger.Object);

        // Act
        var voice = manager.GetVoice("nonexistent-voice");

        // Assert
        voice.Should().BeNull();
    }

    // Helper: Create fake voice files (.onnx + .onnx.json)
    private void CreateVoiceFiles(string voiceId, int sampleRate)
    {
        var onnxPath = Path.Combine(_tempDir, $"{voiceId}.onnx");
        var jsonPath = Path.Combine(_tempDir, $"{voiceId}.onnx.json");

        // Create fake ONNX file
        File.WriteAllText(onnxPath, "fake onnx model data");

        // Create fake JSON with sample rate
        var jsonContent = $@"{{
  ""audio"": {{
    ""sample_rate"": {sampleRate}
  }},
  ""inference"": {{
    ""length_scale"": 1.0,
    ""noise_scale"": 0.667,
    ""noise_w"": 0.8
  }}
}}";
        File.WriteAllText(jsonPath, jsonContent);
    }
}
