# CLAUDE.md — SysTTS Project Conventions

> Last verified: 2026-02-16

Claude Code development guidelines for the SysTTS project.

---

## Project Overview

**SysTTS** — A system-level Text-to-Speech service for Windows with:
- **System tray icon** for lifecycle control and quick access
- **HTTP API** (localhost:5100) for programmatic TTS via any application
- **Global hotkeys** (F22, F23) for quick-speak with voice selection
- **Stream Deck plugin** for button-based voice control and custom actions
- **Neural voices** powered by Piper ONNX models via Sherpa-ONNX
- **Audio playback** via NAudio with volume control and device selection
- **Priority-based speech queue** with interrupt behavior

---

## Project Structure

```
SysTTS/
├── CLAUDE.md                      # Project conventions and guidelines
├── README.md                      # Quick start and feature overview
├── SysTTS.sln                     # Solution file (src/ and tests/ folders)
│
├── src/SysTTS/                    # Main WinForms + ASP.NET Core application
│   ├── Program.cs                 # Entry point: host builder, endpoints
│   ├── appsettings.json           # Configuration (port, voices, hotkeys, etc.)
│   ├── TrayApplicationContext.cs  # System tray icon and lifecycle
│   ├── Services/                  # Core service implementations
│   │   ├── VoiceManager.cs        # Voice scanning, FileSystemWatcher, metadata
│   │   ├── TtsEngine.cs           # Sherpa-ONNX wrapper (thread-unsafe per instance)
│   │   ├── AudioPlayer.cs         # NAudio WaveOutEvent wrapper
│   │   ├── SpeechQueue.cs         # Priority queue with serial processing
│   │   ├── SpeechService.cs       # Source filtering, voice resolution
│   │   ├── HotkeyService.cs       # Win32 keyboard hooks (dedicated thread)
│   │   ├── ClipboardService.cs    # Clipboard save/restore, Ctrl+C simulation, OLE message pumping
│   │   └── UserPreferences.cs     # Persists picker voice to user-preferences.json
│   ├── Handlers/                  # HTTP request handlers
│   │   └── SpeakSelectionHandler.cs # Clipboard integration for hotkeys
│   ├── Interop/                   # Win32 P/Invoke declarations
│   │   ├── NativeMethods.cs       # Win32 API declarations
│   │   └── VirtualKeyParser.cs    # Virtual key code parsing
│   ├── Models/                    # DTOs and data models
│   ├── Settings/                  # Settings POCOs (Service, Audio, Hotkey, Source)
│   ├── Forms/                     # WinForms UI
│   │   └── VoicePickerForm.cs     # Voice selection dialog
│   └── SysTTS.csproj              # Project configuration
│
├── tests/SysTTS.Tests/            # xUnit test project
│   ├── SysTTS.Tests.csproj
│   └── *.cs                       # Test files
│
├── streamdeck-plugin/             # Stream Deck plugin (TypeScript/Node.js)
│   ├── .sdignore                  # Files to exclude from plugin package
│   ├── package.json               # npm scripts
│   ├── tsconfig.json
│   ├── rollup.config.mjs          # Build configuration
│   ├── com.systts.sdPlugin/       # Plugin output directory
│   └── src/                       # TypeScript source
│       ├── index.ts               # Plugin entry point
│       ├── actions/               # Stream Deck actions (Speak, Stop, etc.)
│       └── common/                # API client, configuration helpers
│
├── voices/                        # Piper ONNX voice models (gitignored)
│   ├── en_US-amy-medium.onnx
│   ├── en_US-amy-medium.onnx.json
│   └── ...
│
├── espeak-ng-data/                # Shared phonemization data (gitignored)
│   └── ...
│
├── user-preferences.json          # Runtime: persisted picker voice (gitignored)
│
├── scripts/                       # Utility scripts
│   ├── download-models.ps1        # Download voice models from HuggingFace
│   └── ...
│
└── docs/                          # Documentation
    ├── CUSTOM_VOICES.md           # Voice training and import guide
    ├── INTEGRATION.md             # API reference and integration examples
    ├── TECHNICAL_SPEC.md          # Architecture and implementation reference
    └── ...
```

---

## Build & Run

### Prerequisites
- **.NET 8 SDK** (includes ASP.NET Core / Kestrel)
- **Windows 10 or 11** (WinForms + Win32 keyboard hooks)
- **Voice models** (download via `download-models.ps1`)
- *(Optional)* **Stream Deck + Elgato software** for plugin development

### Build

```bash
# Build main application
dotnet build src/SysTTS/SysTTS.csproj

# Build tests
dotnet build tests/SysTTS.Tests/SysTTS.Tests.csproj

# Build Stream Deck plugin
cd streamdeck-plugin
npm install
npm run build
```

### Run

```bash
# Download voice models (one-time)
powershell.exe -File scripts/download-models.ps1

# Run main application (starts tray icon + HTTP API)
dotnet run --project src/SysTTS/SysTTS.csproj

# Verify HTTP API is running
curl http://127.0.0.1:5100/api/status

# Run tests
dotnet test tests/SysTTS.Tests/

# Watch Stream Deck plugin source for changes
cd streamdeck-plugin
npm run watch
```

### Configuration

Edit `src/SysTTS/appsettings.json` to configure:
- **Port** (default: 5100, localhost only)
- **Default voice** (must match a voice in `voices/`)
- **Voice paths** for models and espeak data
- **Hotkey mappings** (F22/F23 modes)
- **Speech sources** (T-Tracker, custom apps) with filtering and priority
- **Audio output device** and volume

Configuration changes require application restart.

---

## Testing

### Framework & Tools
- **Test Framework:** xUnit
- **Mocking:** Moq
- **Assertions:** FluentAssertions
- **Naming Convention:** `MethodName_Condition_Expected`
- **Pattern:** AAA (Arrange, Act, Assert)

### Example Test Structure

```csharp
[Fact]
public void ProcessSpeakRequest_WithValidText_ReturnsQueuedTrue()
{
    // Arrange
    var mockVoiceManager = new Mock<IVoiceManager>();
    var service = new SpeechService(mockVoiceManager.Object);

    // Act
    var (queued, id) = service.ProcessSpeakRequest("Hello", null, null);

    // Assert
    queued.Should().BeTrue();
    id.Should().NotBeEmpty();
}
```

### Running Tests

```bash
# Run all tests
dotnet test tests/SysTTS.Tests/

# Run single test class
dotnet test tests/SysTTS.Tests/ --filter "ClassName"

# Run with verbosity
dotnet test tests/SysTTS.Tests/ -v detailed

# Run with code coverage
dotnet test tests/SysTTS.Tests/ /p:CollectCoverageData=true
```

---

## Architecture Notes

### Threading Model

- **Main STA Thread:** WinForms application context, message pump
  - Required for clipboard operations (safe access)
  - Required for UI dialogs (VoicePickerForm)
  - SynchronizationContext captured at startup and injected via DI

- **HotkeyService-Hook Thread:** Dedicated background thread with its own message pump
  - Installs WH_KEYBOARD_LL hook (requires a message loop on the installing thread)
  - Runs `Application.Run()` to pump messages for the hook callback
  - Offloads hotkey processing to `Task.Run()` to stay within 1000ms callback timeout
  - Marshals UI operations (VoicePickerForm) to STA thread via SynchronizationContext

- **Kestrel Thread Pool:** ASP.NET Core HTTP API
  - Runs on background threads (controlled by Kestrel)
  - Handles API requests independently from UI thread
  - Marshals clipboard operations to STA thread via SynchronizationContext

- **Synthesis Background Tasks:**
  - TtsEngine synthesis runs off-thread (Sherpa-ONNX)
  - AudioPlayer playback managed by NAudio (off-thread)
  - No blocking operations on main thread

**Key Rule:** SpeechService, TtsEngine, AudioPlayer, and SpeechQueue are **singletons** registered in DI. Parallel requests serialize through the queue; synthesis is thread-pooled.

### Speech Queue Behavior

- **Priority queue:** Lower priority number = higher precedence
- **Serial playback:** Synthesis and audio output serialized (no audio collision)
- **Max depth:** Configurable (default: 10) — oldest low-priority items evicted when full
- **Interrupt:** If `InterruptOnHigherPriority` is true, a high-priority request stops current speech
- **Sources:** Each source (t-tracker, default) has priority and optional regex filters

### Voice Manager Lifecycle

- **Startup:** Scans `voices/` directory for `.onnx` + `.onnx.json` pairs
- **Lazy Loading:** TtsEngine instances created on-demand per voice
- **Caching:** Loaded engines cached in memory (not unloaded until shutdown)
- **FileSystemWatcher:** Monitors `voices/` for new/deleted models at runtime
  - New models detected immediately and available to API
  - Config changes require application restart for source mappings

### Win32 Keyboard Hooks

- Uses `WH_KEYBOARD_LL` low-level hook via P/Invoke (not `RegisterHotKey`)
- Hook installed on dedicated `HotkeyService-Hook` background thread with its own message pump
- Callback filters for registered virtual key codes from config (e.g., F22, F23)
- Processing offloaded to `Task.Run()` to avoid blocking the 1000ms hook callback timeout
- Delegate reference stored in field to prevent GC collection
- Supports two modes:
  - **Direct:** Immediately capture selected text via ClipboardService and speak with last-picked voice (falls back to configured voice if no picker selection has been made)
  - **Picker:** Capture text, show VoicePickerForm on STA thread (via SynchronizationContext), speak with selected voice; saves selection to UserPreferences for both picker and direct mode

### Sherpa-ONNX & NAudio

- **Sherpa-ONNX:** ONNX Runtime wrapper for TTS synthesis
  - NOT thread-safe per instance (TtsEngine serializes calls)
  - Processes text → phonemes → audio (float32)
  - Runtime package: `org.k2fsa.sherpa.onnx.runtime.win-x64`
  - Requires `espeak-ng-data/` directory for phonemization

- **NAudio:** Audio playback
  - Wraps WaveOutEvent (system audio device)
  - Converts float32 → int16 PCM
  - Volume control via WaveOutEvent.Volume property

---

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| `org.k2fsa.sherpa.onnx` | 1.12.23 | TTS synthesis wrapper |
| `org.k2fsa.sherpa.onnx.runtime.win-x64` | 1.12.23 | ONNX Runtime for Windows x64 |
| `NAudio` | 2.2.1 | Audio playback (WaveOutEvent) |
| `Microsoft.AspNetCore.App` (framework ref) | — | Kestrel HTTP server, DI |
| `xunit` | 2.5.3 | Test framework |
| `Moq` | 4.20.72 | Mocking library |
| `FluentAssertions` | 6.12.2 | Assertion library |

---

## Configuration Schema

**`appsettings.json`** structure:

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
    "source-name": {
      "voice": "voice-id",
      "filters": ["pattern1", "pattern2"] | null,
      "priority": 1
    }
  },
  "Hotkeys": [
    { "Key": "F23", "Mode": "direct", "Voice": "voice-id" },
    { "Key": "F22", "Mode": "picker" }
  ],
  "Audio": {
    "OutputDevice": null,
    "Volume": 1.0
  }
}
```

---

## HTTP API

**Base URL:** `http://127.0.0.1:5100`

| Endpoint | Method | Request | Response | Purpose |
|----------|--------|---------|----------|---------|
| `/api/status` | GET | — | `{ running, activeVoices, queueDepth }` | Service health |
| `/api/voices` | GET | — | `[{ id, name, sampleRate }]` | List available voices |
| `/api/speak` | POST | `{ text, source?, voice? }` | 202 `{ queued, id }` | Queue text to speak |
| `/api/speak-selection` | POST | `{ voice? }` | 202 `{ queued, id, text }` | Speak clipboard selection |
| `/api/stop` | POST | — | 200 `{ stopped }` | Stop and clear queue |

**Error handling:**
- 400: Bad request (missing required fields, e.g., empty text on `/api/speak`)
- 202 Accepted: Request queued (not yet played)
- 200 OK with `{ queued: false }`: `/api/speak-selection` when no text is selected

---

## Development Workflow

### Before Committing

1. **Test:** Run `dotnet test tests/SysTTS.Tests/` — all must pass
2. **Build:** Run `dotnet build src/SysTTS/SysTTS.csproj` — no errors
3. **Manual verification:** Start app, test hotkeys, verify API endpoints
4. **Lint:** Watch for compiler warnings (treat as errors)

### When Adding Features

1. **Tests first:** Write failing test in `tests/SysTTS.Tests/`
2. **Implementation:** Write minimal code to pass test
3. **Integration:** Verify hotkeys/API/audio playback work end-to-end
4. **Documentation:** Update TECHNICAL_SPEC.md with new components/behavior
5. **Configuration:** Update appsettings.json defaults if needed

### Stream Deck Plugin Development

1. **Link for development:** `streamdeck link` (creates symlink to plugin folder)
2. **Watch for changes:** `npm run watch` (rebuilds on file changes)
3. **Reload in Stream Deck:** Right-click plugin → Reload
4. **Build for distribution:** `npm run build` (outputs `.streamDeckPlugin` package)

### Debugging

- **Main app:** `dotnet run --project src/SysTTS/SysTTS.csproj` (logs to console)
- **Tests:** Set breakpoints in test, run with Visual Studio debugger
- **API calls:** Use `curl` or Postman to test endpoints locally
- **Hotkeys:** Verify message pump is running (WinForms must be active)

---

## Common Issues

| Issue | Cause | Solution |
|-------|-------|----------|
| Port 5100 in use | Another app listening | Change port in appsettings.json, or kill existing process |
| Voices not found | Models not downloaded | Run `scripts/download-models.ps1` |
| Hotkeys not responding | WinForms window not active | Focus the main window (tray icon exists) |
| Audio not playing | Output device not available | Check `Audio.OutputDevice` in config, set to null for default |
| ONNX model error | Corrupted file or old Sherpa-ONNX version | Re-download model, verify .onnx.json is valid |
| Espeak data missing | Path misconfigured | Verify `Service.EspeakDataPath` points to espeak-ng-data directory |

---

## References

- **Piper Voices:** https://huggingface.co/rhasspy/piper-voices
- **Sherpa-ONNX:** https://github.com/k2-fsa/sherpa-onnx
- **NAudio:** https://github.com/naudio/NAudio
- **Stream Deck SDK:** https://developer.elgato.com/documentation/stream-deck/

---

## Coding Standards

- **Language:** C# (.NET 8) + TypeScript (Stream Deck plugin)
- **Naming:** `PascalCase` for classes/methods, `camelCase` for properties/variables
- **Nullable:** Enabled; use `!` sparingly, prefer `??` for defaults
- **Async:** Prefer `async/await`, but use `Task.Run()` for long-running synthesis
- **Logging:** Use `ILogger` from DI; avoid console output in production code
- **Comments:** Explain "why", not "what" — code should be self-documenting
