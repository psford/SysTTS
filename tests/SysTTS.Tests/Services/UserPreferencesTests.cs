using FluentAssertions;
using SysTTS.Services;
using System.IO;

namespace SysTTS.Tests.Services;

public class UserPreferencesTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _preferencesFilePath;

    public UserPreferencesTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"SysTTS.Tests.{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
        _preferencesFilePath = Path.Combine(_tempDir, "user-preferences.json");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public void Constructor_LoadsDefaultPreferences_WhenFileDoesNotExist()
    {
        // Arrange & Act
        var prefs = new UserPreferences(_preferencesFilePath);

        // Assert
        prefs.LastUsedPickerVoice.Should().BeNull();
    }

    [Fact]
    public void Save_CreatesJsonFile_WithLastUsedVoice()
    {
        // Arrange
        var prefs = new UserPreferences(_preferencesFilePath);
        prefs.LastUsedPickerVoice = "voice-123";

        // Act
        prefs.Save();

        // Assert
        File.Exists(_preferencesFilePath).Should().BeTrue();
        var content = File.ReadAllText(_preferencesFilePath);
        content.Should().Contain("voice-123");
    }

    [Fact]
    public void Load_RestoresLastUsedVoice_FromPersistedFile()
    {
        // Arrange
        var prefs1 = new UserPreferences(_preferencesFilePath);
        prefs1.LastUsedPickerVoice = "voice-456";
        prefs1.Save();

        // Act
        var prefs2 = new UserPreferences(_preferencesFilePath);

        // Assert
        prefs2.LastUsedPickerVoice.Should().Be("voice-456");
    }

    [Fact]
    public void Save_IsThreadSafe_WhenCalledConcurrently()
    {
        // Arrange
        var prefs = new UserPreferences(_preferencesFilePath);
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < 10; i++)
        {
            int voiceIndex = i;
            tasks.Add(Task.Run(() =>
            {
                prefs.LastUsedPickerVoice = $"voice-{voiceIndex}";
                prefs.Save();
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert
        // File should exist and be valid JSON
        File.Exists(_preferencesFilePath).Should().BeTrue();

        // Load should succeed
        var prefs2 = new UserPreferences(_preferencesFilePath);
        prefs2.LastUsedPickerVoice.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Load_SetsSingleProperty_AfterPersistence()
    {
        // Arrange
        var prefs1 = new UserPreferences(_preferencesFilePath);
        prefs1.LastUsedPickerVoice = "voice-789";
        prefs1.Save();

        // Act - Load into a new instance
        var prefs2 = new UserPreferences(_preferencesFilePath);

        // Assert - LastUsedPickerVoice should be loaded from file
        prefs2.LastUsedPickerVoice.Should().Be("voice-789");
    }
}
