<!-- GENERATED FILE — DO NOT EDIT. -->
<!-- Shared rules: claude-env/shared/claude-md/. Project rules: CLAUDE.local.md. -->
<!-- Regenerate: helpers/sync-claude-md.sh <repo> -->


# Git Flow (develop → main)

<!-- Canonical source: claude-env/shared/claude-md/git-flow-develop-main.md. -->
<!-- Branch names are parameterized: develop / main. -->
<!-- Repos that do not follow this flow (e.g. a single-trunk `master` model) should -->
<!-- omit this fragment and document their flow in CLAUDE.local.md. -->

## Critical Git Checkpoints

| Checkpoint | Rule | Enforcement |
|------------|------|-------------|
| **COMMITS** | Show status → diff → log → message → WAIT for explicit approval. A question is NOT approval. | Hook reminds; manual |
| **main BRANCH** | NEVER commit, merge, push --force, or rebase on `main`. | **BLOCKED** |
| **REVERSE MERGE** | NEVER merge `main` INTO `develop` (flow is `develop` → `main` only). | **BLOCKED** |
| **PR MERGE** | Patrick merges via GitHub web only — NEVER use `gh pr merge`. | **BLOCKED** |
| **MERGED PRs** | NEVER edit/push to merged/closed PRs. Always create a NEW PR. | **BLOCKED** |
| **NO RESET --HARD** | NEVER run `git reset --hard` (it destroyed uncommitted work once). Use `git merge`/`git rebase` to sync; `git stash` first if the tree is dirty. | **BLOCKED** |

## Branching Strategy

```
develop (work here) → PR → main (production)
                                  ↑
                           NEVER reverse this
```

- **Feature branches** for: new services, architecture changes, multi-file refactors, big UI changes, multi-session work, 5+ files.
- **Direct on `develop`** for: small fixes, tweaks, internal docs.
- **NEVER** commit directly to `main`, merge to it via CLI, deploy without an explicit "deploy", or click "Update branch" on the GitHub PR page.
- Before branching: `git fetch origin` and check `git log origin/main..develop` — never assume branches are in sync, and never offer to reuse the current branch without confirming it isn't `main`.

### Forbidden Operations (on develop)
| Operation | Why |
|-----------|-----|
| `git merge main` | `develop` flows TO `main` only |
| `git pull origin main` | Pulls and merges `main` into `develop` |
| `git rebase main` | Rewrites `develop` history based on `main` |

If the branches diverge, merge `develop` into `main` via PR — never the reverse.

## PR Rules

**Verification — when asked to check a PR:**
1. `git fetch origin` (ALWAYS fetch first).
2. `git log origin/main..develop --oneline` (ALWAYS `origin/main`, not local).
3. `gh pr view <N> --json commits` to see what's in the PR.
4. Report the delta — never just update PR title/body. Never assert PR state from memory; confirm with `gh pr view`.

**Merged PRs** — once merged/closed, a PR is DEAD. After any `git push`:
1. Check `gh pr list --head develop --base main --state open`.
2. No open PR → create a NEW one. Never reference old PR numbers without checking state. If Patrick is deploying, the previous PR is already merged — create a new PR for any follow-up fix.

## Pre-Commit Protocol

Before every commit, show Patrick:
1. `git status` — staged, unstaged, untracked
2. `git diff` — actual changes
3. `git log -3` — recent commits for style
4. Planned commit message
5. What will NOT happen (no `main`, no deploy, no PR)

Then **WAIT for explicit approval**. A question or comment resets the checkpoint — answer it, then wait again. Also verify: `claudeLog.md` updated, all files staged, feature tested.

# SysTTS — project-specific

<!-- Project-specific rules. Universal rules + git flow + .NET-Windows-service stack -->
<!-- rules above are assembled from claude-env/shared/claude-md/ by sync-claude-md.sh. -->
<!-- Edit THIS file (or the shared fragments) — never edit the generated CLAUDE.md. -->
<!-- Generic .NET test stack (xUnit/Moq/FluentAssertions, AAA, naming), build/test -->
<!-- conventions, ILogger-from-DI, async/Task.Run, and config-requires-restart come -->
<!-- from the shared Windows-service stack tier above and are not repeated here. -->

Last verified: 2026-06-13

## Project Overview
System-level Text-to-Speech service for Windows:
- **System tray icon** for lifecycle control
- **HTTP API** (localhost:5100) for programmatic TTS from any app
- **Global hotkeys** (F22, F23) for quick-speak with voice selection
- **Stream Deck plugin** for button-based voice control
- **Neural voices** via Piper ONNX models through Sherpa-ONNX
- **Audio** via NAudio (volume + device selection)
- **Priority-based speech queue** with interrupt behavior

Sibling of whisper-service (both share the `[WINDOWS-SERVICE]` stack tier).

## Project Structure
```
src/SysTTS/                    # WinForms + ASP.NET Core (Kestrel) app
  Program.cs                   # host builder, endpoints
  appsettings.json             # port, voices, hotkeys, sources, audio
  TrayApplicationContext.cs    # system tray + lifecycle
  Services/  VoiceManager, TtsEngine (Sherpa-ONNX, thread-unsafe per instance),
             AudioPlayer (NAudio), SpeechQueue (priority, serial),
             SpeechService, HotkeyService (Win32 hooks, dedicated thread),
             ClipboardService, UserPreferences
  Handlers/  SpeakSelectionHandler (clipboard integration for hotkeys)
  Interop/   NativeMethods, VirtualKeyParser (Win32 P/Invoke)
  Settings/  Service, Audio, Hotkey, Source POCOs
  Forms/     VoicePickerForm
tests/SysTTS.Tests/            # xUnit
streamdeck-plugin/             # TypeScript/Node.js (rollup); com.systts.sdPlugin/
voices/ espeak-ng-data/ user-preferences.json   # gitignored runtime assets
scripts/download-models.ps1    # fetch Piper models from HuggingFace
docs/  CUSTOM_VOICES.md, INTEGRATION.md, TECHNICAL_SPEC.md
```

## Build & Run (project-specific)
```bash
dotnet build src/SysTTS/SysTTS.csproj
dotnet test  tests/SysTTS.Tests/
powershell.exe -File scripts/download-models.ps1   # one-time, Piper voice models
dotnet run --project src/SysTTS/SysTTS.csproj      # tray icon + HTTP API
curl http://127.0.0.1:5100/api/status              # verify API
# Stream Deck plugin:
cd streamdeck-plugin && npm install && npm run build   # npm run watch during dev
```
Before committing, additionally do **manual verification**: start app, test F22/F23 hotkeys, hit the API endpoints. Update `docs/TECHNICAL_SPEC.md` when adding components, and `appsettings.json` defaults when adding config keys.

## Architecture — Threading Model
- **Main STA thread:** WinForms context + message pump. Required for clipboard and UI dialogs (VoicePickerForm). `SynchronizationContext` captured at startup, injected via DI.
- **HotkeyService-Hook thread:** dedicated background thread with its own message pump; installs `WH_KEYBOARD_LL` (needs a message loop on the installing thread); runs `Application.Run()`; offloads processing to `Task.Run()` to stay within the 1000ms hook-callback timeout; marshals UI to the STA thread.
- **Kestrel thread pool:** HTTP API on background threads; marshals clipboard ops to the STA thread.
- **Synthesis/playback:** TtsEngine (Sherpa-ONNX) and AudioPlayer (NAudio) run off-thread; no blocking on the main thread.
- **Key rule:** SpeechService, TtsEngine, AudioPlayer, SpeechQueue are DI **singletons**; parallel requests serialize through the queue.

## Architecture — Speech Queue
Priority queue (lower number = higher precedence); serial synthesis+playback (no audio collision); configurable max depth (default 10, oldest low-priority evicted when full); `InterruptOnHigherPriority` stops current speech for a higher-priority request. Each source (e.g. `t-tracker`, `default`) has a priority and optional regex filters.

## Architecture — Voice Manager
Startup scans `voices/` for `.onnx` + `.onnx.json` pairs; TtsEngine instances lazy-loaded per voice and cached until shutdown; `FileSystemWatcher` detects new/deleted models at runtime (new models available immediately; source-mapping config changes need a restart).

## Architecture — Win32 Keyboard Hooks
`WH_KEYBOARD_LL` low-level hook via P/Invoke (not `RegisterHotKey`); delegate ref stored in a field to prevent GC; filters registered virtual key codes (F22/F23). Two modes: **Direct** (capture selection, speak with last-picked voice, fall back to configured) and **Picker** (capture text, show VoicePickerForm on STA thread, speak with selected voice, persist selection to UserPreferences).

## Architecture — Sherpa-ONNX & NAudio
Sherpa-ONNX (`org.k2fsa.sherpa.onnx` 1.12.23 + `...runtime.win-x64`) is **not thread-safe per instance** (TtsEngine serializes calls); needs `espeak-ng-data/` for phonemization; text → phonemes → float32 audio. NAudio (2.2.1) wraps WaveOutEvent, converts float32 → int16 PCM, volume via `WaveOutEvent.Volume`.

## Configuration Schema (`appsettings.json`)
```json
{
  "Service": { "Port": 5100, "VoicesPath": "voices", "DefaultVoice": "en_US-amy-medium",
               "EspeakDataPath": "espeak-ng-data", "MaxQueueDepth": 10, "InterruptOnHigherPriority": true },
  "Sources": { "source-name": { "voice": "voice-id", "filters": ["pattern"] , "priority": 1 } },
  "Hotkeys": [ { "Key": "F23", "Mode": "direct", "Voice": "voice-id" }, { "Key": "F22", "Mode": "picker" } ],
  "Audio":   { "OutputDevice": null, "Volume": 1.0 }
}
```
Port 5100 is localhost-only. Configuration changes require an application restart.

## HTTP API (`http://127.0.0.1:5100`)
| Endpoint | Method | Request | Response | Purpose |
|----------|--------|---------|----------|---------|
| `/api/status` | GET | — | `{ running, activeVoices, queueDepth }` | health |
| `/api/voices` | GET | — | `[{ id, name, sampleRate }]` | list voices |
| `/api/speak` | POST | `{ text, source?, voice? }` | 202 `{ queued, id }` | queue text |
| `/api/speak-selection` | POST | `{ voice? }` | 202 `{ queued, id, text }` | speak clipboard selection |
| `/api/stop` | POST | — | 200 `{ stopped }` | stop + clear queue |

400 = bad request (e.g. empty text); 202 = queued; 200 `{ queued: false }` = speak-selection with no selection.

## Dependencies
`org.k2fsa.sherpa.onnx` 1.12.23 + `...runtime.win-x64` (TTS); `NAudio` 2.2.1 (playback); `Microsoft.AspNetCore.App` (Kestrel/DI); test stack xUnit 2.5.3 / Moq 4.20.72 / FluentAssertions 6.12.2.

## Common Issues
| Issue | Cause | Fix |
|-------|-------|-----|
| Port 5100 in use | another listener | change port or kill process |
| Voices not found | models not downloaded | run `scripts/download-models.ps1` |
| Hotkeys not responding | WinForms window not active | focus the main window |
| Audio not playing | output device unavailable | set `Audio.OutputDevice` to null |
| ONNX model error | corrupt file / old Sherpa-ONNX | re-download, verify `.onnx.json` |
| Espeak data missing | path misconfigured | check `Service.EspeakDataPath` |

## References
Piper voices (huggingface.co/rhasspy/piper-voices) · Sherpa-ONNX (github.com/k2-fsa/sherpa-onnx) · NAudio (github.com/naudio/NAudio) · Stream Deck SDK (developer.elgato.com).
