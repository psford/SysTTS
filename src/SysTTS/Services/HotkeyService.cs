using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SysTTS.Interop;
using SysTTS.Settings;

namespace SysTTS.Services;

/// <summary>
/// Installs a Win32 low-level keyboard hook and dispatches registered hotkey actions.
///
/// Key behaviors:
/// - Start() reads Hotkeys config, installs WH_KEYBOARD_LL hook
/// - Hook callback dispatches to Task.Run for processing (returns within 1000ms)
/// - Direct mode: capture text via ClipboardService, send to SpeechService
/// - Picker mode: log "not yet implemented" (Phase 5)
/// - Always CallNextHookEx to pass events to other hooks
/// - Stores delegate reference to prevent GC
/// - Implements IDisposable for cleanup
/// </summary>
public class HotkeyService : IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly IClipboardService _clipboardService;
    private readonly ISpeechService _speechService;
    private readonly ILogger<HotkeyService> _logger;

    private nint _hookHandle = nint.Zero;
    private NativeMethods.LowLevelKeyboardProc? _keyboardProc;
    private Dictionary<int, HotkeySettings> _hotkeyMap = new();

    public HotkeyService(
        IConfiguration configuration,
        IClipboardService clipboardService,
        ISpeechService speechService,
        ILogger<HotkeyService> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
        _speechService = speechService ?? throw new ArgumentNullException(nameof(speechService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Starts the hotkey service by reading config and installing the keyboard hook.
    /// </summary>
    public void Start()
    {
        try
        {
            // Read Hotkeys array from configuration
            var hotkeySettingsList = new List<HotkeySettings>();
            var hotkeySection = _configuration.GetSection("Hotkeys");

            if (hotkeySection.Exists())
            {
                hotkeySection.Bind(hotkeySettingsList);
            }

            if (hotkeySettingsList.Count == 0)
            {
                _logger.LogWarning("No hotkeys configured in appsettings.json");
                return;
            }

            _logger.LogInformation("Loading {Count} hotkey(s)", hotkeySettingsList.Count);

            // Parse each hotkey and build lookup dictionary
            foreach (var hotkeySettings in hotkeySettingsList)
            {
                var vkCode = VirtualKeyParser.ParseKeyCode(hotkeySettings.Key);

                if (vkCode == null)
                {
                    _logger.LogWarning("Failed to parse hotkey key: {Key}", hotkeySettings.Key);
                    continue;
                }

                _hotkeyMap[vkCode.Value] = hotkeySettings;
                _logger.LogDebug("Registered hotkey: VK=0x{VkCode:X2} Mode={Mode} Voice={Voice}",
                    vkCode.Value, hotkeySettings.Mode, hotkeySettings.Voice ?? "default");
            }

            if (_hotkeyMap.Count == 0)
            {
                _logger.LogWarning("No valid hotkeys were registered");
                return;
            }

            // Install the low-level keyboard hook
            _keyboardProc = LowLevelKeyboardProc;
            _hookHandle = NativeMethods.SetWindowsHookEx(
                NativeMethods.WH_KEYBOARD_LL,
                _keyboardProc,
                NativeMethods.GetModuleHandle(null),
                0);

            if (_hookHandle == nint.Zero)
            {
                _logger.LogError("Failed to install keyboard hook");
                _keyboardProc = null;
                return;
            }

            _logger.LogInformation("Keyboard hook installed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting HotkeyService");
        }
    }

    /// <summary>
    /// Stops the hotkey service by uninstalling the keyboard hook.
    /// </summary>
    public void Stop()
    {
        try
        {
            if (_hookHandle != nint.Zero)
            {
                if (NativeMethods.UnhookWindowsHookEx(_hookHandle))
                {
                    _logger.LogInformation("Keyboard hook uninstalled successfully");
                }
                else
                {
                    _logger.LogWarning("Failed to uninstall keyboard hook");
                }

                _hookHandle = nint.Zero;
                _keyboardProc = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping HotkeyService");
        }
    }

    /// <summary>
    /// Low-level keyboard hook callback. Must return within 1000ms.
    /// </summary>
    private nint LowLevelKeyboardProc(int nCode, nint wParam, nint lParam)
    {
        try
        {
            // Only process key-down events
            if (nCode >= 0 && (wParam == NativeMethods.WM_KEYDOWN || wParam == NativeMethods.WM_SYSKEYDOWN))
            {
                // Marshal lParam to KBDLLHOOKSTRUCT
                var hookData = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);

                // Check if this key is registered as a hotkey
                if (_hotkeyMap.TryGetValue((int)hookData.vkCode, out var hotkey))
                {
                    _logger.LogDebug("Hotkey detected: VK=0x{VkCode:X2} Mode={Mode}", hookData.vkCode, hotkey.Mode);

                    // Offload processing to Task.Run to stay within 1000ms callback timeout
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await ProcessHotkeyAsync(hotkey);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing hotkey");
                        }
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in LowLevelKeyboardProc");
        }

        // Always pass the event to the next hook in the chain
        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    /// <summary>
    /// Processes a hotkey action based on its configured mode.
    /// </summary>
    private async Task ProcessHotkeyAsync(HotkeySettings hotkey)
    {
        if (string.IsNullOrWhiteSpace(hotkey.Mode) || hotkey.Mode.Equals("direct", StringComparison.OrdinalIgnoreCase))
        {
            // Direct mode: capture selected text and speak immediately
            await ProcessDirectModeAsync(hotkey);
        }
        else if (hotkey.Mode.Equals("picker", StringComparison.OrdinalIgnoreCase))
        {
            // Picker mode: not yet implemented (Phase 5)
            _logger.LogInformation("Picker mode not yet implemented");
        }
        else
        {
            _logger.LogWarning("Unknown hotkey mode: {Mode}", hotkey.Mode);
        }
    }

    /// <summary>
    /// Processes direct-mode hotkey: captures selected text and sends to SpeechService.
    /// </summary>
    private async Task ProcessDirectModeAsync(HotkeySettings hotkey)
    {
        // Capture selected text via ClipboardService (preserves original clipboard)
        var selectedText = await _clipboardService.CaptureSelectedTextAsync();

        // If no text was selected, do nothing (AC3.7)
        if (string.IsNullOrWhiteSpace(selectedText))
        {
            _logger.LogDebug("No text selected, skipping speech");
            return;
        }

        // Send text to SpeechService with hotkey-configured voice (AC3.1)
        var (queued, requestId) = _speechService.ProcessSpeakRequest(
            selectedText,
            "hotkey",
            hotkey.Voice);

        if (queued)
        {
            _logger.LogInformation("Hotkey speech request queued: {RequestId} with voice {Voice}",
                requestId, hotkey.Voice ?? "default");
        }
        else
        {
            _logger.LogWarning("Failed to queue hotkey speech request");
        }
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
