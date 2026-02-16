# SysTTS Implementation Plan - Phase 5

**Goal:** Hotkeys configured in picker mode show a voice selection popup before speaking.

**Architecture:** VoicePickerForm is a compact borderless WinForms popup that lists available voices from VoiceManager, pre-selects the last-used voice (persisted in UserPreferences JSON file), and returns the selected voice ID. HotkeyService's picker mode handler captures text first, shows the form, then sends speech request with the chosen voice.

**Tech Stack:** WinForms (`Form`, `ListBox`), JSON file persistence for user preferences

**Scope:** 7 phases from original design (phase 5 of 7)

**Codebase verified:** 2026-02-15

---

## Acceptance Criteria Coverage

This phase implements:

### sys-tts.AC3: Global hotkeys capture selected text and speak with configured or user-selected voice (picker mode)
- **sys-tts.AC3.2 Success:** Pressing a picker-mode hotkey with text selected shows voice picker popup near cursor with available voices
- **sys-tts.AC3.3 Success:** Selecting a voice in the picker speaks the captured text with that voice
- **sys-tts.AC3.5 Success:** Last-used voice in picker mode is remembered across sessions
- **sys-tts.AC3.6 Success:** Picker popup dismisses on Escape, click-away, or voice selection without side effects

---

<!-- START_TASK_1 -->
### Task 1: Create VoicePickerForm

**Verifies:** sys-tts.AC3.2, sys-tts.AC3.5, sys-tts.AC3.6

**Files:**
- Create: `src/SysTTS/Forms/VoicePickerForm.cs`

**Implementation:**

Compact borderless WinForms popup for voice selection, positioned near the cursor when shown.

Form properties:
- `FormBorderStyle = FormBorderStyle.None`
- `ShowInTaskbar = false`
- `TopMost = true`
- `StartPosition = FormStartPosition.Manual`
- Size: ~200x300 (or auto-sized to voice list length, capped at screen height)

Contents:
- `ListBox` displaying available voice names
- Pre-selects last-used voice on open (passed via constructor parameter, AC3.5)

Positioning:
- Calculate `Location` from `Cursor.Position` (offset -10, -10 so cursor is inside form)
- Clamp to screen working area (`Screen.FromPoint(Cursor.Position).WorkingArea`) so popup doesn't go off-screen

Dismiss behaviors (AC3.6):
- **Voice selected** (double-click `ListBox` or Enter key): set `SelectedVoiceId` property, set `DialogResult = DialogResult.OK`, close form
- **Escape key** (`KeyDown` event, `e.KeyCode == Keys.Escape`): set `DialogResult = DialogResult.Cancel`, close form
- **Click outside** (`Deactivate` event): set `DialogResult = DialogResult.Cancel`, close form

Public API:
- Constructor: `VoicePickerForm(IReadOnlyList<VoiceInfo> voices, string? lastUsedVoiceId)`
- Property: `string? SelectedVoiceId` — set when user selects a voice, null if dismissed

**Verification:**

Run: `dotnet build src/SysTTS/SysTTS.csproj`
Expected: Build succeeds.

**Commit:** `feat: add VoicePickerForm with voice list and dismiss behaviors`
<!-- END_TASK_1 -->

<!-- START_TASK_2 -->
### Task 2: Create UserPreferences for last-used voice persistence

**Verifies:** sys-tts.AC3.5

**Files:**
- Create: `src/SysTTS/Services/UserPreferences.cs`
- Modify: `.gitignore` (add `user-preferences.json`)

**Implementation:**

Simple JSON file persistence for user preferences that change at runtime (separate from `appsettings.json` which is static config).

```csharp
namespace SysTTS.Services;

public class UserPreferences
{
    private readonly string _filePath;
    private readonly object _lock = new();

    public string? LastUsedPickerVoice { get; set; }

    public UserPreferences()
    {
        _filePath = Path.Combine(AppContext.BaseDirectory, "user-preferences.json");
        Load();
    }

    public void Load() { /* Read JSON file if exists, parse, set properties */ }
    public void Save() { /* Serialize to JSON, write to file, thread-safe via lock */ }
}
```

Key behaviors:
- File path: `user-preferences.json` next to the executable
- `Load()`: reads file if exists, deserializes, sets properties. Returns defaults if file missing.
- `Save()`: serializes current state, writes atomically. Thread-safe via `lock`.
- Only stores: `LastUsedPickerVoice` (for now, extensible for future preferences)

Add to `.gitignore`:
```
user-preferences.json
```

**Verification:**

Run: `dotnet build src/SysTTS/SysTTS.csproj`
Expected: Build succeeds.

**Commit:** `feat: add UserPreferences for last-used voice persistence`
<!-- END_TASK_2 -->

<!-- START_TASK_3 -->
### Task 3: Wire picker mode in HotkeyService

**Verifies:** sys-tts.AC3.2, sys-tts.AC3.3

**Files:**
- Modify: `src/SysTTS/Services/HotkeyService.cs`

**Implementation:**

Replace the "picker mode not yet implemented" stub from Phase 4 with actual picker flow.

Add `IVoiceManager` and `UserPreferences` to HotkeyService constructor parameters.

Picker mode handler (in `Task.Run` from hook callback):
1. Call `ClipboardService.CaptureSelectedTextAsync()` — capture text first
2. If null (nothing selected), return — AC3.7 already handled from Phase 4
3. Load `lastUsedVoice` from `UserPreferences.LastUsedPickerVoice`
4. Get available voices from `IVoiceManager.GetAvailableVoices()`
5. Show `VoicePickerForm` on the main WinForms thread:
   - Must marshal to UI thread since form needs STA + message pump
   - Use `Application.OpenForms[0]?.Invoke()` or store a reference to the main form/context
   - Create `VoicePickerForm(voices, lastUsedVoice)` and call `ShowDialog()` (AC3.2)
6. Check result:
   - If `DialogResult.OK` and `SelectedVoiceId` is not null:
     - Save `SelectedVoiceId` to `UserPreferences.LastUsedPickerVoice` and call `Save()` (AC3.5)
     - Call `SpeechService.ProcessSpeakRequest(capturedText, "hotkey-picker", selectedVoiceId)` (AC3.3)
   - If `DialogResult.Cancel`:
     - Do nothing — text is not spoken, no side effects (AC3.6)

**Verification:**

Run: `dotnet build src/SysTTS/SysTTS.csproj`
Expected: Build succeeds.

**Commit:** `feat: wire picker mode in HotkeyService with VoicePickerForm`
<!-- END_TASK_3 -->

<!-- START_TASK_4 -->
### Task 4: Register UserPreferences and update DI

**Files:**
- Modify: `src/SysTTS/Program.cs`

**Implementation:**

Register `UserPreferences` as singleton in DI:

```csharp
builder.Services.AddSingleton<UserPreferences>();
```

Ensure `HotkeyService` constructor receives `IVoiceManager` and `UserPreferences` (Phase 4 already registered it as singleton; just need to add the new constructor parameters).

No other DI changes needed — VoicePickerForm is created per-use, not registered in DI.

**Verification:**

Run: `dotnet build src/SysTTS/SysTTS.csproj`
Expected: Build succeeds.

**Commit:** `feat: register UserPreferences in DI`
<!-- END_TASK_4 -->

<!-- START_TASK_5 -->
### Task 5: Operational verification

**Step 1: Start the application**

Run: `dotnet run --project src/SysTTS/SysTTS.csproj`

**Step 2: Test picker-mode hotkey (sys-tts.AC3.2)**

1. Open any text editor, select some text
2. Press F22 (picker-mode hotkey from config)
3. Verify: Voice picker popup appears near the cursor, listing available voices

**Step 3: Test voice selection (sys-tts.AC3.3)**

1. In the picker popup, double-click a voice (or select + Enter)
2. Verify: Popup closes, selected text is spoken with the chosen voice

**Step 4: Test last-used voice persistence (sys-tts.AC3.5)**

1. Select text, press F22, choose a non-default voice (e.g., if multiple voices available)
2. Close the application (Quit from tray)
3. Restart the application
4. Select text, press F22
5. Verify: Previously chosen voice is pre-selected (highlighted) in the picker list

**Step 5: Test dismiss behaviors (sys-tts.AC3.6)**

Test each dismiss path:
1. Open picker (F22 with text selected) → Press Escape → verify popup closes, no speech
2. Open picker again → Click somewhere outside the popup → verify popup closes, no speech
3. Open picker again → Select a voice → verify popup closes, speech plays

**Step 6: Run all tests**

Run: `dotnet test tests/SysTTS.Tests/`
Expected: All tests pass (no regressions from Phase 4).

**Step 7: Verify all changes committed**

Run: `git status`
Expected: Working tree clean — all phase changes committed in prior tasks.
<!-- END_TASK_5 -->
