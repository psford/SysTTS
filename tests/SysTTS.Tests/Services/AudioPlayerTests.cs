using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SysTTS.Services;

namespace SysTTS.Tests.Services;

public class AudioPlayerTests
{
    private readonly Mock<ILogger<AudioPlayer>> _mockLogger;

    public AudioPlayerTests()
    {
        _mockLogger = new Mock<ILogger<AudioPlayer>>();
    }

    // ConvertFloat32ToInt16Pcm pure math tests (no audio hardware needed)

    [Fact]
    public void ConvertFloat32ToInt16Pcm_PositiveOne_ProducesMaxInt16()
    {
        // Arrange
        float[] samples = [1.0f];

        // Act
        byte[] result = AudioPlayer.ConvertFloat32ToInt16Pcm(samples);

        // Assert
        result.Should().HaveCount(2); // 2 bytes per sample
        short value = BitConverter.ToInt16(result, 0);
        value.Should().Be(short.MaxValue); // 32767
    }

    [Fact]
    public void ConvertFloat32ToInt16Pcm_NegativeOne_ProducesMinInt16()
    {
        // Arrange
        float[] samples = [-1.0f];

        // Act
        byte[] result = AudioPlayer.ConvertFloat32ToInt16Pcm(samples);

        // Assert
        result.Should().HaveCount(2);
        short value = BitConverter.ToInt16(result, 0);
        value.Should().Be(short.MinValue); // -32768
    }

    [Fact]
    public void ConvertFloat32ToInt16Pcm_Zero_ProducesZero()
    {
        // Arrange
        float[] samples = [0.0f];

        // Act
        byte[] result = AudioPlayer.ConvertFloat32ToInt16Pcm(samples);

        // Assert
        result.Should().HaveCount(2);
        short value = BitConverter.ToInt16(result, 0);
        value.Should().Be(0);
    }

    [Fact]
    public void ConvertFloat32ToInt16Pcm_ClampAboveOne_ProducesMaxInt16()
    {
        // Arrange
        float[] samples = [1.5f]; // Out of range, should clamp to 1.0f

        // Act
        byte[] result = AudioPlayer.ConvertFloat32ToInt16Pcm(samples);

        // Assert
        result.Should().HaveCount(2);
        short value = BitConverter.ToInt16(result, 0);
        value.Should().Be(short.MaxValue); // Should clamp to max
    }

    [Fact]
    public void ConvertFloat32ToInt16Pcm_ClampBelowNegativeOne_ProducesMinInt16()
    {
        // Arrange
        float[] samples = [-1.5f]; // Out of range, should clamp to -1.0f

        // Act
        byte[] result = AudioPlayer.ConvertFloat32ToInt16Pcm(samples);

        // Assert
        result.Should().HaveCount(2);
        short value = BitConverter.ToInt16(result, 0);
        value.Should().Be(short.MinValue); // Should clamp to min
    }

    [Fact]
    public void ConvertFloat32ToInt16Pcm_MultipleSamples_ProducesCorrectByteArray()
    {
        // Arrange
        float[] samples = [1.0f, 0.0f, -1.0f, 0.5f];

        // Act
        byte[] result = AudioPlayer.ConvertFloat32ToInt16Pcm(samples);

        // Assert
        result.Should().HaveCount(8); // 4 samples × 2 bytes each

        // Verify each sample
        short value1 = BitConverter.ToInt16(result, 0);
        short value2 = BitConverter.ToInt16(result, 2);
        short value3 = BitConverter.ToInt16(result, 4);
        short value4 = BitConverter.ToInt16(result, 6);

        value1.Should().Be(short.MaxValue); // 1.0f
        value2.Should().Be(0); // 0.0f
        value3.Should().Be(short.MinValue); // -1.0f
        // For 0.5f, the result should be approximately half of MaxValue
        value4.Should().BeGreaterThan(0).And.BeLessThan(short.MaxValue);
    }

    [Fact]
    public void ConvertFloat32ToInt16Pcm_NullInput_ThrowsArgumentNullException()
    {
        // Act
        var action = () => AudioPlayer.ConvertFloat32ToInt16Pcm(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ConvertFloat32ToInt16Pcm_EmptyArray_ProducesEmptyByteArray()
    {
        // Arrange
        float[] samples = [];

        // Act
        byte[] result = AudioPlayer.ConvertFloat32ToInt16Pcm(samples);

        // Assert
        result.Should().HaveCount(0);
    }

    [Fact]
    public void ConvertFloat32ToInt16Pcm_HalfValue_ProducesHalfInt16()
    {
        // Arrange
        float[] samples = [0.5f];

        // Act
        byte[] result = AudioPlayer.ConvertFloat32ToInt16Pcm(samples);

        // Assert
        result.Should().HaveCount(2);
        short value = BitConverter.ToInt16(result, 0);
        // 0.5 * 32767 ≈ 16383 (or 16384 with rounding)
        value.Should().BeGreaterThan(16000).And.BeLessThan(17000);
    }

    [Fact]
    public void ConvertFloat32ToInt16Pcm_NegativeHalfValue_ProducesNegativeHalfInt16()
    {
        // Arrange
        float[] samples = [-0.5f];

        // Act
        byte[] result = AudioPlayer.ConvertFloat32ToInt16Pcm(samples);

        // Assert
        result.Should().HaveCount(2);
        short value = BitConverter.ToInt16(result, 0);
        // -0.5 * 32768 = -16384
        value.Should().BeLessThan(0).And.BeGreaterThan(-17000);
    }
}
