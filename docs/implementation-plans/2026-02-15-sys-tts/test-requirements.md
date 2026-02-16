# SysTTS Test Requirements

This document maps every acceptance criterion from the [SysTTS design plan](design-plans/2026-02-15-sys-tts.md) to specific automated tests or human verification procedures. Acceptance criteria use the slug `sys-tts` (e.g., sys-tts.AC1.1, sys-tts.AC2.3).

**Conventions:**
- **Phase 1** is infrastructure/scaffolding -- all ACs verified operationally (human verification only).
- **Phase 7** is documentation only -- no automated tests.
- **Phases 2-6** contain automated tests as specified in their implementation plans.
- **sys-tts.AC5** (T-Tracker integration) is explicitly deferred to a separate T-Tracker PR per the design document. ACs are listed but no tests are created.

---

## Automated Tests

### sys-tts.AC2: HTTP endpoint accepts text, applies per-source voice/filter config, and speaks

| AC | Description | Test Type | Test File | Phase |
|----|-------------|-----------|-----------|-------|
| sys-tts.AC2.1 | `POST /api/speak` with `{ text, source }` resolves voice from source config and produces audible speech | Unit | `tests/SysTTS.Tests/Services/SpeechServiceTests.cs` | 3 |
| sys-tts.AC2.2 | `POST /api/speak` with explicit `voice` field overrides the source-configured voice | Unit | `tests/SysTTS.Tests/Services/SpeechServiceTests.cs` | 3 |
| sys-tts.AC2.3 | `POST /api/speak` with source that has regex filters silently drops text that matches no filter (returns 202, no speech) | Unit | `tests/SysTTS.Tests/Services/SpeechServiceTests.cs` | 3 |
| sys-tts.AC2.4 | `POST /api/speak` with source that has `filters: null` speaks all text regardless of content | Unit | `tests/SysTTS.Tests/Services/SpeechServiceTests.cs` | 3 |
| sys-tts.AC2.7 | Higher-priority request interrupts lower-priority speech currently playing (when `InterruptOnHigherPriority` is true) | Unit | `tests/SysTTS.Tests/Services/SpeechQueueTests.cs` | 3 |
| sys-tts.AC2.8 | Queue at max depth drops oldest low-priority item when new request arrives | Unit | `tests/SysTTS.Tests/Services/SpeechQueueTests.cs` | 3 |

**Test details (Phase 3, `SpeechServiceTests.cs`):**

| Test Method | Verifies AC |
|-------------|-------------|
| `ProcessSpeakRequest_WithSource_ResolvesVoiceFromSourceConfig` | sys-tts.AC2.1 |
| `ProcessSpeakRequest_WithVoiceOverride_UsesOverride` | sys-tts.AC2.2 |
| `ProcessSpeakRequest_TextNotMatchingFilters_ReturnsFalse` | sys-tts.AC2.3 |
| `ProcessSpeakRequest_TextMatchingFilter_ReturnsTrue` | sys-tts.AC2.3 |
| `ProcessSpeakRequest_NullFilters_SpeaksAllText` | sys-tts.AC2.4 |
| `ProcessSpeakRequest_UnknownSource_FallsBackToDefault` | (supporting) |

**Test details (Phase 3, `SpeechQueueTests.cs`):**

| Test Method | Verifies AC |
|-------------|-------------|
| `Enqueue_HigherPriorityDuringPlayback_InterruptsCurrentSpeech` | sys-tts.AC2.7 |
| `Enqueue_QueueAtMaxDepth_DropsLowestPriorityItem` | sys-tts.AC2.8 |
| `Enqueue_MultipleRequests_ProcessesInPriorityOrder` | sys-tts.AC6.1 (supporting) |
| `StopAndClear_CancelsCurrentAndEmptiesQueue` | sys-tts.AC2.5 (supporting) |

---

### sys-tts.AC4: Voice models load from voices/ directory -- adding a voice requires only dropping in an ONNX file and updating config

| AC | Description | Test Type | Test File | Phase |
|----|-------------|-----------|-----------|-------|
| sys-tts.AC4.1 | `GET /api/voices` returns list of all `.onnx` + `.onnx.json` pairs found in `voices/` directory | Unit | `tests/SysTTS.Tests/Services/VoiceManagerTests.cs` | 2 |
| sys-tts.AC4.2 | Dropping a new ONNX model pair into `voices/` while running makes it available without restart (FileSystemWatcher) | Integration | `tests/SysTTS.Tests/Services/VoiceManagerTests.cs` | 2 |
| sys-tts.AC4.3 | Voice models are loaded lazily on first use and cached in memory for subsequent requests | Unit | `tests/SysTTS.Tests/Services/TtsEngineTests.cs` | 2 |
| sys-tts.AC4.4 | Source config referencing a missing voice falls back to `DefaultVoice` with a logged warning | Unit | `tests/SysTTS.Tests/Services/VoiceManagerTests.cs` | 2 |
| sys-tts.AC4.5 | `voices/` directory with zero valid models -- service starts, endpoints return empty list, speak requests log error | Unit | `tests/SysTTS.Tests/Services/VoiceManagerTests.cs` | 2 |

**Test details (Phase 2, `VoiceManagerTests.cs`):**

| Test Method | Verifies AC |
|-------------|-------------|
| `GetAvailableVoices_WithValidPairs_ReturnsList` | sys-tts.AC4.1 |
| `FileSystemWatcher_NewModelDropped_BecomesAvailable` | sys-tts.AC4.2 |
| `ResolveVoiceId_MissingVoice_FallsBackToDefault` | sys-tts.AC4.4 |
| `GetAvailableVoices_EmptyDirectory_ReturnsEmptyList` | sys-tts.AC4.5 |
| `GetAvailableVoices_OnnxWithoutJson_IgnoresModel` | (supporting) |
| `ResolveVoiceId_ExistingVoice_ReturnsRequestedId` | (supporting) |
| `ResolveVoiceId_NullInput_ReturnsDefault` | (supporting) |
| `GetVoice_ExistingId_ReturnsVoiceInfo` | (supporting) |
| `GetVoice_MissingId_ReturnsNull` | (supporting) |

**Test details (Phase 2, `TtsEngineTests.cs`):**

| Test Method | Verifies AC |
|-------------|-------------|
| `Synthesize_FirstCallForVoice_CreatesNewInstance` | sys-tts.AC4.3 |
| `Synthesize_SecondCallSameVoice_ReusesInstance` | sys-tts.AC4.3 |
| `Synthesize_DifferentVoice_CreatesSeparateInstance` | (supporting) |
| `Dispose_DisposesAllCachedInstances` | (supporting) |

**Test details (Phase 2, `AudioPlayerTests.cs`):**

These tests verify the float32-to-int16 PCM conversion logic used by AudioPlayer. While not directly mapped to an AC, they validate a critical internal component.

| Test Method | Purpose |
|-------------|---------|
| `ConvertFloat32ToInt16Pcm_PositiveOne_ProducesMaxInt16` | Boundary: +1.0f to Int16.MaxValue |
| `ConvertFloat32ToInt16Pcm_NegativeOne_ProducesMinInt16` | Boundary: -1.0f to Int16.MinValue |
| `ConvertFloat32ToInt16Pcm_Zero_ProducesZero` | Zero-crossing |
| `ConvertFloat32ToInt16Pcm_ClampAboveOne_ProducesMaxInt16` | Out-of-range clamping |
| `ConvertFloat32ToInt16Pcm_ClampBelowNegativeOne_ProducesMinInt16` | Out-of-range clamping |
| `ConvertFloat32ToInt16Pcm_MultipleSamples_ProducesCorrectByteArray` | Multi-sample byte ordering |

---

### sys-tts.AC6: Cross-Cutting Behaviors (partial automation)

| AC | Description | Test Type | Test File | Phase |
|----|-------------|-----------|-----------|-------|
| sys-tts.AC6.1 | Speech queue processes requests serially -- no audio collision from concurrent requests | Unit | `tests/SysTTS.Tests/Services/SpeechQueueTests.cs` | 3 |

**Note:** sys-tts.AC6.1 is verified by the SpeechQueue's single processing task architecture. The test `Enqueue_MultipleRequests_ProcessesInPriorityOrder` confirms serial processing via mocked `ITtsEngine` and `IAudioPlayer` calls.

---

### Phase 4: VirtualKeyParser Tests

These tests verify the config string to virtual key code parsing logic used by HotkeyService. While not directly mapped to a top-level AC, they validate the hotkey configuration pipeline that supports sys-tts.AC3.1.

| Test Method | Test File | Phase |
|-------------|-----------|-------|
| `ParseKeyCode_F22_Returns0x85` | `tests/SysTTS.Tests/Interop/VirtualKeyParserTests.cs` | 4 |
| `ParseKeyCode_F23_Returns0x86` | `tests/SysTTS.Tests/Interop/VirtualKeyParserTests.cs` | 4 |
| `ParseKeyCode_F24_Returns0x87` | `tests/SysTTS.Tests/Interop/VirtualKeyParserTests.cs` | 4 |
| `ParseKeyCode_HexString_ParsesCorrectly` | `tests/SysTTS.Tests/Interop/VirtualKeyParserTests.cs` | 4 |
| `ParseKeyCode_DecimalString_ParsesCorrectly` | `tests/SysTTS.Tests/Interop/VirtualKeyParserTests.cs` | 4 |
| `ParseKeyCode_UnknownKey_ReturnsNull` | `tests/SysTTS.Tests/Interop/VirtualKeyParserTests.cs` | 4 |
| `ParseKeyCode_CaseInsensitive_Works` | `tests/SysTTS.Tests/Interop/VirtualKeyParserTests.cs` | 4 |
| `ParseKeyCode_ModifierKeys_Work` | `tests/SysTTS.Tests/Interop/VirtualKeyParserTests.cs` | 4 |

---

## Human Verification

### sys-tts.AC1: Service runs as Windows application with system tray presence

All AC1 criteria are verified operationally in Phase 1. This phase is infrastructure scaffolding; no automated tests are specified.

| AC | Description | Justification | Verification Approach | Phase |
|----|-------------|---------------|----------------------|-------|
| sys-tts.AC1.1 | Application starts, tray icon appears in system tray, and `GET /api/status` returns 200 with `{ running: true }` | Requires visual confirmation of system tray icon and a running Kestrel server. Full integration with Windows shell. | 1. Run `dotnet run --project src/SysTTS/SysTTS.csproj`. 2. Visually confirm tray icon appears. 3. Run `curl http://localhost:5100/api/status` and verify response `{"running":true,"activeVoices":0,"queueDepth":0}`. | 1 |
| sys-tts.AC1.2 | Right-clicking tray icon shows context menu with status info and quit option | Requires physical mouse interaction with Windows system tray and visual confirmation of context menu contents. | 1. Right-click the SysTTS tray icon. 2. Verify context menu shows "SysTTS - Running" (disabled) and "Quit". | 1 |
| sys-tts.AC1.3 | Selecting "Quit" from tray menu shuts down Kestrel, unhooks hotkeys, and exits cleanly (no orphan processes) | Requires physical mouse interaction and process-level verification that no orphan processes remain. | 1. Click "Quit" from tray context menu. 2. Verify application exits. 3. Confirm no orphan processes via `Get-Process -Name SysTTS -ErrorAction SilentlyContinue` returning nothing. 4. Confirm `curl http://localhost:5100/api/status` fails (connection refused). | 1 |
| sys-tts.AC1.4 | If configured port is already in use, application logs error and exits with descriptive message (does not crash silently) | Requires two running instances to trigger port conflict, and visual confirmation of error dialog. | 1. Start first instance: `dotnet run --project src/SysTTS/SysTTS.csproj`. 2. Start second instance in another terminal. 3. Verify error dialog appears with "Failed to start HTTP server on port 5100" message. 4. Verify second instance exits after dialog is dismissed. | 1 |

---

### sys-tts.AC2: HTTP endpoint (human verification for audio-dependent ACs)

| AC | Description | Justification | Verification Approach | Phase |
|----|-------------|---------------|----------------------|-------|
| sys-tts.AC2.1 | `POST /api/speak` with `{ text, source }` resolves voice from source config and produces audible speech | Audible speech output requires human ear to confirm audio plays correctly through speakers. The voice resolution logic is unit-tested separately. | 1. Start SysTTS. 2. Run `curl -X POST http://localhost:5100/api/speak -H "Content-Type: application/json" -d "{\"text\":\"Hello, this is a test\",\"source\":\"default\"}"`. 3. Confirm response is 202. 4. Confirm audible speech plays through default audio device. | 3 |
| sys-tts.AC2.5 | `POST /api/stop` cancels current speech and clears the queue | Requires audible confirmation that speech stops mid-utterance. Queue clearing is unit-tested. | 1. POST a long text to `/api/speak`. 2. Immediately POST to `/api/stop`. 3. Confirm speech stops immediately (mid-word). | 3 |
| sys-tts.AC2.6 | `POST /api/speak` with missing `text` field returns 400 error | Can be verified via HTTP request/response without audio, but grouped here for completeness as it is verified in Phase 3 operational testing. | 1. Run `curl -X POST http://localhost:5100/api/speak -H "Content-Type: application/json" -d "{\"source\":\"default\"}"`. 2. Confirm response is 400 with `{"error":"text is required"}`. | 3 |

---

### sys-tts.AC3: Global hotkeys capture selected text and speak

| AC | Description | Justification | Verification Approach | Phase |
|----|-------------|---------------|----------------------|-------|
| sys-tts.AC3.1 | Pressing a direct-mode hotkey with text selected in any application speaks that text with the hotkey's configured voice | Requires physical keyboard interaction with Win32 keyboard hook, cross-application clipboard capture, and audible speech confirmation. Cannot be automated without UI automation frameworks. | 1. Start SysTTS. 2. Open any text editor (Notepad, VS Code, browser). 3. Select some text. 4. Press F23. 5. Confirm selected text is spoken aloud with en_US-amy-medium voice. | 4 |
| sys-tts.AC3.2 | Pressing a picker-mode hotkey with text selected shows voice picker popup near cursor with available voices | Requires physical keyboard interaction, visual confirmation of popup appearance and position near cursor. | 1. Start SysTTS. 2. Select text in any editor. 3. Press F22 (picker-mode hotkey). 4. Confirm voice picker popup appears near the cursor listing available voices. | 5 |
| sys-tts.AC3.3 | Selecting a voice in the picker speaks the captured text with that voice | Requires physical mouse/keyboard interaction with popup and audible speech confirmation. | 1. Open picker (F22 with text selected). 2. Double-click a voice (or select + Enter). 3. Confirm popup closes and selected text is spoken with the chosen voice. | 5 |
| sys-tts.AC3.4 | Clipboard contents are preserved after hotkey capture (saved before, restored after) | Requires verifying clipboard state across applications before and after hotkey use. | 1. Copy "original clipboard text" to clipboard (Ctrl+C normally). 2. Select different text in an editor. 3. Press F23. 4. Paste (Ctrl+V) somewhere. 5. Confirm pasted content is "original clipboard text" (not the hotkey-captured text). | 4 |
| sys-tts.AC3.5 | Last-used voice in picker mode is remembered across sessions | Requires application restart and visual confirmation that previously selected voice is pre-selected in picker. | 1. Select text, press F22, choose a non-default voice. 2. Close application (Quit from tray). 3. Restart application. 4. Select text, press F22. 5. Confirm previously chosen voice is pre-selected (highlighted) in the picker list. | 5 |
| sys-tts.AC3.6 | Picker popup dismisses on Escape, click-away, or voice selection without side effects | Requires testing three separate dismiss paths with physical interaction and confirming no unintended behavior. | Test each dismiss path: 1. Open picker (F22) then press Escape -- confirm popup closes, no speech. 2. Open picker again then click outside the popup -- confirm popup closes, no speech. 3. Open picker again then select a voice -- confirm popup closes, speech plays. | 5 |
| sys-tts.AC3.7 | Hotkey pressed with no text selected (empty clipboard after Ctrl+C) does nothing (no error, no speech) | Requires physical keyboard interaction with no text selected and confirmation of no visible or audible side effects. | 1. Click in an empty area (deselect all text). 2. Press F23. 3. Confirm nothing happens -- no error, no speech, no crash. | 4 |
| sys-tts.AC3.8 | `POST /api/speak-selection` performs clipboard capture in the service and speaks with specified or default voice | Requires selected text to exist in another application for clipboard capture to work, plus audible speech confirmation. | 1. Start SysTTS. 2. Select text in any editor. 3. Run `curl -X POST http://localhost:5100/api/speak-selection -H "Content-Type: application/json" -d "{}"`. 4. Confirm response is 202 and captured text is spoken aloud. 5. Repeat with `{"voice":"en_US-amy-medium"}` and confirm voice override works. | 6 |

---

### sys-tts.AC5: T-Tracker sends notification text to the service as the first integration

**DEFERRED:** All AC5 criteria are explicitly deferred to a separate T-Tracker PR per the design document. No tests are created in the SysTTS project for these criteria. They will be tested in the T-Tracker repository when the integration PR is created.

| AC | Description | Justification | Verification Approach | Phase |
|----|-------------|---------------|----------------------|-------|
| sys-tts.AC5.1 | T-Tracker's notification code includes fire-and-forget `fetch()` to `POST /api/speak` with `source: 't-tracker'` | Deferred to T-Tracker PR. Implementation is in T-Tracker's `notifications.js`, not in SysTTS. | Code review of T-Tracker PR confirming `fetch()` call to `/api/speak` with `source: 't-tracker'` alongside existing `new Notification()`. | N/A |
| sys-tts.AC5.2 | T-Tracker functions normally when SysTTS service is not running (`.catch(() => {})` pattern) | Deferred to T-Tracker PR. Requires T-Tracker running without SysTTS. | 1. Stop SysTTS. 2. Trigger T-Tracker notification. 3. Confirm T-Tracker functions normally (notification appears, no console errors beyond expected network failure). | N/A |
| sys-tts.AC5.3 | SysTTS applies t-tracker source filters -- only text matching configured patterns (e.g., "approaching", "arrived") produces speech | Deferred to T-Tracker PR for end-to-end verification. Note: the filter logic itself is unit-tested in SysTTS via sys-tts.AC2.3 with `SpeechServiceTests`. | 1. Start SysTTS with t-tracker source config (filters: ["approaching", "arrived"]). 2. POST text matching filter from T-Tracker -- confirm speech. 3. POST non-matching text -- confirm silence. | N/A |

---

### sys-tts.AC6: Cross-Cutting Behaviors

| AC | Description | Justification | Verification Approach | Phase |
|----|-------------|---------------|----------------------|-------|
| sys-tts.AC6.2 | Kestrel bound to `127.0.0.1` only -- no external network access, no firewall prompt | Requires network-level verification that the service is not accessible from external machines. Cannot be reliably unit-tested. | 1. Start SysTTS. 2. Confirm no Windows Firewall prompt appeared on first launch. 3. From another machine on the same network, attempt `curl http://<host-ip>:5100/api/status` and confirm connection refused. 4. Verify Kestrel configuration in `Program.cs` uses `http://127.0.0.1:{port}`. | 1 |
| sys-tts.AC6.3 | Stream Deck plugin populates voice dropdown from `GET /api/voices` and triggers speech via HTTP endpoints | Requires physical Stream Deck hardware, Elgato Stream Deck software, and plugin installation. Cannot be automated without hardware. | 1. Build plugin: `cd streamdeck-plugin && npm run build`. 2. Install: `streamdeck link com.systts.sdPlugin`. 3. Open Stream Deck app, find "SysTTS" category. 4. Drag "Speak Selected Text" to a button -- confirm settings panel shows voice dropdown populated from `/api/voices`. 5. Select text, press button -- confirm speech plays. 6. Drag "Speak Custom Text" -- configure text, press button -- confirm text spoken. 7. Drag "Stop Speaking" -- start speech, press button -- confirm speech stops. | 6 |

---

## Test Summary by Phase

| Phase | Description | Automated Test Files | Human Verification ACs |
|-------|-------------|---------------------|----------------------|
| 1 | Project scaffolding and service host | None (infrastructure only) | AC1.1, AC1.2, AC1.3, AC1.4, AC6.2 |
| 2 | TTS engine and voice manager | `VoiceManagerTests.cs`, `TtsEngineTests.cs`, `AudioPlayerTests.cs` | None |
| 3 | Speech queue and HTTP speak endpoint | `SpeechServiceTests.cs`, `SpeechQueueTests.cs` | AC2.1 (audio), AC2.5 (audio), AC2.6 (HTTP) |
| 4 | Global hotkeys and clipboard capture | `VirtualKeyParserTests.cs` | AC3.1, AC3.4, AC3.7 |
| 5 | Voice picker popup | None (UI-only phase) | AC3.2, AC3.3, AC3.5, AC3.6 |
| 6 | Speak-selection endpoint and Stream Deck | None (hardware-dependent) | AC3.8, AC6.3 |
| 7 | Documentation | None (docs only) | None |

---

## Automated Test File Index

| Test File | Phase | Test Count | ACs Covered |
|-----------|-------|------------|-------------|
| `tests/SysTTS.Tests/Services/VoiceManagerTests.cs` | 2 | 9 | AC4.1, AC4.2, AC4.4, AC4.5 |
| `tests/SysTTS.Tests/Services/TtsEngineTests.cs` | 2 | 4 | AC4.3 |
| `tests/SysTTS.Tests/Services/AudioPlayerTests.cs` | 2 | 6 | (internal component) |
| `tests/SysTTS.Tests/Services/SpeechServiceTests.cs` | 3 | 6 | AC2.1, AC2.2, AC2.3, AC2.4 |
| `tests/SysTTS.Tests/Services/SpeechQueueTests.cs` | 3 | 4 | AC2.5, AC2.7, AC2.8, AC6.1 |
| `tests/SysTTS.Tests/Interop/VirtualKeyParserTests.cs` | 4 | 8 | (supporting AC3.1) |
| **Total** | | **37** | |
