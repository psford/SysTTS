namespace SysTTS.Services;

/// <summary>
/// Service for capturing selected text from any application by simulating Ctrl+C.
/// Preserves the original clipboard contents before and after capture.
/// </summary>
public interface IClipboardService
{
    /// <summary>
    /// Captures selected text from the currently focused application by simulating Ctrl+C.
    /// </summary>
    /// <returns>
    /// The selected text if any text was selected, or null if nothing was selected (empty clipboard after Ctrl+C).
    /// Original clipboard contents are restored after capture.
    /// </returns>
    Task<string?> CaptureSelectedTextAsync();
}
