namespace SysTTS.Settings;

/// <summary>
/// Strongly-typed configuration for a single hotkey binding.
/// </summary>
public class HotkeySettings
{
    /// <summary>
    /// The key name or code (e.g., "F23", "0x86", "134").
    /// Parsed by VirtualKeyParser to determine the virtual key code.
    /// </summary>
    public string Key { get; set; } = "";

    /// <summary>
    /// The hotkey mode: "direct" or "picker".
    /// - direct: Immediately capture selected text and speak with configured voice.
    /// - picker: Show voice picker popup, let user select voice, then speak with selected voice.
    /// </summary>
    public string Mode { get; set; } = "direct";

    /// <summary>
    /// The voice identifier (e.g., "en_US-amy-medium").
    /// Used only in direct mode. If null, defaults to service DefaultVoice.
    /// </summary>
    public string? Voice { get; set; }
}
