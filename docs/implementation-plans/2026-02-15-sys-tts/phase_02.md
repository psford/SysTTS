# SysTTS Implementation Plan - Phase 2

**Goal:** Sherpa-ONNX loads Piper ONNX voice models and synthesizes text to audio bytes, with NAudio playback.

**Architecture:** VoiceManager scans `voices/` directory for `.onnx` + `.onnx.json` pairs, catalogs metadata, and watches for changes via FileSystemWatcher. TtsEngine wraps Sherpa-ONNX with lazy-loaded per-voice `OfflineTts` instances. AudioPlayer converts float32 samples to int16 PCM and plays via NAudio `WaveOutEvent`.

**Tech Stack:** Sherpa-ONNX (org.k2fsa.sherpa.onnx v1.12.23), NAudio v2.2.1, Piper ONNX voice models, xUnit + FluentAssertions for tests

**Scope:** 7 phases from original design (phase 2 of 7)

**Codebase verified:** 2026-02-15

---

## Acceptance Criteria Coverage

This phase implements and tests:

### sys-tts.AC4: Voice models load from voices/ directory — adding a voice requires only dropping in an ONNX file and updating config
- **sys-tts.AC4.1 Success:** `GET /api/voices` returns list of all `.onnx` + `.onnx.json` pairs found in `voices/` directory
- **sys-tts.AC4.2 Success:** Dropping a new ONNX model pair into `voices/` while running makes it available without restart (FileSystemWatcher)
- **sys-tts.AC4.3 Success:** Voice models are loaded lazily on first use and cached in memory for subsequent requests
- **sys-tts.AC4.4 Failure:** Source config referencing a missing voice falls back to `DefaultVoice` with a logged warning
- **sys-tts.AC4.5 Edge:** `voices/` directory with zero valid models — service starts, endpoints return empty list, speak requests log error

---

<!-- START_TASK_1 -->
### Task 1: Add NuGet dependencies

**Files:**
- Modify: `src/SysTTS/SysTTS.csproj`
- Create: `.gitignore`

**Step 1: Add package references to .csproj**

Add to the existing `<ItemGroup>` (or create new one) in `src/SysTTS/SysTTS.csproj`:

```xml
<PackageReference Include="org.k2fsa.sherpa.onnx" Version="1.12.23" />
<PackageReference Include="org.k2fsa.sherpa.onnx.runtime.win-x64" Version="1.12.3" />
<PackageReference Include="NAudio" Version="2.2.1" />
```

**Note on Sherpa-ONNX versions:** The managed wrapper (`org.k2fsa.sherpa.onnx`) and the native runtime (`org.k2fsa.sherpa.onnx.runtime.win-x64`) use independent version numbers. Version 1.12.23 (managed) paired with 1.12.3 (runtime) is the correct pairing per NuGet dependency resolution — the managed package declares its required runtime version range. Verify at restore time that `dotnet restore` resolves without version conflicts.

**Step 2: Create .gitignore at repo root**

```
# Voice models (large binary files)
voices/
espeak-ng-data/

# Build output
bin/
obj/
```

**Step 3: Verify**

Run: `dotnet restore src/SysTTS/SysTTS.csproj`
Expected: Restores without errors. No version conflict warnings for Sherpa-ONNX packages.

**Step 4: Commit**

```bash
git add src/SysTTS/SysTTS.csproj .gitignore
git commit -m "chore: add Sherpa-ONNX and NAudio dependencies"
```
<!-- END_TASK_1 -->

<!-- START_SUBCOMPONENT_A (tasks 2-3) -->
<!-- START_TASK_2 -->
### Task 2: Create VoiceInfo model and VoiceManager service

**Verifies:** sys-tts.AC4.1, sys-tts.AC4.2, sys-tts.AC4.3, sys-tts.AC4.4, sys-tts.AC4.5

**Files:**
- Create: `src/SysTTS/Models/VoiceInfo.cs`
- Create: `src/SysTTS/Services/IVoiceManager.cs`
- Create: `src/SysTTS/Services/VoiceManager.cs`

**Implementation:**

`VoiceInfo` — simple record holding voice metadata:

```csharp
namespace SysTTS.Models;

public record VoiceInfo(string Id, string Name, string ModelPath, string ConfigPath, int SampleRate);
```

`IVoiceManager` — interface for voice catalog management:

```csharp
using SysTTS.Models;

namespace SysTTS.Services;

public interface IVoiceManager : IDisposable
{
    IReadOnlyList<VoiceInfo> GetAvailableVoices();
    VoiceInfo? GetVoice(string voiceId);
    string ResolveVoiceId(string? requestedVoiceId);
}
```

`VoiceManager` — scans `VoicesPath` directory for `.onnx` files with matching `.onnx.json` files. Reads the JSON to extract `audio.sample_rate`. `FileSystemWatcher` monitors for file create/delete and re-scans. `ResolveVoiceId` falls back to `DefaultVoice` when requested voice missing, logs warning. Empty directory returns empty list.

Key behaviors:
- Voice ID derived from filename without extension (e.g., `en_US-amy-medium.onnx` → `en_US-amy-medium`)
- Constructor takes `IOptions<ServiceSettings>` and `ILogger<VoiceManager>`
- Thread-safe catalog access via `lock` or `ReaderWriterLockSlim`
- FileSystemWatcher debounced (100ms) to avoid rapid re-scans
- `ResolveVoiceId(null)` returns `DefaultVoice`
- `ResolveVoiceId("missing-voice")` logs warning, returns `DefaultVoice`
- `GetAvailableVoices()` with empty directory returns empty `List<VoiceInfo>`

**Verification:**

Run: `dotnet build src/SysTTS/SysTTS.csproj`
Expected: Build succeeds.

**Commit:**

```bash
git add src/SysTTS/Models/ src/SysTTS/Services/
git commit -m "feat: add VoiceManager with directory scanning and FileSystemWatcher"
```
<!-- END_TASK_2 -->

<!-- START_TASK_3 -->
### Task 3: Create VoiceManager tests

**Verifies:** sys-tts.AC4.1, sys-tts.AC4.2, sys-tts.AC4.4, sys-tts.AC4.5

**Files:**
- Create: `tests/SysTTS.Tests/SysTTS.Tests.csproj`
- Create: `tests/SysTTS.Tests/Services/VoiceManagerTests.cs`

**Implementation:**

Create test project with xUnit + FluentAssertions + Moq (following stock-analyzer patterns at `C:/Users/patri/Documents/claudeProjects/projects/stock-analyzer/tests/StockAnalyzer.Core.Tests/`). Tests use temporary directories with fake `.onnx` and `.onnx.json` files — no actual Sherpa-ONNX synthesis needed for scanning/metadata/fallback logic.

**Testing:**
Tests must verify each AC listed above:
- sys-tts.AC4.1: `GetAvailableVoices_WithValidPairs_ReturnsList` — temp dir with `.onnx` + `.onnx.json` pairs returns correct VoiceInfo list
- sys-tts.AC4.2: `FileSystemWatcher_NewModelDropped_BecomesAvailable` — create model pair after init, verify it appears in catalog (with short delay for watcher)
- sys-tts.AC4.4: `ResolveVoiceId_MissingVoice_FallsBackToDefault` — requested voice missing, returns DefaultVoice ID
- sys-tts.AC4.5: `GetAvailableVoices_EmptyDirectory_ReturnsEmptyList` — empty voices dir returns empty list

Additional test cases:
- `GetAvailableVoices_OnnxWithoutJson_IgnoresModel` — orphan `.onnx` without matching `.json` excluded
- `ResolveVoiceId_ExistingVoice_ReturnsRequestedId` — valid voice ID returned as-is
- `ResolveVoiceId_NullInput_ReturnsDefault` — null voice ID returns DefaultVoice
- `GetVoice_ExistingId_ReturnsVoiceInfo` — found voice returns full VoiceInfo
- `GetVoice_MissingId_ReturnsNull` — missing voice returns null

Follow project testing patterns: `[Fact]`, AAA pattern, `MethodName_Condition_Expected` naming. Task-implementor generates actual test code at execution time.

**Step 1: Create test project**

```bash
dotnet new xunit -o tests/SysTTS.Tests --framework net8.0
dotnet sln add tests/SysTTS.Tests/SysTTS.Tests.csproj
dotnet add tests/SysTTS.Tests/SysTTS.Tests.csproj reference src/SysTTS/SysTTS.csproj
dotnet add tests/SysTTS.Tests/SysTTS.Tests.csproj package FluentAssertions --version 6.12.2
dotnet add tests/SysTTS.Tests/SysTTS.Tests.csproj package Moq --version 4.20.72
```

**Step 2: Write tests, verify they pass**

Run: `dotnet test tests/SysTTS.Tests/`
Expected: All tests pass.

**Step 3: Commit**

```bash
git add tests/ SysTTS.sln
git commit -m "test: add VoiceManager tests for scanning, fallback, and hot-reload"
```
<!-- END_TASK_3 -->
<!-- END_SUBCOMPONENT_A -->

<!-- START_SUBCOMPONENT_B (tasks 4-5) -->
<!-- START_TASK_4 -->
### Task 4: Create TtsEngine service

**Verifies:** sys-tts.AC4.3

**Files:**
- Create: `src/SysTTS/Services/ITtsEngine.cs`
- Create: `src/SysTTS/Services/TtsEngine.cs`
- Modify: `src/SysTTS/Settings/ServiceSettings.cs` (add EspeakDataPath)

**Implementation:**

Add `EspeakDataPath` property to `ServiceSettings`:

```csharp
public string EspeakDataPath { get; set; } = "espeak-ng-data";
```

`ITtsEngine` — interface for text-to-speech synthesis:

```csharp
namespace SysTTS.Services;

public interface ITtsEngine : IDisposable
{
    (float[] Samples, int SampleRate) Synthesize(string text, string voiceId, float speed = 1.0f);
}
```

`TtsEngine` — wraps Sherpa-ONNX. Maintains a `ConcurrentDictionary<string, OfflineTts>` for lazy-loaded instances (one per voice model, AC4.3). Uses a `SemaphoreSlim` per instance to serialize synthesis calls (Sherpa-ONNX is not thread-safe per instance).

Key behaviors:
- Constructor takes `IVoiceManager`, `IOptions<ServiceSettings>`, `ILogger<TtsEngine>`
- First call for a voice ID creates `OfflineTts` with `OfflineTtsConfig`:
  - `config.Model.Piper.Model` = voice's ModelPath
  - `config.Model.Piper.DataDir` = ServiceSettings.EspeakDataPath
  - `config.Model.NumThreads` = 2
  - `config.Model.Provider` = "cpu"
- Subsequent calls reuse cached instance
- Returns `(float[] Samples, int SampleRate)` tuple from `GeneratedAudio`
- To support unit testing: extract the `OfflineTts` creation into a `protected virtual` method (e.g., `CreateTtsInstance(VoiceInfo voice)`) so tests can subclass and verify caching without needing real Sherpa-ONNX models
- `Dispose()` disposes all cached `OfflineTts` instances

**Verification:**

Run: `dotnet build src/SysTTS/SysTTS.csproj`
Expected: Build succeeds.

**Commit:** `feat: add TtsEngine with lazy-loaded Sherpa-ONNX instances`
<!-- END_TASK_4 -->

<!-- START_TASK_5 -->
### Task 5: Create TtsEngine tests

**Verifies:** sys-tts.AC4.3

**Files:**
- Create: `tests/SysTTS.Tests/Services/TtsEngineTests.cs`

**Testing:**

Tests verify lazy-loading and caching behavior (AC4.3) by subclassing `TtsEngine` and overriding the `CreateTtsInstance` factory method with a mock/fake that tracks calls. No real Sherpa-ONNX models needed.

Test cases:
- sys-tts.AC4.3: `Synthesize_FirstCallForVoice_CreatesNewInstance` — first call for a voice ID triggers factory method once
- sys-tts.AC4.3: `Synthesize_SecondCallSameVoice_ReusesInstance` — second call for same voice ID does NOT trigger factory method again (cached)
- `Synthesize_DifferentVoice_CreatesSeparateInstance` — call for a different voice ID triggers factory for the new voice
- `Dispose_DisposesAllCachedInstances` — all created instances are disposed when TtsEngine is disposed

Follow project testing patterns: `[Fact]`, AAA pattern, `MethodName_Condition_Expected` naming. Task-implementor generates actual test code at execution time.

**Verification:**

Run: `dotnet test tests/SysTTS.Tests/`
Expected: All tests pass.

**Commit:**

```bash
git add tests/SysTTS.Tests/Services/TtsEngineTests.cs
git commit -m "test: add TtsEngine tests for lazy-loading and caching behavior"
```
<!-- END_TASK_5 -->
<!-- END_SUBCOMPONENT_B -->

<!-- START_SUBCOMPONENT_C (tasks 6-7) -->
<!-- START_TASK_6 -->
### Task 6: Create AudioPlayer service

**Files:**
- Create: `src/SysTTS/Services/IAudioPlayer.cs`
- Create: `src/SysTTS/Services/AudioPlayer.cs`

**Implementation:**

`IAudioPlayer` — interface for audio playback:

```csharp
namespace SysTTS.Services;

public interface IAudioPlayer
{
    Task PlayAsync(float[] samples, int sampleRate, CancellationToken cancellationToken = default);
    void Stop();
}
```

`AudioPlayer` — plays synthesized audio through default audio device using NAudio `WaveOutEvent`.

Key behaviors:
- Converts float32 samples `[-1.0, 1.0]` to int16 PCM bytes via a `public static` conversion method `ConvertFloat32ToInt16Pcm(float[] samples)` — this enables direct unit testing of the conversion logic without needing audio hardware
- The conversion: clamp each sample to `[-1.0, 1.0]` → scale by `Int16.MaxValue` → cast to `short` → `BitConverter.GetBytes`
- Creates `WaveFormat(sampleRate, 16, 1)` (16-bit mono PCM)
- Creates `MemoryStream` from PCM bytes → `RawSourceWaveStream` → `WaveOutEvent`
- Uses `TaskCompletionSource` to await `PlaybackStopped` event
- `CancellationToken` registration calls `Stop()` on cancellation
- `Stop()` calls `WaveOutEvent.Stop()` which triggers `PlaybackStopped` event
- Disposes `WaveOutEvent`, `RawSourceWaveStream`, `MemoryStream` in `PlaybackStopped` handler
- Constructor takes `ILogger<AudioPlayer>`

**Verification:**

Run: `dotnet build src/SysTTS/SysTTS.csproj`
Expected: Build succeeds.

**Commit:** `feat: add AudioPlayer with NAudio WaveOutEvent playback`
<!-- END_TASK_6 -->

<!-- START_TASK_7 -->
### Task 7: Create AudioPlayer conversion tests

**Files:**
- Create: `tests/SysTTS.Tests/Services/AudioPlayerTests.cs`

**Testing:**

Tests verify the float32→int16 PCM conversion logic directly via the `public static ConvertFloat32ToInt16Pcm` method. No audio device or NAudio playback needed — pure math tests.

Test cases:
- `ConvertFloat32ToInt16Pcm_PositiveOne_ProducesMaxInt16` — sample value `1.0f` converts to `Int16.MaxValue` (32767)
- `ConvertFloat32ToInt16Pcm_NegativeOne_ProducesMinInt16` — sample value `-1.0f` converts to `Int16.MinValue` (-32768)
- `ConvertFloat32ToInt16Pcm_Zero_ProducesZero` — sample value `0.0f` converts to `0`
- `ConvertFloat32ToInt16Pcm_ClampAboveOne_ProducesMaxInt16` — sample value `1.5f` (out of range) clamps to `Int16.MaxValue`
- `ConvertFloat32ToInt16Pcm_ClampBelowNegativeOne_ProducesMinInt16` — sample value `-1.5f` (out of range) clamps to `Int16.MinValue`
- `ConvertFloat32ToInt16Pcm_MultipleSamples_ProducesCorrectByteArray` — array of mixed values produces correct byte sequence (2 bytes per sample, little-endian)

Follow project testing patterns: `[Fact]`, AAA pattern, `MethodName_Condition_Expected` naming. Task-implementor generates actual test code at execution time.

**Verification:**

Run: `dotnet test tests/SysTTS.Tests/`
Expected: All tests pass.

**Commit:**

```bash
git add tests/SysTTS.Tests/Services/AudioPlayerTests.cs
git commit -m "test: add AudioPlayer conversion tests for float32 to int16 PCM"
```
<!-- END_TASK_7 -->
<!-- END_SUBCOMPONENT_C -->

<!-- START_TASK_8 -->
### Task 8: Register services and add GET /api/voices endpoint

**Files:**
- Modify: `src/SysTTS/Program.cs`
- Modify: `src/SysTTS/appsettings.json`

**Implementation:**

Register VoiceManager, TtsEngine, and AudioPlayer as singletons in DI container. Add `GET /api/voices` endpoint. Update `/api/status` to include real voice count.

Add to `Program.cs` after settings binding:

```csharp
builder.Services.AddSingleton<IVoiceManager, VoiceManager>();
builder.Services.AddSingleton<ITtsEngine, TtsEngine>();
builder.Services.AddSingleton<IAudioPlayer, AudioPlayer>();
```

Add endpoint:

```csharp
app.MapGet("/api/voices", (IVoiceManager voiceManager) =>
    Results.Ok(voiceManager.GetAvailableVoices().Select(v => new { v.Id, v.Name, v.SampleRate })));
```

Update status endpoint to use real voice count:

```csharp
app.MapGet("/api/status", (IVoiceManager voiceManager) => Results.Ok(new
{
    running = true,
    activeVoices = voiceManager.GetAvailableVoices().Count,
    queueDepth = 0
}));
```

Add to `appsettings.json` Service section:

```json
"EspeakDataPath": "espeak-ng-data"
```

**Verification:**

Run: `dotnet build src/SysTTS/SysTTS.csproj`
Expected: Build succeeds.

**Commit:** `feat: register TTS services and add /api/voices endpoint`
<!-- END_TASK_8 -->

<!-- START_TASK_9 -->
### Task 9: Download pre-built Piper voice and espeak-ng-data

**Files:**
- Create: `scripts/download-models.ps1`

**Implementation:**

PowerShell script that downloads the default Piper voice model and espeak-ng-data directory. These are large binary files gitignored from the repo.

Downloads:
1. `en_US-amy-medium.onnx` (~45MB) from `https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/amy/medium/en_US-amy-medium.onnx`
2. `en_US-amy-medium.onnx.json` from `https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/amy/medium/en_US-amy-medium.onnx.json`
3. `espeak-ng-data` from `https://github.com/k2-fsa/sherpa-onnx/releases/download/tts-models/espeak-ng-data.tar.bz2`

Script creates `voices/` and `espeak-ng-data/` directories relative to project root. Uses `Invoke-WebRequest` for downloads. Extracts tar.bz2 using `tar` (available on Windows 10+).

**Verification:**

Run: `powershell.exe -File scripts/download-models.ps1`
Expected: `voices/en_US-amy-medium.onnx`, `voices/en_US-amy-medium.onnx.json`, and `espeak-ng-data/` directory exist.

**Commit:** `chore: add model download script for Piper voices`
<!-- END_TASK_9 -->

<!-- START_TASK_10 -->
### Task 10: Operational verification

**Step 1: Ensure models are downloaded**

Run: `powershell.exe -File scripts/download-models.ps1`

**Step 2: Start the application**

Run: `dotnet run --project src/SysTTS/SysTTS.csproj`

**Step 3: Verify voices endpoint (sys-tts.AC4.1)**

Run: `curl http://localhost:5100/api/voices`
Expected: JSON array with `en_US-amy-medium` entry including `id`, `name`, `sampleRate`.

**Step 4: Verify status endpoint shows voice count**

Run: `curl http://localhost:5100/api/status`
Expected: `{"running":true,"activeVoices":1,"queueDepth":0}`

**Step 5: Run all tests**

Run: `dotnet test tests/SysTTS.Tests/`
Expected: All tests pass (VoiceManager, TtsEngine, AudioPlayer).

**Step 6: Verify all changes committed**

Run: `git status`
Expected: Working tree clean — all phase changes committed in prior tasks.
<!-- END_TASK_10 -->
