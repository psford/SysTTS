# SysTTS Implementation Plan - Phase 3

**Goal:** HTTP clients can POST text to be spoken with per-source voice mapping, regex filtering, and priority queuing.

**Architecture:** SpeechService orchestrates HTTP requests — resolves voice from per-source config, applies regex filters, enqueues via SpeechQueue. SpeechQueue processes requests serially with priority ordering (lower number = higher priority), optional interrupt-on-higher-priority, and max depth eviction. Pipeline: SpeechService → SpeechQueue → TtsEngine → AudioPlayer.

**Tech Stack:** .NET 8, ASP.NET Core Minimal APIs, System.Text.RegularExpressions, System.Threading.Channels

**Scope:** 7 phases from original design (phase 3 of 7)

**Codebase verified:** 2026-02-15

---

## Acceptance Criteria Coverage

This phase implements and tests:

### sys-tts.AC2: HTTP endpoint accepts text, applies per-source voice/filter config, and speaks
- **sys-tts.AC2.1 Success:** `POST /api/speak` with `{ text, source }` resolves voice from source config and produces audible speech
- **sys-tts.AC2.2 Success:** `POST /api/speak` with explicit `voice` field overrides the source-configured voice
- **sys-tts.AC2.3 Success:** `POST /api/speak` with source that has regex filters silently drops text that matches no filter (returns 202, no speech)
- **sys-tts.AC2.4 Success:** `POST /api/speak` with source that has `filters: null` speaks all text regardless of content
- **sys-tts.AC2.5 Success:** `POST /api/stop` cancels current speech and clears the queue
- **sys-tts.AC2.6 Failure:** `POST /api/speak` with missing `text` field returns 400 error
- **sys-tts.AC2.7 Edge:** Higher-priority request interrupts lower-priority speech currently playing (when `InterruptOnHigherPriority` is true)
- **sys-tts.AC2.8 Edge:** Queue at max depth drops oldest low-priority item when new request arrives

### sys-tts.AC6: Cross-Cutting Behaviors (partial)
- **sys-tts.AC6.1:** Speech queue processes requests serially — no audio collision from concurrent requests

---

<!-- START_TASK_1 -->
### Task 1: Create SourceSettings and SpeechRequest models

**Files:**
- Create: `src/SysTTS/Settings/SourceSettings.cs`
- Create: `src/SysTTS/Models/SpeechRequest.cs`
- Create: `src/SysTTS/Models/SpeakRequestDto.cs`

**Implementation:**

`SourceSettings` — strongly-typed class for per-source configuration from `Sources` section in appsettings.json:

```csharp
namespace SysTTS.Settings;

public class SourceSettings
{
    public string? Voice { get; set; }
    public string[]? Filters { get; set; }
    public int Priority { get; set; } = 3;
}
```

`SpeechRequest` — internal model representing a queued speech request:

```csharp
namespace SysTTS.Models;

public record SpeechRequest(
    string Id,
    string Text,
    string VoiceId,
    int Priority,
    string? Source);
```

`SpeakRequestDto` — HTTP request body DTO:

```csharp
namespace SysTTS.Models;

public record SpeakRequestDto(string? Text, string? Source, string? Voice);
```

**Verification:**

Run: `dotnet build src/SysTTS/SysTTS.csproj`
Expected: Build succeeds.

**Commit:** `feat: add SourceSettings and SpeechRequest models`
<!-- END_TASK_1 -->

<!-- START_SUBCOMPONENT_A (tasks 2-4) -->
<!-- START_TASK_2 -->
### Task 2: Create SpeechQueue service

**Verifies:** sys-tts.AC2.5, sys-tts.AC2.7, sys-tts.AC2.8, sys-tts.AC6.1

**Files:**
- Create: `src/SysTTS/Services/ISpeechQueue.cs`
- Create: `src/SysTTS/Services/SpeechQueue.cs`

**Implementation:**

`ISpeechQueue` interface:

```csharp
using SysTTS.Models;

namespace SysTTS.Services;

public interface ISpeechQueue
{
    string Enqueue(SpeechRequest request);
    Task StopAndClear();
    int QueueDepth { get; }
}
```

`SpeechQueue` processes requests serially — one utterance at a time. Uses a `PriorityQueue<SpeechRequest, int>` for ordering (lower number = higher priority). Runs a background processing loop.

Key behaviors:
- `Enqueue` adds to priority queue, signals processing loop via `SemaphoreSlim` or `Channel`
- Processing loop dequeues highest-priority item, calls `ITtsEngine.Synthesize()` then `IAudioPlayer.PlayAsync()` with a per-request `CancellationTokenSource`
- If `InterruptOnHigherPriority` is true: when new request has lower priority number than currently-playing request, cancel current playback CTS (AC2.7)
- Track current request priority to compare against incoming requests
- Max queue depth from `ServiceSettings.MaxQueueDepth`: when full, remove lowest-priority (highest number) oldest item from queue (AC2.8)
- `StopAndClear()`: cancel current CTS + drain the priority queue (AC2.5)
- Serial processing via single processing task ensures no audio collision (AC6.1)
- Constructor takes `ITtsEngine`, `IAudioPlayer`, `IOptions<ServiceSettings>`, `ILogger<SpeechQueue>`
- Implements `IDisposable` to cancel processing loop on shutdown

**Verification:**

Run: `dotnet build src/SysTTS/SysTTS.csproj`
Expected: Build succeeds.

**Commit:** `feat: add SpeechQueue with priority ordering and serial processing`
<!-- END_TASK_2 -->

<!-- START_TASK_3 -->
### Task 3: Create SpeechService with source filtering and voice resolution

**Verifies:** sys-tts.AC2.1, sys-tts.AC2.2, sys-tts.AC2.3, sys-tts.AC2.4

**Files:**
- Create: `src/SysTTS/Services/ISpeechService.cs`
- Create: `src/SysTTS/Services/SpeechService.cs`

**Implementation:**

`ISpeechService` interface:

```csharp
namespace SysTTS.Services;

public interface ISpeechService
{
    (bool Queued, string? Id) ProcessSpeakRequest(string text, string? source, string? voiceOverride);
}
```

`SpeechService` orchestrates between HTTP requests and the speech queue:

Key behaviors:
- Reads source configuration from `IConfiguration` by looking up `Sources:{sourceName}` section, binding to `SourceSettings`
- If source not found, falls back to `Sources:default` config
- **Filter logic (AC2.3, AC2.4):**
  - If source `Filters` array is non-null: text must match at least one regex pattern (case-insensitive `Regex.IsMatch`). If no match, return `(false, null)` — silently dropped
  - If source `Filters` is null: all text passes through
- **Voice resolution (AC2.1, AC2.2):**
  - If `voiceOverride` is provided and non-empty, use it directly
  - Otherwise use source config `Voice` property
  - Pass resolved voice through `IVoiceManager.ResolveVoiceId()` for fallback handling
- Construct `SpeechRequest` with `Guid.NewGuid().ToString()` as ID, source priority, resolved voice
- Enqueue via `ISpeechQueue.Enqueue()`
- Return `(true, requestId)` if queued, `(false, null)` if filtered
- Constructor takes `ISpeechQueue`, `IVoiceManager`, `IConfiguration`, `ILogger<SpeechService>`

**Verification:**

Run: `dotnet build src/SysTTS/SysTTS.csproj`
Expected: Build succeeds.

**Commit:** `feat: add SpeechService with source filtering and voice resolution`
<!-- END_TASK_3 -->

<!-- START_TASK_4 -->
### Task 4: Create SpeechQueue and SpeechService tests

**Verifies:** sys-tts.AC2.1, sys-tts.AC2.2, sys-tts.AC2.3, sys-tts.AC2.4, sys-tts.AC2.7, sys-tts.AC2.8

**Files:**
- Create: `tests/SysTTS.Tests/Services/SpeechServiceTests.cs`
- Create: `tests/SysTTS.Tests/Services/SpeechQueueTests.cs`

**Testing:**

**SpeechServiceTests** — test source filter logic and voice resolution with mocked `ISpeechQueue`, `IVoiceManager`, `IConfiguration`:
- sys-tts.AC2.1: `ProcessSpeakRequest_WithSource_ResolvesVoiceFromSourceConfig` — uses source config voice
- sys-tts.AC2.2: `ProcessSpeakRequest_WithVoiceOverride_UsesOverride` — explicit voice takes precedence over source config
- sys-tts.AC2.3: `ProcessSpeakRequest_TextNotMatchingFilters_ReturnsFalse` — text "random stuff" with filters ["approaching", "arrived"] returns (false, null)
- sys-tts.AC2.3: `ProcessSpeakRequest_TextMatchingFilter_ReturnsTrue` — text "train approaching" matches "approaching" filter, returns (true, id)
- sys-tts.AC2.4: `ProcessSpeakRequest_NullFilters_SpeaksAllText` — null filters passes any text
- `ProcessSpeakRequest_UnknownSource_FallsBackToDefault` — unknown source name uses "default" config

**SpeechQueueTests** — test priority ordering, max depth, interrupt, and serial processing with mocked `ITtsEngine`, `IAudioPlayer`:
- sys-tts.AC2.7: `Enqueue_HigherPriorityDuringPlayback_InterruptsCurrentSpeech` — verify current playback CancellationToken is cancelled when higher-priority arrives
- sys-tts.AC2.8: `Enqueue_QueueAtMaxDepth_DropsLowestPriorityItem` — when queue is full, lowest priority (highest number) oldest item is evicted
- `Enqueue_MultipleRequests_ProcessesInPriorityOrder` — priority 1 processed before priority 3
- `StopAndClear_CancelsCurrentAndEmptiesQueue` — current playback cancelled, queue emptied

Follow project testing patterns: xUnit `[Fact]`, Moq for mocking interfaces, FluentAssertions, AAA pattern, `MethodName_Condition_Expected` naming.

**Verification:**

Run: `dotnet test tests/SysTTS.Tests/`
Expected: All tests pass.

**Commit:** `test: add SpeechQueue and SpeechService tests`
<!-- END_TASK_4 -->
<!-- END_SUBCOMPONENT_A -->

<!-- START_TASK_5 -->
### Task 5: Add HTTP endpoints and register services

**Verifies:** sys-tts.AC2.5, sys-tts.AC2.6

**Files:**
- Modify: `src/SysTTS/Program.cs`

**Implementation:**

Register services in DI:

```csharp
builder.Services.AddSingleton<ISpeechQueue, SpeechQueue>();
builder.Services.AddSingleton<ISpeechService, SpeechService>();
```

Add `POST /api/speak` endpoint:
- Accepts `SpeakRequestDto` from request body
- Validates `Text` is not null/empty — returns 400 if missing (AC2.6)
- Calls `ISpeechService.ProcessSpeakRequest(text, source, voice)`
- Returns 202 `{ queued: true/false, id: string? }`

```csharp
app.MapPost("/api/speak", (SpeakRequestDto request, ISpeechService speechService) =>
{
    if (string.IsNullOrWhiteSpace(request.Text))
        return Results.BadRequest(new { error = "text is required" });

    var (queued, id) = speechService.ProcessSpeakRequest(request.Text, request.Source, request.Voice);
    return Results.Accepted(value: new { queued, id });
});
```

Add `POST /api/stop` endpoint:
- Calls `ISpeechQueue.StopAndClear()` (AC2.5)
- Returns 200 `{ stopped: true }`

```csharp
app.MapPost("/api/stop", async (ISpeechQueue queue) =>
{
    await queue.StopAndClear();
    return Results.Ok(new { stopped = true });
});
```

Update `/api/status` to include real queue depth:

```csharp
app.MapGet("/api/status", (IVoiceManager voiceManager, ISpeechQueue queue) => Results.Ok(new
{
    running = true,
    activeVoices = voiceManager.GetAvailableVoices().Count,
    queueDepth = queue.QueueDepth
}));
```

**Verification:**

Run: `dotnet build src/SysTTS/SysTTS.csproj`
Expected: Build succeeds.

**Commit:** `feat: add POST /api/speak and /api/stop endpoints`
<!-- END_TASK_5 -->

<!-- START_TASK_6 -->
### Task 6: Operational verification

**Step 1: Start the application**

Run: `dotnet run --project src/SysTTS/SysTTS.csproj`

**Step 2: Test speak endpoint (sys-tts.AC2.1)**

Run: `curl -X POST http://localhost:5100/api/speak -H "Content-Type: application/json" -d "{\"text\":\"Hello, this is a test\",\"source\":\"default\"}"`
Expected: Returns 202, audible speech plays.

**Step 3: Test missing text (sys-tts.AC2.6)**

Run: `curl -X POST http://localhost:5100/api/speak -H "Content-Type: application/json" -d "{\"source\":\"default\"}"`
Expected: Returns 400 with `{"error":"text is required"}`.

**Step 4: Test source filtering (sys-tts.AC2.3)**

Run: `curl -X POST http://localhost:5100/api/speak -H "Content-Type: application/json" -d "{\"text\":\"random unrelated text\",\"source\":\"t-tracker\"}"`
Expected: Returns 202 with `{"queued":false,"id":null}` — no speech (text doesn't match "approaching" or "arrived" filters).

**Step 5: Test source filter match (sys-tts.AC2.3)**

Run: `curl -X POST http://localhost:5100/api/speak -H "Content-Type: application/json" -d "{\"text\":\"Bus approaching Station\",\"source\":\"t-tracker\"}"`
Expected: Returns 202 with `{"queued":true,"id":"..."}`, speech plays.

**Step 6: Test stop endpoint (sys-tts.AC2.5)**

Run: `curl -X POST http://localhost:5100/api/speak -H "Content-Type: application/json" -d "{\"text\":\"This is a longer sentence that takes time to speak fully\",\"source\":\"default\"}"`
Then immediately: `curl -X POST http://localhost:5100/api/stop`
Expected: Speech stops immediately.

**Step 7: Run all tests**

Run: `dotnet test tests/SysTTS.Tests/`
Expected: All tests pass.

**Step 8: Verify all changes committed**

Run: `git status`
Expected: Working tree clean — all phase changes committed in prior tasks.
<!-- END_TASK_6 -->
