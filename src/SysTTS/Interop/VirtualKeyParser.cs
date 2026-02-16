using System.Globalization;

namespace SysTTS.Interop;

/// <summary>
/// Parses key name strings to virtual key codes.
/// Supports F1-F24, modifier keys, letter keys, and hex/decimal numeric input.
/// </summary>
public static class VirtualKeyParser
{
    /// <summary>
    /// Dictionary of known key names to virtual key codes.
    /// </summary>
    private static readonly Dictionary<string, int> KeyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Function Keys (F1-F24)
        { "F1", NativeMethods.VK_F1 },
        { "F2", NativeMethods.VK_F2 },
        { "F3", NativeMethods.VK_F3 },
        { "F4", NativeMethods.VK_F4 },
        { "F5", NativeMethods.VK_F5 },
        { "F6", NativeMethods.VK_F6 },
        { "F7", NativeMethods.VK_F7 },
        { "F8", NativeMethods.VK_F8 },
        { "F9", NativeMethods.VK_F9 },
        { "F10", NativeMethods.VK_F10 },
        { "F11", NativeMethods.VK_F11 },
        { "F12", NativeMethods.VK_F12 },
        { "F13", NativeMethods.VK_F13 },
        { "F14", NativeMethods.VK_F14 },
        { "F15", NativeMethods.VK_F15 },
        { "F16", NativeMethods.VK_F16 },
        { "F17", NativeMethods.VK_F17 },
        { "F18", NativeMethods.VK_F18 },
        { "F19", NativeMethods.VK_F19 },
        { "F20", NativeMethods.VK_F20 },
        { "F21", NativeMethods.VK_F21 },
        { "F22", NativeMethods.VK_F22 },
        { "F23", NativeMethods.VK_F23 },
        { "F24", NativeMethods.VK_F24 },

        // Modifier Keys
        { "Ctrl", NativeMethods.VK_CONTROL },
        { "Control", NativeMethods.VK_CONTROL },
        { "Alt", NativeMethods.VK_ALT },
        { "Shift", NativeMethods.VK_SHIFT },

        // Letter Keys
        { "A", 0x41 },
        { "B", 0x42 },
        { "C", NativeMethods.VK_C },
        { "D", 0x44 },
        { "E", 0x45 },
        { "F", 0x46 },
        { "G", 0x47 },
        { "H", 0x48 },
        { "I", 0x49 },
        { "J", 0x4A },
        { "K", 0x4B },
        { "L", 0x4C },
        { "M", 0x4D },
        { "N", 0x4E },
        { "O", 0x4F },
        { "P", 0x50 },
        { "Q", 0x51 },
        { "R", 0x52 },
        { "S", 0x53 },
        { "T", 0x54 },
        { "U", 0x55 },
        { "V", 0x56 },
        { "W", 0x57 },
        { "X", 0x58 },
        { "Y", 0x59 },
        { "Z", 0x5A },
    };

    /// <summary>
    /// Parses a key name or numeric string to its virtual key code.
    /// </summary>
    /// <param name="keyString">
    /// Key name (e.g., "F23", "Ctrl"), hex string (e.g., "0x86"), or decimal string (e.g., "134").
    /// Case-insensitive for named keys.
    /// </param>
    /// <returns>
    /// Virtual key code (int) if recognized, or null if the key name is not found.
    /// </returns>
    public static int? ParseKeyCode(string keyString)
    {
        if (string.IsNullOrWhiteSpace(keyString))
        {
            return null;
        }

        // Try named key lookup first (case-insensitive)
        if (KeyMap.TryGetValue(keyString, out var vkCode))
        {
            return vkCode;
        }

        // Try hex string (e.g., "0x86")
        if (keyString.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(keyString.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hexValue))
            {
                return hexValue;
            }
        }

        // Try decimal string (e.g., "134")
        if (int.TryParse(keyString, NumberStyles.Integer, CultureInfo.InvariantCulture, out var decimalValue))
        {
            return decimalValue;
        }

        // Not recognized
        return null;
    }
}
