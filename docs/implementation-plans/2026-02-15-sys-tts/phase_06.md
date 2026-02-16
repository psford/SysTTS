# SysTTS Implementation Plan - Phase 6

**Goal:** Stream Deck (and other external tools) can trigger speak-selected-text via HTTP, with a dedicated Stream Deck plugin for button-based TTS control.

**Architecture:** `POST /api/speak-selection` endpoint performs clipboard capture server-side via ClipboardService and queues speech. Stream Deck plugin (separate TypeScript/Node.js project) provides three button actions that call SysTTS HTTP endpoints, with a Property Inspector UI that populates voice dropdowns from `GET /api/voices`.

**Tech Stack:** ASP.NET Core Minimal APIs (endpoint), Stream Deck SDK (@elgato/streamdeck v2+, Node.js v20+, TypeScript, Rollup)

**Scope:** 7 phases from original design (phase 6 of 7)

**Codebase verified:** 2026-02-15

---

## Acceptance Criteria Coverage

This phase implements:

### sys-tts.AC3: Global hotkeys capture selected text and speak (speak-selection endpoint)
- **sys-tts.AC3.8 Success:** `POST /api/speak-selection` performs clipboard capture in the service and speaks with specified or default voice

### sys-tts.AC6: Cross-Cutting Behaviors
- **sys-tts.AC6.3:** Stream Deck plugin populates voice dropdown from `GET /api/voices` and triggers speech via HTTP endpoints

---

<!-- START_TASK_1 -->
### Task 1: Add POST /api/speak-selection endpoint

**Verifies:** sys-tts.AC3.8

**Files:**
- Modify: `src/SysTTS/Program.cs`
- Create: `src/SysTTS/Models/SpeakSelectionRequestDto.cs`

**Implementation:**

`SpeakSelectionRequestDto`:

```csharp
namespace SysTTS.Models;

public record SpeakSelectionRequestDto(string? Voice);
```

Add `POST /api/speak-selection` endpoint to `Program.cs`:
- Calls `IClipboardService.CaptureSelectedTextAsync()` to capture currently selected text via clipboard
- If no text captured (null/empty), returns 200 with `{ queued: false, text: "" }`
- If text captured, calls `ISpeechService.ProcessSpeakRequest(text, "speak-selection", request.Voice)`
- Returns 202 with `{ queued: true/false, id: string?, text: string }`

```csharp
app.MapPost("/api/speak-selection", async (SpeakSelectionRequestDto request, IClipboardService clipboard, ISpeechService speechService) =>
{
    var text = await clipboard.CaptureSelectedTextAsync();
    if (string.IsNullOrWhiteSpace(text))
        return Results.Ok(new { queued = false, text = "" });

    var (queued, id) = speechService.ProcessSpeakRequest(text, "speak-selection", request.Voice);
    return Results.Accepted(value: new { queued, id, text });
});
```

**Verification:**

Run: `dotnet build src/SysTTS/SysTTS.csproj`
Expected: Build succeeds.

**Commit:** `feat: add POST /api/speak-selection endpoint`
<!-- END_TASK_1 -->

<!-- START_TASK_2 -->
### Task 2: Create Stream Deck plugin scaffolding

**Verifies:** sys-tts.AC6.3

**Files:**
- Create: `streamdeck-plugin/package.json`
- Create: `streamdeck-plugin/tsconfig.json`
- Create: `streamdeck-plugin/rollup.config.mjs`
- Create: `streamdeck-plugin/com.systts.sdPlugin/manifest.json`
- Create: `streamdeck-plugin/com.systts.sdPlugin/images/` (placeholder icon PNGs)
- Create: `streamdeck-plugin/.sdignore`

**Implementation:**

Separate `streamdeck-plugin/` directory at repo root. TypeScript project using `@elgato/streamdeck` SDK with rollup build.

`package.json`:
- Dependencies: `@elgato/streamdeck`
- DevDependencies: `typescript`, `@rollup/plugin-typescript`, `rollup`, `@rollup/plugin-node-resolve`
- Scripts: `build`, `watch`

`manifest.json` with `SDKVersion: 3`, defines 3 actions:
- `com.systts.plugin.speak-selection` — "Speak Selected Text" (has PropertyInspector)
- `com.systts.plugin.speak-text` — "Speak Custom Text" (has PropertyInspector)
- `com.systts.plugin.stop-speaking` — "Stop Speaking" (no settings)

All actions: `"Controllers": ["Keypad"]`, `CodePath: "bin/index.js"`.

Placeholder icons: simple PNGs in `images/` directories (20x20 @1x, 40x40 @2x per action + 256x256 plugin icon).

**Verification:**

```bash
cd streamdeck-plugin && npm install
```
Expected: Installs without errors.

**Commit:** `chore: scaffold Stream Deck plugin project`
<!-- END_TASK_2 -->

<!-- START_TASK_3 -->
### Task 3: Create action classes and shared API client

**Verifies:** sys-tts.AC6.3

**Files:**
- Create: `streamdeck-plugin/src/index.ts`
- Create: `streamdeck-plugin/src/actions/SpeakSelectionAction.ts`
- Create: `streamdeck-plugin/src/actions/SpeakTextAction.ts`
- Create: `streamdeck-plugin/src/actions/StopSpeakingAction.ts`
- Create: `streamdeck-plugin/src/common/api.ts`

**Implementation:**

`api.ts` — shared HTTP client module:
- `BASE_URL` defaulting to `http://localhost:5100` (port matching SysTTS default)
- `speakSelection(voice?: string)` — POST `/api/speak-selection` with `{ voice }`
- `speakText(text: string, voice?: string)` — POST `/api/speak` with `{ text, voice }`
- `stopSpeaking()` — POST `/api/stop`
- `getVoices()` — GET `/api/voices`, returns `Array<{ id: string, name: string, sampleRate: number }>`
- All requests in try/catch with `streamDeck.logger.error` on failure; fire-and-forget when service unavailable

Action classes (each extends `SingletonAction<Settings>`):

`SpeakSelectionAction`:
- Settings interface: `{ voice?: string }`
- `onKeyDown`: calls `api.speakSelection(settings.voice)`

`SpeakTextAction`:
- Settings interface: `{ text?: string, voice?: string }`
- `onKeyDown`: calls `api.speakText(settings.text, settings.voice)`, logs warning if text not configured

`StopSpeakingAction`:
- No settings
- `onKeyDown`: calls `api.stopSpeaking()`

`index.ts`:
- Import and register all three actions
- Call `streamDeck.connect()`

**Verification:**

```bash
cd streamdeck-plugin && npm run build
```
Expected: Build succeeds, `com.systts.sdPlugin/bin/index.js` created.

**Commit:** `feat: add Stream Deck action classes for speak and stop`
<!-- END_TASK_3 -->

<!-- START_TASK_4 -->
### Task 4: Create Property Inspector UI

**Verifies:** sys-tts.AC6.3

**Files:**
- Create: `streamdeck-plugin/com.systts.sdPlugin/ui/settings.html`
- Create: `streamdeck-plugin/com.systts.sdPlugin/ui/settings.js`
- Create: `streamdeck-plugin/com.systts.sdPlugin/ui/styles.css`

**Implementation:**

`settings.html` — Property Inspector page with:
- Voice dropdown (`<select>`) with initial "Loading..." option
- Text textarea (hidden by default, shown only for SpeakText action)
- Dark theme styling matching Stream Deck aesthetic

`settings.js` — Property Inspector logic:
- `streamDeck.ui.onConnected()`: initialize with current action settings
- `loadVoices()`: fetch `GET http://localhost:5100/api/voices`, populate dropdown, select saved voice
  - On fetch error: show "Service unavailable" option in dropdown
- Detect action UUID to conditionally show text field (`com.systts.plugin.speak-text`)
- Change handlers on dropdown and textarea: save via `streamDeck.ui.setSettings()`

`styles.css` — Dark theme:
- Background: `#333`, text: `#fff`
- Inputs: dark background (`#222`), subtle border (`#555`), blue focus ring
- Font: system sans-serif stack
- Compact layout with labeled sections

**Verification:**

```bash
cd streamdeck-plugin && npm run build
```
Expected: Build succeeds, UI files in `com.systts.sdPlugin/ui/`.

**Commit:** `feat: add Property Inspector with voice dropdown`
<!-- END_TASK_4 -->

<!-- START_TASK_5 -->
### Task 5: Operational verification

**Step 1: Test speak-selection endpoint (sys-tts.AC3.8)**

Start SysTTS: `dotnet run --project src/SysTTS/SysTTS.csproj`

Select text in any editor, then:

Run: `curl -X POST http://localhost:5100/api/speak-selection -H "Content-Type: application/json" -d "{}"`
Expected: Returns 202, captured text is spoken aloud.

Run: `curl -X POST http://localhost:5100/api/speak-selection -H "Content-Type: application/json" -d "{\"voice\":\"en_US-amy-medium\"}"`
Expected: Returns 202, text spoken with specified voice.

**Step 2: Build Stream Deck plugin**

```bash
cd streamdeck-plugin && npm run build
```
Expected: Build succeeds.

**Step 3: Install plugin for development**

```bash
cd streamdeck-plugin && streamdeck link com.systts.sdPlugin
```

(Requires Stream Deck application and `@elgato/cli` installed globally.)

**Step 4: Test Stream Deck integration (sys-tts.AC6.3)**

1. Open Stream Deck app, find "SysTTS" category in action list
2. Drag "Speak Selected Text" to a button:
   - Verify settings panel shows voice dropdown populated from `/api/voices`
   - Select text, press button — verify speech plays
3. Drag "Speak Custom Text" to a button:
   - Verify settings panel shows voice dropdown AND text input
   - Configure static text, press button — verify configured text is spoken
4. Drag "Stop Speaking" to a button:
   - Start long speech (via any method), press stop button — verify speech stops immediately

**Step 5: Run all .NET tests**

Run: `dotnet test tests/SysTTS.Tests/`
Expected: All tests pass (no regressions).

**Step 6: Verify all changes committed**

Run: `git status`
Expected: Working tree clean — all phase changes committed in prior tasks.
<!-- END_TASK_5 -->
