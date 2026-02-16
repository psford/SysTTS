# SysTTS Technical Specification

**Document Version:** 1.0
**Last Updated:** 2026-02-15
**Status:** Final Implementation

---

## 1. System Architecture

SysTTS is a system-level text-to-speech service composed of layered components: UI/tray, HTTP API, speech processing pipeline, audio output, and hotkey integration.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         WinForms Application (STA Thread)                    │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │ TrayApplicationContext                                               │   │
│  │ - System tray icon (notification area)                              │   │
│  │ - Context menu (SysTTS label, Quit action)                          │   │
│  │ - Application lifecycle management                                  │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │ HotkeyService (Dedicated Message Pump Thread)                        │   │
│  │ - Win32 WH_KEYBOARD_LL hook installation                             │   │
│  │ - Hotkey detection and dispatch (Direct/Picker modes)               │   │
│  │ - Message pump for hook callback invocation                         │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │ VoicePickerForm                                                      │   │
│  │ - Modal voice selection popup (Picker mode)                         │   │
│  │ - Voice list with radio button selection                            │   │
│  │ - Last-used voice pre-selection                                     │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│                    Kestrel HTTP Server (Thread Pool)                         │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │ ASP.NET Core Endpoints                                               │   │
│  │ - POST /api/speak (text + voice + source)                           │   │
│  │ - POST /api/speak-selection (voice override)                        │   │
│  │ - GET /api/voices (list available voices)                           │   │
│  │ - GET /api/status (queue depth, active voices)                      │   │
│  │ - POST /api/stop (stop playback + clear queue)                      │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │ SpeakSelectionHandler                                                │   │
│  │ - Decoupled endpoint handler for speak-selection                    │   │
│  │ - Uses ClipboardService to capture selected text                    │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│                      Speech Processing Pipeline                              │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │ SpeechService (Source Filtering + Voice Resolution)                  │   │
│  │ - Reads source config from appsettings.json                          │   │
│  │ - Applies regex filters (null = pass-through)                        │   │
│  │ - Resolves voice (override > source > default)                       │   │
│  │ - Constructs SpeechRequest with priority                             │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │ SpeechQueue (Priority Queue + Serial Processing)                     │   │
│  │ - PriorityQueue<SpeechRequest, int> (lower = higher priority)       │   │
│  │ - Max depth enforcement with eviction (lowest priority oldest)      │   │
│  │ - Interrupt-on-higher-priority logic                                 │   │
│  │ - Background processing task (one request at a time)                │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│                         Voice & Audio Synthesis                              │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │ VoiceManager (Piper ONNX Voice Catalog)                              │   │
│  │ - Scans voices/ directory for .onnx + .onnx.json pairs             │   │
│  │ - FileSystemWatcher for dynamic voice detection                     │   │
│  │ - ReaderWriterLockSlim for thread-safe catalog access              │   │
│  │ - Debounced rescan (100ms) on file system changes                   │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │ TtsEngine (Sherpa-ONNX Synthesis)                                    │   │
│  │ - Lazy-loaded OfflineTts instances (one per voice)                  │   │
│  │ - Semaphore per voice for serialized synthesis                      │   │
│  │ - Float32 audio output from Sherpa-ONNX                             │   │
│  │ - Speed parameter support (default 1.0x)                            │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│                       Audio Output & Playback                                │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │ AudioPlayer (NAudio WaveOutEvent)                                    │   │
│  │ - Converts float32 → int16 PCM                                       │   │
│  │ - RawSourceWaveStream from byte[] buffer                             │   │
│  │ - Cancellation token support for interrupt                           │   │
│  │ - PlaybackStopped event handling                                     │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │ ClipboardService (Clipboard Access)                                  │   │
│  │ - Ctrl+C simulation via SendKeys                                     │   │
│  │ - Clipboard save/restore                                             │   │
│  │ - STA thread marshaling for clipboard operations                     │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│                      Preferences & Configuration                             │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │ UserPreferences (Runtime Preferences)                                │   │
│  │ - Last-used voice for picker mode                                    │   │
│  │ - Persisted to user-preferences.json (gitignored)                   │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Data Flow

### 2.1 HTTP POST /api/speak

1. HTTP client sends: `{ text, source?, voice? }`
2. Program.cs endpoint invokes `SpeechService.ProcessSpeakRequest()`
3. SpeechService:
   - Retrieves `Sources:{source}` config from appsettings.json (falls back to `Sources:default`)
   - Applies regex filters; if filters are non-null, text must match ≥1 pattern
   - If filters are null, all text passes through
   - Resolves voice: `voiceOverride > sourceVoice > VoiceManager.ResolveVoiceId()`
   - Constructs `SpeechRequest(id, text, voiceId, priority, source)`
4. SpeechService enqueues via `ISpeechQueue.Enqueue()`
5. SpeechQueue:
   - Checks max depth; evicts lowest-priority oldest item if needed
   - Adds request to `PriorityQueue<SpeechRequest, int>` (lower = higher priority)
   - If `InterruptOnHigherPriority` is true and request.Priority < current request priority, cancels current playback
   - Signals processing loop to process next item
6. Background processing loop dequeues and calls `ProcessRequestAsync()`:
   - Calls `TtsEngine.Synthesize(text, voiceId)` → `(float[] samples, int sampleRate)`
   - Checks if request was cancelled after synthesis
   - Calls `AudioPlayer.PlayAsync(samples, sampleRate)` → plays audio
7. Endpoint returns `202 Accepted { queued: true, id: "..." }`

### 2.2 HTTP POST /api/speak-selection

1. HTTP client sends: `{ voice? }`
2. Program.cs endpoint dispatches to `SpeakSelectionHandler.Handle()`
3. SpeakSelectionHandler:
   - Calls `ClipboardService.CaptureSelectedTextAsync()` → simulates Ctrl+C, reads clipboard, restores clipboard
   - If text is empty, returns `200 OK { queued: false, text: "" }`
   - Calls `SpeechService.ProcessSpeakRequest(text, "speak-selection", request.Voice)`
4. Speech processing proceeds as per 2.1
5. Endpoint returns `202 Accepted { queued: true, id: "...", text: "..." }`

### 2.3 Hotkey Direct Mode

1. User presses registered hotkey key code (e.g., F23)
2. Win32 WH_KEYBOARD_LL hook callback detects key-down event
3. Hook callback offloads to `Task.Run` → `ProcessHotkeyAsync(hotkey)`
4. ProcessDirectModeAsync():
   - Calls `ClipboardService.CaptureSelectedTextAsync()` (same as speak-selection)
   - If text is empty, returns early (no speech)
   - Calls `SpeechService.ProcessSpeakRequest(text, "hotkey", hotkey.Voice)`
5. Speech processing proceeds as per 2.1

### 2.4 Hotkey Picker Mode

1. User presses registered hotkey key code (e.g., F22)
2. Win32 hook detects key-down, offloads to `Task.Run` → `ProcessHotkeyAsync(hotkey)`
3. ProcessPickerModeAsync():
   - Calls `ClipboardService.CaptureSelectedTextAsync()`
   - If text is empty, returns early
   - Loads `_userPreferences.LastUsedPickerVoice`
   - Marshals to UI thread via `SynchronizationContext.Send()`: creates `VoicePickerForm`, calls `ShowDialog()`
   - If user clicks OK:
     - Saves selected voice to `UserPreferences.SetLastUsedPickerVoice(voiceId)`
     - Calls `SpeechService.ProcessSpeakRequest(text, "hotkey-picker", selectedVoiceId)`
   - If user presses Escape or clicks away:
     - Form returns `DialogResult.Cancel`, no side effects
4. Speech processing proceeds as per 2.1

### 2.5 Stream Deck Button Press (Via HTTP)

1. Stream Deck plugin on user's computer detects button press
2. Plugin makes HTTP POST to `http://127.0.0.1:5100/api/speak` with text and voice
3. Proceeds as per 2.1

---

## 3. Component Catalog

### 3.1 Program.cs

**Purpose:** Entry point, host builder, service registration, endpoint mapping.

**Key Responsibilities:**
- Configure WebApplicationBuilder with DI services
- Register all TTS services as singletons
- Bind `appsettings.json` sections to `ServiceSettings` and `AudioSettings`
- Capture STA `SynchronizationContext` before `Application.Run()` (required for WinForms UI marshaling)
- Map HTTP endpoints (`/api/speak`, `/api/speak-selection`, `/api/voices`, `/api/status`, `/api/stop`)
- Start Kestrel on background threads, wait for startup signal
- Start WinForms application context on main STA thread
- Gracefully shutdown: stop HotkeyService, cancel Kestrel, dispose services

**Dependencies:**
- `IVoiceManager`, `ITtsEngine`, `IAudioPlayer`, `ISpeechQueue`, `ISpeechService`
- `IClipboardService`, `UserPreferences`, `HotkeyService`
- `IHostApplicationLifetime` (for ApplicationStarted event)

---

### 3.2 TrayApplicationContext

**Purpose:** WinForms application context, system tray icon, quit handling.

**Key Responsibilities:**
- Create system tray icon with "SysTTS" label
- Provide context menu: disabled "SysTTS - Running" label, Quit action
- On Quit: hide tray icon, dispose menus/icon, cancel CancellationTokenSource to shut down Kestrel

**Lifecycle:**
- Created in `Application.Run()` on main STA thread
- Lives until user clicks Quit or application closes

---

### 3.3 VoiceManager

**Implements:** `IVoiceManager`

**Purpose:** Scan and catalog Piper ONNX voice models from disk.

**Key Responsibilities:**
- Constructor: create `voices/` directory if missing, perform initial voice scan
- `ScanVoices()`: enumerate `*.onnx` files, verify paired `*.onnx.json` exists, parse metadata (sample rate, espeak voice), build catalog
- `GetAvailableVoices()`: return read-locked catalog
- `GetVoice(voiceId)`: return single voice info by ID
- `ResolveVoiceId(requestedId)`: return requested ID if exists, else default voice (with logging)
- FileSystemWatcher: monitor for `.onnx` and `.onnx.json` file creation/deletion, debounce rescan (100ms)
- Thread safety: ReaderWriterLockSlim for catalog access

**Configuration:**
- `ServiceSettings.VoicesPath` (default: "voices")
- `ServiceSettings.DefaultVoice` (default: "en_US-amy-medium")

**Data Structure:**
```csharp
public record VoiceInfo(
    string Id,              // filename without extension
    string Name,            // same as Id (can be enhanced with metadata)
    string ModelPath,       // full path to .onnx file
    string ConfigPath,      // full path to .onnx.json file
    int SampleRate          // parsed from .onnx.json audio.sample_rate
);
```

---

### 3.4 TtsEngine

**Implements:** `ITtsEngine`

**Purpose:** Synthesize speech using Sherpa-ONNX Piper models.

**Key Responsibilities:**
- Lazy-load `OfflineTts` instances per voice (cached in `ConcurrentDictionary`)
- For each voice, maintain a `SemaphoreSlim(1,1)` to serialize synthesis calls (Sherpa-ONNX not thread-safe per instance)
- `Synthesize(text, voiceId, speed=1.0f)` → resolve voice → acquire semaphore → call `tts.Generate()` → return `(float[] samples, int sampleRate)`
- On synthesis error: log and propagate exception
- `CreateTtsInstance()`: virtual protected factory for testing; creates `OfflineTts` with Piper (VITS) config

**Configuration:**
- `ServiceSettings.EspeakDataPath` (default: "espeak-ng-data", passed to Sherpa-ONNX config)

**Dependencies:**
- `IVoiceManager` (for voice lookup)
- `org.k2fsa.sherpa.onnx` NuGet package (v1.12.23)

---

### 3.5 AudioPlayer

**Implements:** `IAudioPlayer`

**Purpose:** Convert Sherpa-ONNX float32 samples to PCM and play via NAudio.

**Key Responsibilities:**
- `ConvertFloat32ToInt16Pcm(float[])`: convert float32 [-1.0, 1.0] → int16 [-32768, 32767], clamp out-of-range, return little-endian PCM bytes
- `PlayAsync(samples, sampleRate, cancellationToken)`: create `RawSourceWaveStream` from PCM bytes, initialize `WaveOutEvent`, register cancellation handler, await `PlaybackStopped` event
- `Stop()`: cancel current playback (thread-safe via lock)
- Linked CancellationToken: combines external token + internal playback cancellation

**Dependencies:**
- `NAudio` NuGet package (v2.2.1)

---

### 3.6 SpeechQueue

**Implements:** `ISpeechQueue`

**Purpose:** Priority-ordered queue for speech requests, serial processing.

**Key Responsibilities:**
- `Enqueue(SpeechRequest)`: add to `PriorityQueue<SpeechRequest, int>`, check max depth (evict lowest-priority oldest if exceeded), check interrupt-on-higher-priority logic
- `StopAndClear()`: cancel current playback, drain queue, return Task.CompletedTask
- `QueueDepth`: return current queue size (thread-safe via lock)
- Background `ProcessingLoopAsync()`: wait for signal, dequeue, process one request at a time
- `ProcessRequestAsync()`: synthesize text → play audio, with cancellation support
- On max depth: evict lowest-priority (highest number) oldest item by rebuilding queue

**Configuration:**
- `ServiceSettings.MaxQueueDepth` (default: 10)
- `ServiceSettings.InterruptOnHigherPriority` (default: true)

**Data Structure:**
```csharp
public record SpeechRequest(
    string Id,          // Guid
    string Text,        // text to synthesize
    string VoiceId,     // resolved voice ID
    int Priority,       // lower = higher priority
    string? Source      // source name (for logging)
);
```

---

### 3.7 SpeechService

**Implements:** `ISpeechService`

**Purpose:** Orchestrate between HTTP endpoints and speech queue.

**Key Responsibilities:**
- `ProcessSpeakRequest(text, source, voiceOverride)`:
  - Validate text is not empty
  - Load `Sources:{source}` from config, fall back to `Sources:default`
  - Apply regex filters: if non-null, text must match ≥1 pattern (case-insensitive, 100ms timeout)
  - If filters = null, all text passes
  - Resolve voice: `voiceOverride > sourceVoice > VoiceManager.ResolveVoiceId()`
  - Create SpeechRequest with source priority
  - Enqueue and return `(queued, requestId)` or `(false, null)` if filtered

**Configuration:**
- Source mapping: `appsettings.json` → `Configuration` sections

**Data Structure:**
```csharp
public class SourceSettings
{
    public string? Voice { get; set; }      // default voice for this source
    public string[]? Filters { get; set; }  // regex patterns (null = no filter)
    public int Priority { get; set; } = 3; // default priority
}
```

---

### 3.8 HotkeyService

**Purpose:** Install Win32 low-level keyboard hook, dispatch hotkey actions.

**Key Responsibilities:**
- `Start()`:
  - Read `Hotkeys` array from config
  - Parse key codes via `VirtualKeyParser` → build `_hotkeyMap` (VK code → HotkeySettings)
  - Create dedicated thread with message pump (required for WH_KEYBOARD_LL hook callback)
  - Install `WH_KEYBOARD_LL` hook via `SetWindowsHookEx()`
  - Run `Application.Run()` on thread for message loop
- Hook callback (`LowLevelKeyboardProc`):
  - Detect WM_KEYDOWN/WM_SYSKEYDOWN events
  - Check if VK code is registered
  - Offload to `Task.Run` → `ProcessHotkeyAsync()` (must return within 1000ms)
  - Call `CallNextHookEx()` to pass event to next hook
- `ProcessDirectModeAsync()`: capture text → SpeechService.ProcessSpeakRequest(text, "hotkey", hotkey.Voice)
- `ProcessPickerModeAsync()`:
  - Capture text
  - Load last-used voice from UserPreferences
  - Marshal to UI thread via SynchronizationContext.Send()
  - Show VoicePickerForm
  - If OK: save voice, queue speech
  - If Cancel: no side effects
- `Stop()`: uninstall hook via `UnhookWindowsHookEx()`

**Dependencies:**
- `IConfiguration` (for Hotkeys array)
- `IClipboardService`
- `ISpeechService`
- `IVoiceManager`
- `UserPreferences`
- `SynchronizationContext` (captured from STA main thread)
- Win32 P/Invoke: `SetWindowsHookEx`, `UnhookWindowsHookEx`, `CallNextHookEx`, `GetModuleHandle`

**Configuration:**
```csharp
public class HotkeySettings
{
    public string Key { get; set; }        // key name or code (e.g., "F23", "0x86")
    public string Mode { get; set; }       // "direct" or "picker"
    public string? Voice { get; set; }     // voice for direct mode
}
```

---

### 3.9 ClipboardService

**Implements:** `IClipboardService`

**Purpose:** Capture selected text from any application.

**Key Responsibilities:**
- `CaptureSelectedTextAsync()`:
  - Save current clipboard content
  - Simulate Ctrl+C via `SendKeys.SendWait()` to copy selected text
  - Read clipboard
  - Restore original clipboard content
  - Return text (or empty string if clipboard is empty)
- Marshaling: runs on main STA thread for clipboard access (WinForms requirement)

**Dependencies:**
- `System.Windows.Forms.SendKeys` for Ctrl+C simulation
- WinForms clipboard access

---

### 3.10 VoicePickerForm

**Purpose:** Modal voice selection popup for hotkey picker mode.

**Key Responsibilities:**
- Constructor: accept `IReadOnlyList<VoiceInfo>` and optional `lastUsedVoiceId`
- Display: list of voices in radio buttons or combobox
- Pre-selection: if `lastUsedVoiceId` exists in list, select it
- User actions:
  - Click OK (DialogResult.OK) → set `SelectedVoiceId`, close form
  - Press Escape or click outside (DialogResult.Cancel) → close form without setting SelectedVoiceId
- Property: `SelectedVoiceId` (string, null if user cancelled)

**Lifecycle:**
- Created and shown via `SynchronizationContext.Send()` on UI thread
- Disposed after user interaction

---

### 3.11 UserPreferences

**Purpose:** Persist runtime preferences (e.g., last-used voice).

**Key Responsibilities:**
- Constructor: load `user-preferences.json` from disk (gitignored) or create default
- `LastUsedPickerVoice` property: getter
- `SetLastUsedPickerVoice(voiceId)`: save to `user-preferences.json`
- File location: typically in application directory or user AppData

**Data Structure:**
```json
{
  "LastUsedPickerVoice": "en_US-amy-medium"
}
```

---

### 3.12 SpeakSelectionHandler

**Purpose:** Decoupled endpoint handler for POST /api/speak-selection.

**Key Responsibilities:**
- Static method `Handle()`: dependency-injected with `SpeakSelectionRequestDto`, `IClipboardService`, `ISpeechService`
- Calls `ClipboardService.CaptureSelectedTextAsync()` → captures selected text
- Calls `SpeechService.ProcessSpeakRequest(text, "speak-selection", request.Voice)`
- Returns `202 Accepted { queued, id, text }` or `200 OK { queued: false, text: "" }`

---

## 4. Configuration Contract

### 4.1 appsettings.json Schema

```json
{
  "Service": {
    "Port": <int>,                           // HTTP server port (default: 5100)
    "VoicesPath": <string>,                  // path to voices directory (default: "voices")
    "DefaultVoice": <string>,                // fallback voice ID (default: "en_US-amy-medium")
    "EspeakDataPath": <string>,              // path to espeak-ng-data (default: "espeak-ng-data")
    "MaxQueueDepth": <int>,                  // max queued requests before eviction (default: 10)
    "InterruptOnHigherPriority": <bool>      // interrupt playback on higher priority (default: true)
  },
  "Sources": {
    "<sourceName>": {
      "Voice": <string> or null,             // default voice for source (null = use service default)
      "Filters": [<regex>, ...] or null,     // regex patterns to match text; null = accept all
      "Priority": <int>                      // priority level (lower = higher priority; default: 3)
    },
    "default": { ... }                       // required fallback source
  },
  "Hotkeys": [
    {
      "Key": <string>,                       // key name or code (e.g., "F23", "0x86", "134")
      "Mode": <string>,                      // "direct" or "picker"
      "Voice": <string> or null              // voice for direct mode (null = use service default)
    },
    ...
  ],
  "Audio": {
    "OutputDevice": <string> or null,        // audio device name (null = default device)
    "Volume": <float>                        // volume level 0.0-1.0 (default: 1.0)
  }
}
```

### 4.2 Example appsettings.json

```json
{
  "Service": {
    "Port": 5100,
    "VoicesPath": "voices",
    "DefaultVoice": "en_US-amy-medium",
    "EspeakDataPath": "espeak-ng-data",
    "MaxQueueDepth": 10,
    "InterruptOnHigherPriority": true
  },
  "Sources": {
    "t-tracker": {
      "Voice": "custom-bear",
      "Filters": ["approaching", "arrived"],
      "Priority": 1
    },
    "notifications": {
      "Voice": null,
      "Filters": null,
      "Priority": 2
    },
    "default": {
      "Voice": "en_US-amy-medium",
      "Filters": null,
      "Priority": 3
    }
  },
  "Hotkeys": [
    { "Key": "F23", "Mode": "direct", "Voice": "en_US-amy-medium" },
    { "Key": "F22", "Mode": "picker", "Voice": null }
  ],
  "Audio": {
    "OutputDevice": null,
    "Volume": 1.0
  }
}
```

### 4.3 user-preferences.json Schema

```json
{
  "LastUsedPickerVoice": <string> or null
}
```

---

## 5. HTTP API Contract

### 5.1 POST /api/speak

**Purpose:** Queue speech synthesis and playback with optional source and voice override.

**Request:**
```json
{
  "text": <string>,           // (required) text to synthesize
  "source": <string> or null, // (optional) source name for config lookup
  "voice": <string> or null   // (optional) voice override
}
```

**Response (202 Accepted):**
```json
{
  "queued": <bool>,           // true if text was queued, false if filtered
  "id": <string>              // request ID (Guid) if queued
}
```

**Response (400 Bad Request):**
- If `text` is null, empty, or whitespace: `{ "error": "text is required" }`

**Behavior:**
- If text is empty → error
- If source not in config → fall back to "default" source
- If source filters defined and text doesn't match ≥1 pattern → `queued: false`, not queued
- If source filters null → text always passes
- Voice resolution: `voice param > source.voice > service.DefaultVoice`
- Enqueue via SpeechQueue with source priority
- If queue at max depth → evict lowest-priority oldest item

---

### 5.2 POST /api/speak-selection

**Purpose:** Capture selected text and queue speech with optional voice override.

**Request:**
```json
{
  "voice": <string> or null   // (optional) voice override
}
```

**Response (202 Accepted):**
```json
{
  "queued": <bool>,           // true if text was captured and queued
  "id": <string>,             // request ID (Guid) if queued
  "text": <string>            // captured selected text (empty if none)
}
```

**Behavior:**
- Calls `ClipboardService.CaptureSelectedTextAsync()` on server side
- If selected text is empty → `{ queued: false, text: "" }` (200 OK)
- If text captured → process via SpeechService with source="speak-selection"
- Voice resolution: `voice param > service.DefaultVoice`

---

### 5.3 GET /api/voices

**Purpose:** List available voices.

**Response (200 OK):**
```json
[
  {
    "id": <string>,           // voice ID (filename without extension)
    "name": <string>,         // voice name (same as ID for now)
    "sampleRate": <int>       // sample rate from .onnx.json
  },
  ...
]
```

**Example:**
```json
[
  { "id": "en_US-amy-medium", "name": "en_US-amy-medium", "sampleRate": 22050 },
  { "id": "en_GB-alba-medium", "name": "en_GB-alba-medium", "sampleRate": 22050 }
]
```

---

### 5.4 GET /api/status

**Purpose:** Check server health and queue status.

**Response (200 OK):**
```json
{
  "running": <bool>,          // true if server is running
  "activeVoices": <int>,      // count of available voices
  "queueDepth": <int>         // current queue size
}
```

**Example:**
```json
{ "running": true, "activeVoices": 2, "queueDepth": 3 }
```

---

### 5.5 POST /api/stop

**Purpose:** Stop current playback and clear the queue.

**Response (200 OK):**
```json
{
  "stopped": <bool>           // always true
}
```

**Behavior:**
- Calls `SpeechQueue.StopAndClear()`
- Cancels current playback
- Drains remaining queue items
- Immediate return (does not wait for current synthesis to finish)

---

## 6. Speech Queue Behavior

### 6.1 Priority Ordering

- Queue is `PriorityQueue<SpeechRequest, int>` where lower priority number = higher priority
- Example: priority 1 (hotkey) > priority 2 (notifications) > priority 3 (default API)
- Queue ordered by priority, then FIFO within same priority

### 6.2 Max Depth & Eviction

- When queue reaches `MaxQueueDepth` (default: 10), before enqueuing new request:
  - Find all items with lowest priority (highest number)
  - Among those, evict the oldest (FIFO)
  - Log eviction at DEBUG level
- Example: if 3 items at priority 3 (lowest), evict the first one added

### 6.3 Interrupt-on-Higher-Priority

- If `InterruptOnHigherPriority` is true (default):
  - When new request enqueued with priority lower than currently playing request
  - Cancel current playback immediately
  - New request moves to front of queue and processes next
- If `InterruptOnHigherPriority` is false:
  - New request enqueued normally, does not interrupt current playback

### 6.4 Serial Processing

- Background `ProcessingLoopAsync()` processes one request at a time
- After each request completes (success or error):
  - Clear current playback state
  - Wait for next signal
- No concurrent synthesis or playback

### 6.5 Cancellation Flow

- `StopAndClear()` calls `_currentPlaybackCts?.Cancel()`
- Cancellation token propagated to:
  - AudioPlayer.PlayAsync()
  - TtsEngine.Synthesize() (indirectly, via synthesis completion check)
- Request marked as cancelled in logs; no error thrown

---

## 7. Voice Manager Lifecycle

### 7.1 Startup Scan

- VoiceManager constructor:
  - Create `voices/` directory if missing
  - Call `ScanVoices()`:
    - Enumerate all `*.onnx` files
    - For each, check for paired `*.onnx.json`
    - Parse sample rate from JSON metadata
    - Build in-memory catalog
  - Log number of voices found

### 7.2 FileSystemWatcher

- Monitor `voices/` directory for file creation/deletion
- On `.onnx` or `.onnx.json` file change:
  - Signal debounce timer (100ms delay)
  - After 100ms of no changes, call `ScanVoices()` to rebuild catalog
- Allows adding/removing voices without restart (as long as config changes don't require restart)

### 7.3 Lazy Loading (TtsEngine)

- First time a voice is used:
  - VoiceManager returns VoiceInfo
  - TtsEngine calls `CreateTtsInstance(voiceInfo)` to create OfflineTts
  - OfflineTts loads ONNX file from disk (expensive I/O + memory)
  - Instance cached in `ConcurrentDictionary<string, OfflineTts>`
- Subsequent uses of same voice: reuse cached instance
- Only instances actually used are loaded into memory

### 7.4 Thread Safety

- Catalog access via ReaderWriterLockSlim:
  - Multiple concurrent readers allowed (GetVoice, GetAvailableVoices)
  - Single writer for scan (exclusive lock)
- ONNX instance creation thread-safe via ConcurrentDictionary
- Semaphore per voice serializes synthesis calls

---

## 8. Dependencies Table

| NuGet Package | Version | Purpose |
|---------------|---------|---------|
| `org.k2fsa.sherpa.onnx` | 1.12.23 | Piper neural TTS model inference |
| `org.k2fsa.sherpa.onnx.runtime.win-x64` | 1.12.23 | Native ONNX Runtime binaries (Windows x64) |
| `NAudio` | 2.2.1 | Audio output (WaveOutEvent, WaveFormat) |
| `Microsoft.AspNetCore.App` | (framework reference) | ASP.NET Core hosting, Kestrel |

---

## 9. Thread Model

### 9.1 Main STA Thread (WinForms)

- Thread: `[STAThread] Main()` via `Application.Run()`
- Responsibilities:
  - WinForms message pump (handles window messages, events)
  - VoicePickerForm.ShowDialog() and window events
  - Clipboard operations (WinForms Clipboard requires STA)
  - Hotkey hook callback context
- Duration: entire application lifetime

### 9.2 Hotkey Service Thread

- Thread: dedicated background thread created in `HotkeyService.Start()`
- Responsibilities:
  - Install WH_KEYBOARD_LL hook (requires message pump on installing thread)
  - Run `Application.Run()` for message loop (hook callbacks invoked on this thread)
- Duration: from HotkeyService.Start() until HotkeyService.Stop()
- Properties: `IsBackground = true`, name="HotkeyService-Hook"

### 9.3 Kestrel Thread Pool

- Threads: managed by Kestrel HTTP server (thread pool)
- Responsibilities:
  - Accept HTTP connections
  - Invoke endpoint handlers (SpeechService, etc.)
  - Return responses
- Duration: from app.RunAsync() until application shutdown
- Bound to localhost:5100 via `UseUrls()`

### 9.4 SpeechQueue Background Task

- Task: `_processingTask = ProcessingLoopAsync()`
- Thread: managed by default TaskScheduler (likely thread pool)
- Responsibilities:
  - Dequeue SpeechRequest items
  - Call TtsEngine.Synthesize() (CPU-bound)
  - Call AudioPlayer.PlayAsync() (I/O-bound)
- Serialization: one request at a time via semaphore signal

### 9.5 Summary

```
Main (STA)
├─ WinForms message pump
├─ TrayApplicationContext
├─ VoicePickerForm UI
├─ Clipboard ops
└─ Hotkey callback source

Kestrel (thread pool)
├─ HTTP server
├─ Endpoint handlers
└─ SpeechService (source filtering, config)

HotkeyService (background)
├─ Message pump for hook
└─ Hook callback dispatch

SpeechQueue (background task)
├─ Serial processing loop
├─ Synthesis (TtsEngine)
└─ Playback (AudioPlayer)
```

**No concurrent synthesis:** Sherpa-ONNX not thread-safe per instance; semaphore serializes.
**No concurrent playback:** SpeechQueue processes one request at a time.
**No concurrent clipboard:** ClipboardService runs on STA thread.

---

## 10. Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-02-15 | Initial release: Phase 7 documentation. Complete architecture, data flow, component catalog, API contract, configuration schema, thread model. |

---

## Appendix A: Win32 Keyboard Hook Details

### Hook Type: WH_KEYBOARD_LL

- Low-level keyboard hook: intercepts keyboard input before other hooks
- Callback must return within 1000ms, else Windows may treat as hung
- Callback returns `CallNextHookEx()` to pass event to next hook

### Key Struct: KBDLLHOOKSTRUCT

```csharp
[StructLayout(LayoutKind.Sequential)]
public struct KBDLLHOOKSTRUCT
{
    public uint vkCode;         // virtual key code (0-255)
    public uint scanCode;       // scan code
    public KBDLLHookFlags flags; // event flags
    public uint time;           // event timestamp
    public nint dwExtraInfo;    // extra information
}
```

### Virtual Key Codes

- F22-F24: VK codes 0x85-0x87
- Named keys parsed via `VirtualKeyParser.ParseKeyCode(string)` → nullable int

---

## Appendix B: Piper Model Format

### Voice File Structure

```
voices/
├─ en_US-amy-medium.onnx        # neural network model
├─ en_US-amy-medium.onnx.json   # metadata + espeak config
├─ en_GB-alba-medium.onnx
├─ en_GB-alba-medium.onnx.json
└─ ...

espeak-ng-data/                 # shared phonemization data
├─ lang/
├─ phondata
└─ ...
```

### .onnx.json Metadata

```json
{
  "espeak": {
    "voice": "en-us",           // espeak-ng voice identifier
    "rate": 1.0
  },
  "audio": {
    "sample_rate": 22050,       // output sample rate (Hz)
    "channels": 1               // mono
  },
  "inference": {
    "noise_scale": 0.667,
    "length_scale": 1.0,
    "noise_w": 0.8
  }
}
```

---

## Appendix C: Sherpa-ONNX Configuration

### OfflineTtsConfig for Piper

```csharp
var config = new OfflineTtsConfig();
config.Model.Vits.Model = "/path/to/model.onnx";
config.Model.Vits.DataDir = "/path/to/espeak-ng-data";
config.Model.NumThreads = 2;          // synthesis parallelism
config.Model.Provider = "cpu";        // use CPU inference
```

### Audio Output

- Sherpa-ONNX returns float32 samples in range [-1.0, 1.0]
- Sample rate determined by model (from .onnx.json)
- AudioPlayer converts to int16 PCM for NAudio playback

---

## Appendix D: Example Request/Response Flows

### Flow 1: HTTP /api/speak with Source Filtering

**Request:**
```
POST http://127.0.0.1:5100/api/speak
Content-Type: application/json

{
  "text": "The truck is approaching the warehouse",
  "source": "t-tracker",
  "voice": null
}
```

**Processing:**
1. SpeechService.ProcessSpeakRequest("The truck is approaching...", "t-tracker", null)
2. Load Sources:t-tracker config: voice="custom-bear", filters=["approaching", "arrived"], priority=1
3. Apply filters: match "approaching" ✓ in text
4. Resolve voice: null → "custom-bear"
5. Create SpeechRequest(id, text, "custom-bear", priority=1, source="t-tracker")
6. Enqueue

**Response:**
```json
{
  "queued": true,
  "id": "a1b2c3d4-..."
}
```

**Processing (background):**
1. Synthesize: TtsEngine.Synthesize("The truck...", "custom-bear") → (float[1234], 22050)
2. Play: AudioPlayer.PlayAsync(float[1234], 22050) → audio plays

---

### Flow 2: Hotkey Picker Mode

**Setup:**
- User presses F22 (Mode: "picker")
- Selected text: "Hello world"
- UserPreferences.LastUsedPickerVoice: "en_US-amy-medium"

**Processing:**
1. Hook detects F22 key-down
2. ProcessPickerModeAsync(hotkeySettings):
   - ClipboardService.CaptureSelectedTextAsync() → "Hello world"
   - Load UserPreferences.LastUsedPickerVoice → "en_US-amy-medium"
   - Get available voices from VoiceManager
   - Marshal to UI thread: VoicePickerForm.ShowDialog() with pre-selected voice
3. User clicks OK on "en_GB-alba-medium"
   - SelectedVoiceId = "en_GB-alba-medium"
4. Back in ProcessPickerModeAsync:
   - UserPreferences.SetLastUsedPickerVoice("en_GB-alba-medium")
   - SpeechService.ProcessSpeakRequest("Hello world", "hotkey-picker", "en_GB-alba-medium")
5. Enqueue with priority=3 (default)

**Background Processing:**
1. Synthesize with "en_GB-alba-medium"
2. Play audio

**Next time user presses F22:**
- LastUsedPickerVoice = "en_GB-alba-medium" (pre-selected in picker)

---

## Appendix E: Error Handling

### Synthesis Errors

| Error | Handling | Logging |
|-------|----------|---------|
| Voice not found | ResolveVoiceId() falls back to default | WARNING |
| ONNX file corrupted | TtsEngine throws, request logged as failed | ERROR |
| Regex timeout | Filter returns false (safe timeout), logged | WARNING |
| Clipboard empty | CaptureSelectedTextAsync() returns "", no speech | DEBUG |
| Queue full | Evict lowest-priority oldest item, log | WARNING |
| Hook installation fails | Log error, service still starts, hotkeys disabled | ERROR |
| Playback error | Log error, continue to next queue item | ERROR |

### Recovery

- Synthesis/playback errors: logged, does not crash application
- Next queue item processes normally
- Malformed requests: return 400 Bad Request
- Missing config: fall back to defaults (port 5100, voices directory, etc.)

---

## Appendix F: Performance Characteristics

### Model Loading

- First use of a voice: ~500ms-2s (depends on model size, disk speed, CPU)
- Subsequent uses: instant (cached)
- Lazy loading minimizes startup time

### Synthesis

- Typical: 100-500ms per 10 seconds of speech (depends on model, text length, CPU)
- Can be interrupted with cancellation token
- Serialized per voice (no concurrent synthesis on same voice)

### Queue Depth

- Max default: 10 items
- Eviction: O(n) for rebuild on max depth
- Dequeue: O(log n) priority queue operation
- Typical latency: <1ms per operation

### Memory

- Per voice: ~100-200MB (ONNX model cached in memory after first load)
- Speech queue: <1MB (typically <10 requests)
- Total: proportional to number of loaded voices

---

## Appendix G: Security Considerations

### Input Validation

- All text inputs validated for null/empty before processing
- Regex filter timeout (100ms) prevents ReDoS attacks
- No code injection via configuration (JSON parse with validation)

### Clipboard Access

- Clipboard read/write only used for text (no code execution)
- Clipboard restored to original content after capture
- Runs on STA thread (WinForms safe)

### File Access

- Voice models loaded from configured directory only
- No path traversal (model paths validated by VoiceManager)
- espeak-ng-data accessed via configured path

### Network

- HTTP server bound to localhost only (`127.0.0.1`)
- No remote access by default
- Clients must be on same machine

### Win32 Hook

- WH_KEYBOARD_LL hook only installed if explicitly configured with hotkeys
- Hook uninstalled on shutdown
- Hook callback dispatches to Task (no blocking operations in hook itself)

---

**End of Technical Specification**
