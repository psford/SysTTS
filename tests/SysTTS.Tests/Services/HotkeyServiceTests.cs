using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using SysTTS.Forms;
using SysTTS.Models;
using SysTTS.Services;
using SysTTS.Settings;
using System.Windows.Forms;

namespace SysTTS.Tests.Services;

public class HotkeyServiceTests
{
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<IClipboardService> _mockClipboardService;
    private readonly Mock<ISpeechService> _mockSpeechService;
    private readonly Mock<ILogger<HotkeyService>> _mockLogger;
    private readonly Mock<IVoiceManager> _mockVoiceManager;
    private readonly UserPreferences _userPreferences;

    public HotkeyServiceTests()
    {
        _mockConfiguration = new Mock<IConfiguration>();
        _mockClipboardService = new Mock<IClipboardService>();
        _mockSpeechService = new Mock<ISpeechService>();
        _mockLogger = new Mock<ILogger<HotkeyService>>();
        _mockVoiceManager = new Mock<IVoiceManager>();

        // Use in-memory preferences for testing
        var tempFile = Path.Combine(Path.GetTempPath(), $"prefs-{Guid.NewGuid()}.json");
        _userPreferences = new UserPreferences(tempFile);
    }

    /// <summary>
    /// Tests that HotkeyService accepts IVoiceManager and UserPreferences in constructor.
    /// </summary>
    [Fact]
    public void Constructor_AcceptsVoiceManagerAndUserPreferences()
    {
        // Act
        var service = new HotkeyService(
            _mockConfiguration.Object,
            _mockClipboardService.Object,
            _mockSpeechService.Object,
            _mockLogger.Object,
            _mockVoiceManager.Object,
            _userPreferences);

        // Assert - Service should be constructible with new parameters
        service.Should().NotBeNull();
    }

    /// <summary>
    /// Tests that HotkeyService can be created with mocked voice manager.
    /// </summary>
    [Fact]
    public void Constructor_WithMockedVoiceManager_Succeeds()
    {
        // Arrange
        _mockConfiguration.Setup(c => c.GetSection(It.IsAny<string>())).Returns(new Mock<IConfigurationSection>().Object);

        // Act
        var service = new HotkeyService(
            _mockConfiguration.Object,
            _mockClipboardService.Object,
            _mockSpeechService.Object,
            _mockLogger.Object,
            _mockVoiceManager.Object,
            _userPreferences);

        // Assert
        service.Should().NotBeNull();
    }
}
