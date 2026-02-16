# SysTTS Implementation Plan - Phase 7

**Goal:** User-facing documentation for setup, usage, custom voice import, and developer integration.

**Architecture:** N/A — documentation only phase.

**Tech Stack:** Markdown

**Scope:** 7 phases from original design (phase 7 of 7)

**Codebase verified:** 2026-02-15

---

## Acceptance Criteria Coverage

**Verifies: None** — This is a documentation phase. All ACs are verified operationally by the documents being complete and accurate.

---

<!-- START_TASK_1 -->
### Task 1: Create README.md

**Files:**
- Create: `README.md`

**Implementation:**

Project overview and quick start guide. Sections:

1. **What is SysTTS** — 1-2 paragraph description: Windows TTS service with system tray, HTTP API, global hotkeys, Stream Deck support, powered by Piper neural voices
2. **Prerequisites** — .NET 8 SDK, Windows 10/11, optional: Stream Deck + Elgato software
3. **Quick Start:**
   - Clone repository
   - Download voice models: `powershell.exe -File scripts/download-models.ps1`
   - Configure: edit `appsettings.json` (port, default voice, hotkeys, sources)
   - Run: `dotnet run --project src/SysTTS/SysTTS.csproj`
   - Verify: `curl http://localhost:5100/api/status`
4. **Input Modes:**
   - HTTP API: programmatic text-to-speech via POST endpoints
   - Global Hotkeys: select text anywhere, press hotkey to speak
   - Stream Deck: button-based TTS control with voice selection
5. **Configuration Reference:** Brief table of `Service`, `Sources`, `Hotkeys`, `Audio` sections with key properties
6. **Available Voices:** Link to rhasspy/piper-voices on HuggingFace, note about model quality levels
7. **Further Reading:** Links to CUSTOM_VOICES.md, INTEGRATION.md, TECHNICAL_SPEC.md

**Verification:**

Read through for completeness and accuracy against implemented features.

**Commit:** `docs: add README with quick start and configuration reference`
<!-- END_TASK_1 -->

<!-- START_TASK_2 -->
### Task 2: Create docs/CUSTOM_VOICES.md

**Files:**
- Create: `docs/CUSTOM_VOICES.md`

**Implementation:**

Step-by-step guide for adding voices to SysTTS. Sections:

1. **Piper Model Format** — what files make up a voice: `.onnx` model file, `.onnx.json` config (sample rate, phoneme type), shared `espeak-ng-data/` directory
2. **Downloading Pre-Built Voices:**
   - Browse [rhasspy/piper-voices](https://huggingface.co/rhasspy/piper-voices) on HuggingFace
   - Quality levels: low (~15MB), medium (~45MB), high (~75MB)
   - Download steps: download both `.onnx` and `.onnx.json`, drop into `voices/`
   - Automatic detection via FileSystemWatcher — no restart needed
3. **Custom Training Workflows:**
   - GPT-SoVITS: overview of training setup, data preparation (audio clips + transcripts), training process, ONNX export steps, converting to Piper-compatible format
   - Piper Fine-Tuning: Piper's native training pipeline, dataset format (LJSpeech), training commands, checkpoint export to ONNX
   - Note: training is outside SysTTS scope — these are external tools
4. **Importing a Custom Voice:**
   - Place `.onnx` + `.onnx.json` in `voices/` directory
   - Verify `.onnx.json` has correct `audio.sample_rate` and `espeak.voice` fields
   - Update `appsettings.json` source mappings to reference new voice ID (filename without extension)
   - Voice available immediately (FileSystemWatcher) — config changes require restart
5. **Troubleshooting:**
   - Model not appearing: check file pair exists, JSON is valid
   - Wrong pitch/speed: verify sample_rate matches model's actual output rate
   - Garbled audio: ensure espeak-ng-data directory is present and path is correct in config
   - Model loading error: check ONNX file isn't corrupted, verify Sherpa-ONNX compatibility

**Verification:**

Read through for completeness. Verify download URLs are correct.

**Commit:** `docs: add custom voice training and import guide`
<!-- END_TASK_2 -->

<!-- START_TASK_3 -->
### Task 3: Create docs/INTEGRATION.md

**Files:**
- Create: `docs/INTEGRATION.md`

**Implementation:**

How to integrate other apps with SysTTS HTTP API. Sections:

1. **API Endpoint Reference:**
   - `POST /api/speak` — request: `{ text, source?, voice? }`, response: 202 `{ queued, id }`
   - `POST /api/speak-selection` — request: `{ voice? }`, response: 202 `{ queued, id, text }`
   - `GET /api/voices` — response: 200 `[{ id, name, sampleRate }]`
   - `GET /api/status` — response: 200 `{ running, activeVoices, queueDepth }`
   - `POST /api/stop` — response: 200 `{ stopped }`
2. **Per-Source Configuration:**
   - How `source` field maps to `Sources` section in config
   - Voice mapping: source-level default, per-request override
   - Regex filters: patterns array, null for unfiltered, match behavior
   - Priority: lower number = higher priority, interrupt behavior
3. **Integration Examples:**
   - JavaScript (browser): fire-and-forget `fetch()` with `.catch(() => {})` (T-Tracker pattern)
   - Python: `requests.post()` with timeout and exception handling
   - PowerShell: `Invoke-RestMethod` one-liner
   - C#: `HttpClient.PostAsJsonAsync()` with fire-and-forget
   - curl: command-line examples for testing
4. **Graceful Degradation:**
   - SysTTS is optional — client apps must not depend on it being available
   - Pattern: catch network errors, log/ignore, continue normal operation
   - Example: T-Tracker's `.catch(() => {})` approach
5. **Stream Deck Plugin:**
   - Installation: `streamdeck link` for dev, `.streamDeckPlugin` for distribution
   - Actions: Speak Selection, Speak Text, Stop Speaking
   - Voice configuration via Property Inspector dropdown

**Verification:**

Verify all curl examples against actual API contract. Test example code snippets compile/run.

**Commit:** `docs: add integration guide with API reference and examples`
<!-- END_TASK_3 -->

<!-- START_TASK_4 -->
### Task 4: Create CLAUDE.md for SysTTS project

**Files:**
- Create: `CLAUDE.md` (at SysTTS repo root)

**Implementation:**

Claude Code project conventions for SysTTS development sessions:

1. **Project Overview** — SysTTS: system-level TTS service with tray icon, HTTP API, hotkeys, Stream Deck
2. **Project Structure:**
   - `src/SysTTS/` — main WinForms application
   - `tests/SysTTS.Tests/` — xUnit test project
   - `streamdeck-plugin/` — Stream Deck plugin (TypeScript/Node.js)
   - `voices/` — Piper ONNX voice models (gitignored)
   - `espeak-ng-data/` — shared phonemization data (gitignored)
   - `scripts/` — utility scripts (download-models.ps1)
   - `docs/` — documentation
3. **Build & Run:**
   - Build: `dotnet build src/SysTTS/SysTTS.csproj`
   - Test: `dotnet test tests/SysTTS.Tests/`
   - Run: `dotnet run --project src/SysTTS/SysTTS.csproj`
   - Download models: `powershell.exe -File scripts/download-models.ps1`
   - Stream Deck: `cd streamdeck-plugin && npm run build`
4. **Testing:** xUnit + Moq + FluentAssertions, AAA pattern, `MethodName_Condition_Expected` naming
5. **Configuration:**
   - `appsettings.json` — static config (port, voices, sources, hotkeys)
   - `user-preferences.json` — runtime preferences (last-used voice, gitignored)
6. **Key Dependencies:** Sherpa-ONNX (TTS), NAudio (audio), ASP.NET Core/Kestrel (HTTP), WinForms (UI/tray)
7. **Architecture Notes:**
   - WinForms on main STA thread, Kestrel on background threads
   - Speech queue serializes playback — no audio collision
   - Win32 keyboard hooks require message pump (provided by WinForms)
   - Sherpa-ONNX not thread-safe per instance — TtsEngine serializes synthesis

**Verification:**

Read through for accuracy against actual project structure.

**Commit:** `docs: add CLAUDE.md project conventions`
<!-- END_TASK_4 -->

<!-- START_TASK_5 -->
### Task 5: Create docs/TECHNICAL_SPEC.md

**Files:**
- Create: `docs/TECHNICAL_SPEC.md`

**Implementation:**

Architecture and technical reference document. Sections:

1. **System Architecture** — ASCII diagram (from design plan) showing components and data flow
2. **Data Flow** — step-by-step for each input mode:
   - HTTP mode: POST → SpeechService (filter/resolve) → SpeechQueue → TtsEngine → AudioPlayer
   - Hotkey direct mode: keyboard hook → ClipboardService → SpeechService → queue → play
   - Hotkey picker mode: hook → clipboard → VoicePickerForm → SpeechService → queue → play
   - Stream Deck: button → plugin HTTP POST → endpoint → queue → play
3. **Component Catalog:**
   - `Program.cs` — entry point, host builder, endpoint registration
   - `TrayApplicationContext` — system tray icon, quit handling
   - `VoiceManager` — voice directory scanning, FileSystemWatcher, metadata catalog
   - `TtsEngine` — Sherpa-ONNX wrapper, lazy-loaded per-voice instances
   - `AudioPlayer` — NAudio WaveOutEvent wrapper, float32 → int16 PCM
   - `SpeechQueue` — priority queue, serial processing, interrupt behavior
   - `SpeechService` — source filtering, voice resolution, request orchestration
   - `HotkeyService` — Win32 keyboard hook, hotkey dispatch
   - `ClipboardService` — clipboard save/restore, Ctrl+C simulation
   - `VoicePickerForm` — voice selection popup
   - `UserPreferences` — runtime preferences persistence
4. **Configuration Contract** — full `appsettings.json` schema with property descriptions and defaults
5. **HTTP API Contract** — detailed endpoint specifications (request/response schemas, status codes, behavior)
6. **Speech Queue Behavior** — priority ordering, interrupt logic, max depth eviction
7. **Voice Manager Lifecycle** — startup scan, lazy loading, caching, FileSystemWatcher events
8. **Dependencies Table** — NuGet packages with versions, purpose
9. **Thread Model** — STA main thread (WinForms + clipboard), Kestrel thread pool, synthesis background tasks
10. **Version History** — changelog

**Verification:**

Cross-reference with implementation for accuracy. Verify all component names and API contracts match code.

**Commit:** `docs: add technical specification`
<!-- END_TASK_5 -->

<!-- START_TASK_6 -->
### Task 6: Final commit

**Step 1: Verify all documentation is complete**

Check that all files exist:
- `README.md`
- `CLAUDE.md`
- `docs/CUSTOM_VOICES.md`
- `docs/INTEGRATION.md`
- `docs/TECHNICAL_SPEC.md`

**Step 2: Verify all changes committed**

Run: `git status`
Expected: Working tree clean — all phase changes committed in prior tasks.
<!-- END_TASK_6 -->
