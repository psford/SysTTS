using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SysTTS.Forms;
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
/// - Picker mode: show voice picker, let user select voice, then speak with selected voice
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
    private readonly IVoiceManager _voiceManager;
    private readonly UserPreferences _userPreferences;
    private readonly SynchronizationContext _syncContext;

    private nint _hookHandle = nint.Zero;
    private NativeMethods.LowLevelKeyboardProc? _keyboardProc;
    private Dictionary<int, HotkeySettings> _hotkeyMap = new();

    public HotkeyService(
        IConfiguration configuration,
        IClipboardService clipboardService,
        ISpeechService speechService,
        ILogger<HotkeyService> logger,
        IVoiceManager voiceManager,
        UserPreferences userPreferences,
        SynchronizationContext syncContext)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
        _speechService = speechService ?? throw new ArgumentNullException(nameof(speechService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _voiceManager = voiceManager ?? throw new ArgumentNullException(nameof(voiceManager));
        _userPreferences = userPreferences ?? throw new ArgumentNullException(nameof(userPreferences));
        _syncContext = syncContext ?? throw new ArgumentNullException(nameof(syncContext));
    }

    /// <summary>
    /// Starts the hotkey service by reading config and installing the keyboard hook.
    /// Creates a dedicated thread with a message pump to install and maintain the hook.
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

            // Create a dedicated thread with a message pump to install and maintain the hook
            // This ensures the hook callback is invoked, as WH_KEYBOARD_LL requires the
            // installing thread to run a message loop.
            var hookThread = new Thread(() =>
            {
                try
                {
                    // Install the low-level keyboard hook on this thread
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

                    // Run message loop to process keyboard events
                    Application.Run();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in keyboard hook thread");
                }
            })
            {
                Name = "HotkeyService-Hook",
                IsBackground = true
            };

            hookThread.Start();
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
            // Picker mode: show voice picker after capturing text
            await ProcessPickerModeAsync(hotkey);
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

    /// <summary>
    /// Processes picker-mode hotkey: captures selected text, shows voice picker, speaks with selected voice.
    ///
    /// Flow:
    /// 1. Capture text via ClipboardService (AC3.2)
    /// 2. If no text, return (AC3.7)
    /// 3. Load last-used voice from UserPreferences (AC3.5)
    /// 4. Get available voices from VoiceManager
    /// 5. Show VoicePickerForm on UI thread with last-used voice pre-selected (AC3.2)
    /// 6. If user selects a voice:
    ///    - Save selected voice to UserPreferences (AC3.5)
    ///    - Send text to SpeechService with selected voice (AC3.3)
    /// 7. If user dismisses (Escape or click-away):
    ///    - Do nothing, no side effects (AC3.6)
    /// </summary>
    internal async Task ProcessPickerModeAsync(HotkeySettings hotkey)
    {
        try
        {
            // Step 1: Capture selected text (AC3.2)
            var selectedText = await _clipboardService.CaptureSelectedTextAsync();

            // Step 2: If no text selected, return (AC3.7)
            if (string.IsNullOrWhiteSpace(selectedText))
            {
                _logger.LogDebug("No text selected for picker mode, skipping");
                return;
            }

            // Step 3: Load last-used voice from UserPreferences (AC3.5)
            var lastUsedVoice = _userPreferences.LastUsedPickerVoice;
            _logger.LogDebug("Loaded last-used voice: {Voice}", lastUsedVoice ?? "none");

            // Step 4: Get available voices
            var availableVoices = _voiceManager.GetAvailableVoices();
            if (availableVoices.Count == 0)
            {
                _logger.LogWarning("No voices available for picker mode");
                return;
            }

            // Step 5: Show VoicePickerForm on UI thread (AC3.2)
            // Marshal the ShowDialog() call to the main UI thread using SynchronizationContext.
            // The sync context was captured from the STA thread during startup in Program.cs.
            string? selectedVoiceId = null;

            // Use SynchronizationContext to marshal to the UI thread
            // SynchronizationContext.Send() is synchronous and blocks until the callback completes
            _syncContext.Send(_ =>
            {
                using (var pickerForm = new VoicePickerForm(availableVoices, lastUsedVoice))
                {
                    var dialogResult = pickerForm.ShowDialog();
                    selectedVoiceId = dialogResult == DialogResult.OK ? pickerForm.SelectedVoiceId : null;
                }
            }, null);

            // Step 6: Process the result
            if (!string.IsNullOrEmpty(selectedVoiceId))
            {
                // User selected a voice (AC3.3)
                // Step 6a: Save selected voice to UserPreferences (AC3.5)
                _userPreferences.SetLastUsedPickerVoice(selectedVoiceId);
                _logger.LogDebug("Saved selected voice to preferences: {Voice}", selectedVoiceId);

                // Step 6b: Send text to SpeechService with selected voice (AC3.3)
                var (queued, requestId) = _speechService.ProcessSpeakRequest(
                    selectedText,
                    "hotkey-picker",
                    selectedVoiceId);

                if (queued)
                {
                    _logger.LogInformation("Picker mode speech request queued: {RequestId} with voice {Voice}",
                        requestId, selectedVoiceId);
                }
                else
                {
                    _logger.LogWarning("Failed to queue picker mode speech request");
                }
            }
            else
            {
                // User dismissed the picker (AC3.6)
                _logger.LogDebug("Picker form dismissed, no speech request sent");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in picker mode handler");
        }
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
