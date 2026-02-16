using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using SysTTS.Models;
using SysTTS.Services;
using SysTTS.Settings;

namespace SysTTS.Tests.Services;

public class HotkeyServiceTests : IDisposable
{
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<IClipboardService> _mockClipboardService;
    private readonly Mock<ISpeechService> _mockSpeechService;
    private readonly Mock<ILogger<HotkeyService>> _mockLogger;
    private readonly Mock<IVoiceManager> _mockVoiceManager;
    private readonly UserPreferences _userPreferences;
    private readonly string _tempPreferencesFile;

    public HotkeyServiceTests()
    {
        _mockConfiguration = new Mock<IConfiguration>();
        _mockClipboardService = new Mock<IClipboardService>();
        _mockSpeechService = new Mock<ISpeechService>();
        _mockLogger = new Mock<ILogger<HotkeyService>>();
        _mockVoiceManager = new Mock<IVoiceManager>();

        // Use in-memory preferences for testing
        _tempPreferencesFile = Path.Combine(Path.GetTempPath(), $"prefs-{Guid.NewGuid()}.json");
        _userPreferences = new UserPreferences(_tempPreferencesFile);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_tempPreferencesFile))
            {
                File.Delete(_tempPreferencesFile);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    /// <summary>
    /// Tests that HotkeyService accepts IVoiceManager, UserPreferences, and SynchronizationContext in constructor.
    /// </summary>
    [Fact]
    public void Constructor_AcceptsVoiceManagerUserPreferencesAndSyncContext()
    {
        // Arrange - Use DefaultSynchronizationContext for testing
        var syncContext = new System.Threading.SynchronizationContext();

        // Act
        var service = new HotkeyService(
            _mockConfiguration.Object,
            _mockClipboardService.Object,
            _mockSpeechService.Object,
            _mockLogger.Object,
            _mockVoiceManager.Object,
            _userPreferences,
            syncContext);

        // Assert - Service should be constructible with new parameters
        service.Should().NotBeNull();
    }

    /// <summary>
    /// Tests that HotkeyService can be created with mocked voice manager and sync context.
    /// </summary>
    [Fact]
    public void Constructor_WithMockedVoiceManager_Succeeds()
    {
        // Arrange
        _mockConfiguration.Setup(c => c.GetSection(It.IsAny<string>())).Returns(new Mock<IConfigurationSection>().Object);
        var syncContext = new System.Threading.SynchronizationContext();

        // Act
        var service = new HotkeyService(
            _mockConfiguration.Object,
            _mockClipboardService.Object,
            _mockSpeechService.Object,
            _mockLogger.Object,
            _mockVoiceManager.Object,
            _userPreferences,
            syncContext);

        // Assert
        service.Should().NotBeNull();
    }

    /// <summary>
    /// Picker mode behavioral tests require an STA message pump for WinForms form display.
    /// Unit tests cannot directly test the ShowDialog() flow due to lack of message pump.
    /// Integration/operational tests (Task 5 in phase plan) verify the actual picker behavior.
    /// These constructor and parameter validation tests ensure the dependencies are correctly set up.
    /// </summary>
}
