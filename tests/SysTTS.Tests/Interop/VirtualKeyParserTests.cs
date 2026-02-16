using FluentAssertions;
using SysTTS.Interop;

namespace SysTTS.Tests.Interop;

public class VirtualKeyParserTests
{
    // Test F22/F23/F24 parsing
    [Fact]
    public void ParseKeyCode_F22_Returns0x85()
    {
        // Arrange
        var keyString = "F22";

        // Act
        var result = VirtualKeyParser.ParseKeyCode(keyString);

        // Assert
        result.Should().Be(0x85);
        result.Should().Be(NativeMethods.VK_F22);
    }

    [Fact]
    public void ParseKeyCode_F23_Returns0x86()
    {
        // Arrange
        var keyString = "F23";

        // Act
        var result = VirtualKeyParser.ParseKeyCode(keyString);

        // Assert
        result.Should().Be(0x86);
        result.Should().Be(NativeMethods.VK_F23);
    }

    [Fact]
    public void ParseKeyCode_F24_Returns0x87()
    {
        // Arrange
        var keyString = "F24";

        // Act
        var result = VirtualKeyParser.ParseKeyCode(keyString);

        // Assert
        result.Should().Be(0x87);
        result.Should().Be(NativeMethods.VK_F24);
    }

    // Test hex string parsing
    [Fact]
    public void ParseKeyCode_HexString_ParsesCorrectly()
    {
        // Arrange
        var keyString = "0x86";

        // Act
        var result = VirtualKeyParser.ParseKeyCode(keyString);

        // Assert
        result.Should().Be(0x86);
        result.Should().Be(134);
    }

    // Test decimal string parsing
    [Fact]
    public void ParseKeyCode_DecimalString_ParsesCorrectly()
    {
        // Arrange
        var keyString = "134";

        // Act
        var result = VirtualKeyParser.ParseKeyCode(keyString);

        // Assert
        result.Should().Be(134);
        result.Should().Be(0x86);
    }

    // Test unknown key returns null
    [Fact]
    public void ParseKeyCode_UnknownKey_ReturnsNull()
    {
        // Arrange
        var keyString = "UnknownKey";

        // Act
        var result = VirtualKeyParser.ParseKeyCode(keyString);

        // Assert
        result.Should().BeNull();
    }

    // Test case-insensitive matching
    [Fact]
    public void ParseKeyCode_CaseInsensitive_Works()
    {
        // Arrange
        var keyStringLower = "f23";
        var keyStringMixed = "F23";
        var keyStringUpper = "F23";

        // Act
        var resultLower = VirtualKeyParser.ParseKeyCode(keyStringLower);
        var resultMixed = VirtualKeyParser.ParseKeyCode(keyStringMixed);
        var resultUpper = VirtualKeyParser.ParseKeyCode(keyStringUpper);

        // Assert
        resultLower.Should().Be(0x86);
        resultMixed.Should().Be(0x86);
        resultUpper.Should().Be(0x86);
        resultLower.Should().Be(resultMixed).And.Be(resultUpper);
    }

    // Test modifier keys
    [Fact]
    public void ParseKeyCode_ModifierKeys_Work()
    {
        // Arrange & Act
        var ctrlResult = VirtualKeyParser.ParseKeyCode("Ctrl");
        var altResult = VirtualKeyParser.ParseKeyCode("Alt");
        var shiftResult = VirtualKeyParser.ParseKeyCode("Shift");

        // Assert
        ctrlResult.Should().Be(0x11);
        ctrlResult.Should().Be(NativeMethods.VK_CONTROL);

        altResult.Should().Be(0x12);
        altResult.Should().Be(NativeMethods.VK_ALT);

        shiftResult.Should().Be(0x10);
        shiftResult.Should().Be(NativeMethods.VK_SHIFT);
    }

    // Additional test: Alt "Control" alias
    [Fact]
    public void ParseKeyCode_ControlAlias_Works()
    {
        // Arrange
        var keyString = "Control";

        // Act
        var result = VirtualKeyParser.ParseKeyCode(keyString);

        // Assert
        result.Should().Be(0x11);
        result.Should().Be(NativeMethods.VK_CONTROL);
    }

    // Additional test: Letter keys
    [Fact]
    public void ParseKeyCode_LetterKeys_Work()
    {
        // Arrange & Act
        var cResult = VirtualKeyParser.ParseKeyCode("C");
        var aResult = VirtualKeyParser.ParseKeyCode("A");
        var zResult = VirtualKeyParser.ParseKeyCode("Z");

        // Assert
        cResult.Should().Be(0x43);
        cResult.Should().Be(NativeMethods.VK_C);

        aResult.Should().Be(0x41);
        zResult.Should().Be(0x5A);
    }

    // Additional test: Hex string case-insensitive
    [Fact]
    public void ParseKeyCode_HexStringCaseInsensitive_Works()
    {
        // Arrange
        var hexLower = "0x86";
        var hexUpper = "0X86";

        // Act
        var resultLower = VirtualKeyParser.ParseKeyCode(hexLower);
        var resultUpper = VirtualKeyParser.ParseKeyCode(hexUpper);

        // Assert
        resultLower.Should().Be(0x86);
        resultUpper.Should().Be(0x86);
        resultLower.Should().Be(resultUpper);
    }

    // Additional test: Empty and whitespace strings
    [Fact]
    public void ParseKeyCode_EmptyOrWhitespace_ReturnsNull()
    {
        // Arrange & Act
        var emptyResult = VirtualKeyParser.ParseKeyCode("");
        var whitespaceResult = VirtualKeyParser.ParseKeyCode("   ");
        var nullResult = VirtualKeyParser.ParseKeyCode(null!);

        // Assert
        emptyResult.Should().BeNull();
        whitespaceResult.Should().BeNull();
        nullResult.Should().BeNull();
    }

    // Additional test: F1-F12 also work
    [Fact]
    public void ParseKeyCode_FunctionKeysF1toF12_Work()
    {
        // Arrange & Act
        var f1Result = VirtualKeyParser.ParseKeyCode("F1");
        var f12Result = VirtualKeyParser.ParseKeyCode("F12");

        // Assert
        f1Result.Should().Be(0x70);
        f1Result.Should().Be(NativeMethods.VK_F1);

        f12Result.Should().Be(0x7B);
        f12Result.Should().Be(NativeMethods.VK_F12);
    }
}
