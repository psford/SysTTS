using Microsoft.Extensions.Logging;
using SysTTS.Interop;
using System.Windows.Forms;

namespace SysTTS.Services;

/// <summary>
/// Captures selected text from any application by simulating Ctrl+C.
/// Preserves the original clipboard contents before and after capture.
///
/// All clipboard operations run on a dedicated STA thread (required by WinForms
/// Clipboard class). After simulating Ctrl+C, the thread pumps Windows messages
/// via Application.DoEvents() to handle OLE delayed rendering — needed for
/// Electron apps (VS Code, Slack) and browsers (Firefox, Chrome) which don't
/// write clipboard data synchronously.
/// </summary>
public class ClipboardService : IClipboardService
{
    private readonly ILogger<ClipboardService> _logger;

    public ClipboardService(ILogger<ClipboardService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string?> CaptureSelectedTextAsync()
    {
        _logger.LogDebug("CaptureSelectedTextAsync called");

        // Run the capture operation on a dedicated STA thread with message pumping
        var tcs = new TaskCompletionSource<string?>();

        var staThread = new Thread(() =>
        {
            try
            {
                var capturedText = CaptureSelectedTextOnSTA();
                tcs.SetResult(capturedText);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error capturing selected text on STA thread");
                tcs.SetException(ex);
            }
        })
        {
            Name = "ClipboardCapture-STA",
            IsBackground = true
        };

        staThread.TrySetApartmentState(ApartmentState.STA);
        staThread.Start();

        return await tcs.Task;
    }

    private string? CaptureSelectedTextOnSTA()
    {
        try
        {
            // Step 1: Save current clipboard text (null if empty)
            string? originalClipboard = null;
            try
            {
                originalClipboard = Clipboard.GetText();
                _logger.LogDebug("Saved original clipboard text (length: {Length})", originalClipboard?.Length ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read clipboard for backup");
                originalClipboard = null;
            }

            // Step 2: Clear clipboard
            try
            {
                Clipboard.Clear();
                _logger.LogDebug("Cleared clipboard");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clear clipboard");
            }

            // Step 3: Simulate Ctrl+C (Ctrl down, C down, C up, Ctrl up)
            // SendInput is a global input injection — works from any thread.
            var inputs = new NativeMethods.INPUT[4];

            // Ctrl down
            inputs[0].type = NativeMethods.INPUT_KEYBOARD;
            inputs[0].ki.wVk = (ushort)NativeMethods.VK_CONTROL;
            inputs[0].ki.dwFlags = 0;

            // C down
            inputs[1].type = NativeMethods.INPUT_KEYBOARD;
            inputs[1].ki.wVk = (ushort)NativeMethods.VK_C;
            inputs[1].ki.dwFlags = 0;

            // C up
            inputs[2].type = NativeMethods.INPUT_KEYBOARD;
            inputs[2].ki.wVk = (ushort)NativeMethods.VK_C;
            inputs[2].ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;

            // Ctrl up
            inputs[3].type = NativeMethods.INPUT_KEYBOARD;
            inputs[3].ki.wVk = (ushort)NativeMethods.VK_CONTROL;
            inputs[3].ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;

            uint sent = NativeMethods.SendInput((uint)inputs.Length, inputs, System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.INPUT>());
            if (sent != inputs.Length)
            {
                _logger.LogWarning("SendInput failed: sent {Sent} of {Expected} input events", sent, inputs.Length);
            }
            else
            {
                _logger.LogDebug("Sent {Count} input events for Ctrl+C simulation", sent);
            }

            // Step 4: Wait for target app to process Ctrl+C, pumping Windows messages.
            // Electron apps (VS Code, Slack) and browsers (Firefox) use OLE delayed
            // rendering — clipboard data isn't available until the reader pumps COM/OLE
            // messages. Application.DoEvents() processes pending messages on this STA thread.
            var deadline = DateTime.UtcNow.AddMilliseconds(300);
            while (DateTime.UtcNow < deadline)
            {
                Application.DoEvents();
                Thread.Sleep(25);
            }
            _logger.LogDebug("Waited ~300ms with message pumping for clipboard update");

            // Step 5: Read clipboard text
            string? capturedText = null;
            try
            {
                capturedText = Clipboard.GetText();
                _logger.LogDebug("Read captured text from clipboard (length: {Length})", capturedText?.Length ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read captured text from clipboard");
                capturedText = null;
            }

            // Step 6: Restore original clipboard contents
            try
            {
                if (originalClipboard != null)
                {
                    Clipboard.SetText(originalClipboard);
                    _logger.LogDebug("Restored original clipboard text");
                }
                else
                {
                    Clipboard.Clear();
                    _logger.LogDebug("Cleared clipboard (original was empty)");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to restore clipboard contents");
            }

            // Step 7: Return captured text (null if nothing was selected)
            if (string.IsNullOrEmpty(capturedText))
            {
                _logger.LogDebug("No text was selected (AC3.7)");
                return null;
            }

            _logger.LogInformation("Captured selected text (length: {Length})", capturedText.Length);
            return capturedText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in CaptureSelectedTextOnSTA");
            throw;
        }
    }
}
