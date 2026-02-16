# SysTTS Implementation Plan - Phase 4

**Goal:** User can select text anywhere in Windows and trigger speech via configurable global hotkeys.

**Architecture:** HotkeyService installs a Win32 low-level keyboard hook (`WH_KEYBOARD_LL` via `SetWindowsHookEx`), detects registered hotkeys from config, captures selected text via ClipboardService (save clipboard → `SendInput` Ctrl+C → read → restore), and sends text to SpeechService for queuing and playback.

**Tech Stack:** Win32 P/Invoke (`SetWindowsHookEx`, `SendInput`, `GetAsyncKeyState`), System.Windows.Forms.Clipboard

**Scope:** 7 phases from original design (phase 4 of 7)

**Codebase verified:** 2026-02-15

---

## Acceptance Criteria Coverage

This phase implements:

### sys-tts.AC3: Global hotkeys capture selected text and speak with configured or user-selected voice (partial — direct mode only)
- **sys-tts.AC3.1 Success:** Pressing a direct-mode hotkey with text selected in any application speaks that text with the hotkey's configured voice
- **sys-tts.AC3.4 Success:** Clipboard contents are preserved after hotkey capture (saved before, restored after)
- **sys-tts.AC3.7 Failure:** Hotkey pressed with no text selected (empty clipboard after Ctrl+C) does nothing (no error, no speech)

---

<!-- START_TASK_1 -->
### Task 1: Create Win32 interop classes

**Files:**
- Create: `src/SysTTS/Interop/NativeMethods.cs`

**Implementation:**

Static class with all P/Invoke declarations for keyboard hooks and input simulation. Use `nint` for pointer types (.NET 8 convention). `DllImport` for callback-based hooks.

Contents:
- **Hook functions:** `SetWindowsHookEx`, `UnhookWindowsHookEx`, `CallNextHookEx`, `GetModuleHandle`
- **Input simulation:** `SendInput` with `INPUT` struct (`StructLayout(LayoutKind.Explicit)`) containing `KEYBDINPUT`
- **Key state:** `GetAsyncKeyState` for modifier key detection in hook callback
- **Delegate:** `LowLevelKeyboardProc` delegate type
- **Struct:** `KBDLLHOOKSTRUCT` for hook callback data (`StructLayout(LayoutKind.Sequential)`)
- **Constants:**
  - `WH_KEYBOARD_LL = 13`
  - `WM_KEYDOWN = 0x0100`, `WM_SYSKEYDOWN = 0x0104`
  - `INPUT_KEYBOARD = 1`, `KEYEVENTF_KEYUP = 0x0002`
  - Virtual key codes: `VK_CONTROL = 0x11`, `VK_C = 0x43`, `VK_F22 = 0x85`, `VK_F23 = 0x86`, `VK_F24 = 0x87`, etc.

**Verification:**

Run: `dotnet build src/SysTTS/SysTTS.csproj`
Expected: Build succeeds.

**Commit:** `feat: add Win32 P/Invoke interop for keyboard hooks and SendInput`
<!-- END_TASK_1 -->

<!-- START_TASK_2 -->
### Task 2: Create HotkeySettings model and VirtualKeyParser

**Files:**
- Create: `src/SysTTS/Settings/HotkeySettings.cs`
- Create: `src/SysTTS/Interop/VirtualKeyParser.cs`

**Implementation:**

`HotkeySettings` — strongly-typed class for a single hotkey binding from config:

```csharp
namespace SysTTS.Settings;

public class HotkeySettings
{
    public string Key { get; set; } = "";
    public string Mode { get; set; } = "direct"; // "direct" or "picker"
    public string? Voice { get; set; }
}
```

`VirtualKeyParser` — static class that parses key name strings to virtual key codes:
- Dictionary of known key names → VK codes (F1-F24, Ctrl, Alt, Shift, letter keys)
- Supports hex string input ("0x86")
- Supports decimal string input ("134")
- Case-insensitive matching
- Returns `int?` — null if key name not recognized

**Verification:**

Run: `dotnet build src/SysTTS/SysTTS.csproj`
Expected: Build succeeds.

**Commit:** `feat: add HotkeySettings and VirtualKeyParser`
<!-- END_TASK_2 -->

<!-- START_TASK_3 -->
### Task 3: Create ClipboardService

**Verifies:** sys-tts.AC3.4, sys-tts.AC3.7

**Files:**
- Create: `src/SysTTS/Services/IClipboardService.cs`
- Create: `src/SysTTS/Services/ClipboardService.cs`

**Implementation:**

`IClipboardService` interface:

```csharp
namespace SysTTS.Services;

public interface IClipboardService
{
    Task<string?> CaptureSelectedTextAsync();
}
```

`ClipboardService` — captures selected text from any application by simulating Ctrl+C, preserving original clipboard contents:

`CaptureSelectedTextAsync()` flow:
1. Save current clipboard text via `Clipboard.GetText()` (null if empty)
2. Clear clipboard via `Clipboard.Clear()`
3. Simulate Ctrl+C via `SendInput` — 4 `INPUT` structs: Ctrl down, C down, C up, Ctrl up
4. Wait 100ms for OS to process clipboard update (`Task.Delay(100)`)
5. Read clipboard text via `Clipboard.GetText()`
6. Restore original clipboard contents (or clear if was null) — AC3.4
7. Return captured text — null if nothing was selected (AC3.7)

All `System.Windows.Forms.Clipboard` operations must run on STA thread. If called from non-STA thread, use `Control.Invoke()` to marshal to the WinForms UI thread, or run clipboard ops on a dedicated STA thread.

Constructor takes `ILogger<ClipboardService>`.

**Verification:**

Run: `dotnet build src/SysTTS/SysTTS.csproj`
Expected: Build succeeds.

**Commit:** `feat: add ClipboardService with clipboard save/restore and Ctrl+C simulation`
<!-- END_TASK_3 -->

<!-- START_TASK_4 -->
### Task 4: Create HotkeyService

**Verifies:** sys-tts.AC3.1

**Files:**
- Create: `src/SysTTS/Services/HotkeyService.cs`

**Implementation:**

`HotkeyService` installs a Win32 low-level keyboard hook and dispatches registered hotkey actions.

Key behaviors:
- Constructor takes `IConfiguration`, `IClipboardService`, `ISpeechService`, `ILogger<HotkeyService>`
- `Start()`:
  1. Reads `Hotkeys` array from `IConfiguration`, binds to `List<HotkeySettings>`
  2. Parses each hotkey's `Key` string to VK code via `VirtualKeyParser`
  3. Builds lookup dictionary: VK code → `HotkeySettings`
  4. Installs hook via `SetWindowsHookEx(WH_KEYBOARD_LL, callback, GetModuleHandle(null), 0)`
  5. Stores delegate reference as field to prevent GC
- Hook callback (`LowLevelKeyboardProc`):
  1. On `WM_KEYDOWN`/`WM_SYSKEYDOWN`, marshal `lParam` to `KBDLLHOOKSTRUCT`
  2. Look up `vkCode` in registered hotkeys dictionary
  3. If found, offload to `Task.Run` (must return from callback within 1000ms):
     - **Direct mode:** call `ClipboardService.CaptureSelectedTextAsync()`, if text captured → `SpeechService.ProcessSpeakRequest(text, "hotkey", hotkey.Voice)` (AC3.1)
     - If null text returned, do nothing (AC3.7)
     - **Picker mode:** log "picker mode not yet implemented" (Phase 5)
  4. Always call `CallNextHookEx` to pass event to other hooks
- `Stop()`: calls `UnhookWindowsHookEx`, nulls hook ID
- Implements `IDisposable`

**Verification:**

Run: `dotnet build src/SysTTS/SysTTS.csproj`
Expected: Build succeeds.

**Commit:** `feat: add HotkeyService with Win32 keyboard hook for direct mode`
<!-- END_TASK_4 -->

<!-- START_TASK_5 -->
### Task 5: Register services and add hotkey config

**Files:**
- Modify: `src/SysTTS/Program.cs`
- Modify: `src/SysTTS/appsettings.json`

**Implementation:**

Register services in DI:

```csharp
builder.Services.AddSingleton<IClipboardService, ClipboardService>();
builder.Services.AddSingleton<HotkeyService>();
```

Start HotkeyService after Kestrel confirms running (in the `ApplicationStarted` callback):

```csharp
lifetime.ApplicationStarted.Register(() =>
{
    var hotkeyService = app.Services.GetRequiredService<HotkeyService>();
    hotkeyService.Start();
});
```

Stop in `TrayApplicationContext.OnQuit()` or application shutdown path — before cancelling Kestrel CTS, call `hotkeyService.Stop()`.

Update `appsettings.json` `Hotkeys` array:

```json
"Hotkeys": [
    { "Key": "F23", "Mode": "direct", "Voice": "en_US-amy-medium" },
    { "Key": "F22", "Mode": "picker" }
]
```

**Verification:**

Run: `dotnet build src/SysTTS/SysTTS.csproj`
Expected: Build succeeds.

**Commit:** `feat: register HotkeyService and configure example hotkeys`
<!-- END_TASK_5 -->

<!-- START_TASK_6 -->
### Task 6: Create VirtualKeyParser tests

**Files:**
- Create: `tests/SysTTS.Tests/Interop/VirtualKeyParserTests.cs`

**Testing:**

Test the config string → VK code parsing logic:
- `ParseKeyCode_F22_Returns0x85` — "F22" → 0x85
- `ParseKeyCode_F23_Returns0x86` — "F23" → 0x86
- `ParseKeyCode_F24_Returns0x87` — "F24" → 0x87
- `ParseKeyCode_HexString_ParsesCorrectly` — "0x86" → 0x86
- `ParseKeyCode_DecimalString_ParsesCorrectly` — "134" → 0x86
- `ParseKeyCode_UnknownKey_ReturnsNull` — "Unknown" → null
- `ParseKeyCode_CaseInsensitive_Works` — "f23" → 0x86
- `ParseKeyCode_ModifierKeys_Work` — "Ctrl" → 0x11, "Alt" → 0x12, "Shift" → 0x10

Follow project testing patterns: xUnit `[Fact]`, FluentAssertions, `MethodName_Condition_Expected` naming.

**Verification:**

Run: `dotnet test tests/SysTTS.Tests/`
Expected: All tests pass.

**Commit:** `test: add VirtualKeyParser tests`
<!-- END_TASK_6 -->

<!-- START_TASK_7 -->
### Task 7: Operational verification

**Step 1: Start the application**

Run: `dotnet run --project src/SysTTS/SysTTS.csproj`

**Step 2: Test direct-mode hotkey (sys-tts.AC3.1)**

1. Open any text editor (Notepad, VS Code, browser)
2. Select some text
3. Press F23
4. Verify: Selected text is spoken aloud with en_US-amy-medium voice

**Step 3: Test clipboard preservation (sys-tts.AC3.4)**

1. Copy "original clipboard text" to clipboard (Ctrl+C normally)
2. Select different text in an editor
3. Press F23
4. Paste (Ctrl+V) somewhere — verify clipboard contains "original clipboard text"

**Step 4: Test no text selected (sys-tts.AC3.7)**

1. Click in an empty area (deselect all text)
2. Press F23
3. Verify: Nothing happens — no error, no speech, no crash

**Step 5: Run all tests**

Run: `dotnet test tests/SysTTS.Tests/`
Expected: All tests pass.

**Step 6: Verify all changes committed**

Run: `git status`
Expected: Working tree clean — all phase changes committed in prior tasks.
<!-- END_TASK_7 -->
