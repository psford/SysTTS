<!-- GENERATED FILE — DO NOT EDIT. -->
<!-- Shared rules: claude-env/shared/claude-md/. Project rules: CLAUDE.local.md. -->
<!-- Regenerate: helpers/sync-claude-md.sh <repo> -->


# Shared Rules (universal)

<!-- Canonical source: claude-env/shared/claude-md/00-universal.md. Edit HERE, not in any generated CLAUDE.md. -->

These behavioral rules are shared across all of Patrick's repos. They are assembled into each repo's `CLAUDE.md` by `claude-env/helpers/sync-claude-md.sh`. Project-specific contracts live in that repo's `CLAUDE.local.md`.

## Critical Behavioral Checkpoints

| Checkpoint | Rule |
|------------|------|
| **DIAGNOSE BEFORE FIX** | Diagnose root cause first (inspect, measure, log). NEVER guess. Verify the fix before reporting. |
| **PRODUCT DECISIONS** | When Patrick makes a UX/product decision, implement it. Technical objections only for data loss, security, or irreversibility. Record in `docs/decisions.md`. |
| **TEST BEFORE SUGGESTING** | NEVER tell the user to do something without verifying it works. If you can't test it, say so. |
| **VERIFY BEFORE CLAIMING DONE** | Every "✓ / verified / works / passing" must be backed by an exact command and its real output. Label provenance: verified-by-me, trusted-from-agent, or not-verified. A bundle-grep proves code shipped, not that the feature works; `curl` does not enforce CORS; a "Skipping X / not installed" message that exits 0 is failure wearing a success mask — treat it as a blocker. |
| **AUDIT THE CLASS** | When a bug is found as "we forgot X in location Y," immediately search every other location where X might also be missing. Fix the class, not the instance. |

## Principles

| Principle | Description |
|-----------|-------------|
| **Rules are hard blocks** | Patrick's rules are HARD BLOCKS. Hooks must fail (non-zero), never warn-and-pass. |
| **Challenge me** | Push back against bad practices or security vulnerabilities. |
| **Admit limitations** | Never pretend capabilities you lack. Say so and suggest mitigations. |
| **UI matches implementation** | Never put placeholder text suggesting unbuilt functionality. |
| **Evaluate all options** | Before saying "no", consider all tools: Bash, PowerShell, web access, APIs, system commands. |
| **Do it yourself** | Work autonomously. Never ask the user to do something you can do. Escalate only for commit/deploy approval or genuine capability gaps. |
| **Act on credentials** | When given API keys/passwords, use them directly — don't hand instructions back. Pull from Key Vault / `.env` before asking. |
| **Don't propose deferring** | When blocked, push through or ask Patrick to unblock and stand by. Don't recommend "defer to a later session." |
| **Questions require answers** | If you ask "Ready to commit?" — STOP and wait. Never ask then immediately act. |
| **No feature regression** | Changes must never silently lose functionality. |
| **Fix problems immediately** | No technical debt. Fix deprecated code, broken things, suboptimal patterns now. |
| **Flag deprecated APIs** | Use current APIs in new code. Fix straightforward deprecations; flag complex ones. |
| **Right-size to scale** | Match engineering effort to actual scope; don't over-engineer hobby projects. But never dodge a firm requirement the user set. |
| **Design prototypes are contracts** | Implement EVERY effect in a prototype. |
| **PowerShell ONLY for Windows** | The Bash tool runs actual bash. For Windows: `powershell.exe -Command "..."`. Never raw bash syntax for Windows targets. |
| **Prefer FOSS / winget** | MIT/Apache/BSD over proprietary. Lightweight, offline-capable. |
| **No paid services** | Never sign up for paid services on Patrick's behalf. |
| **No ad tech/tracking** | No advertising, tracking pixels, or data sharing with X/Meta. |
| **Cite sources** | When making recommendations, cite sources so Patrick can verify. |
| **Respect public APIs** | Rate limit (single-concurrency, 2s gap), cache in DB, polite User-Agent. |
| **Log sanitization** | ALL user strings in logs wrapped in sanitization wrappers where applicable. |
| **Cross-browser / local CSS** | Standard APIs and CSS only. Locally compiled CSS; CDN only for large libs with SRI hashes. Firefox is Patrick's primary browser — verify UI changes there, not just Chromium. |
| **Verify repo context** | Before writing files or committing to a repo other than the one open in the IDE, verify the target repo's current branch and confirm it's the correct destination. |
| **Preserve original media** | Never degrade user-uploaded media. Store originals at full quality; use resized/compressed versions for display only, always with a path to the original. |
| **Own it all** | Any Claude instance is "me" — don't distance from prior-session work. Environment gaps blocking verification (missing binaries, locked sudo, missing creds) are mine to surface and unblock; "pre-existing on main" is descriptive, not exculpatory. |

## Coding Standards

- **Naming:** JavaScript/TypeScript `camelCase` | Python `snake_case` (PEP 8) | Bash `snake_case` | Docs GitHub-flavored Markdown.
- **Testing:** Code compiling is NOT sufficient. Run tests before committing. Test external dependencies before integrating.
- **Script validation:** Bash scripts must be shellcheck-clean. Python scripts must pass linting (flake8 or ruff).
- **Hot loops:** Default to numba `@njit` for tight numerical Python loops (standing approval).
- **Dependencies:** Walk the peer-dep graph with `npm view` BEFORE installing; never `--force` past a conflict; treat the runtime version as fixed.

### Model Delegation
| Model | Use for |
|-------|---------|
| **Haiku** | Quick scripts, simple file ops, straightforward fixes, running tests |
| **Sonnet** | General development, coding, debugging (default) |
| **Opus** | Architecture, complex refactors, deep research, system design |

Run agents in parallel when possible.

## Communication

- **Research before asking** — search the web first; only ask Patrick if still unclear.
- **Correction vs inquiry** — if Patrick asks "Did you do X?", ask whether it should become a guideline.
- **Proactive updates** — when agreement is reached on a feedback-based rule, add it to the shared rules immediately.
- **Always give links** — provide PR/deploy links immediately after pushing; don't make Patrick ask.

## Session Protocol

- **Starting ("hello!"):** read `CLAUDE.md` + the repo's stated session files (e.g. `sessionState.md`, `claudeLog.md`, `docs/decisions.md`).
- **During:** checkpoint to `sessionState.md` after major tasks, every 10–15 exchanges, and before complex work. Only load files actively needed (CLAUDE.md always loaded). Delete completed plan files; verify git state before working from plans.
- **Ending ("night!"):** update `sessionState.md`, commit pending changes, update `claudeLog.md`.

## File Management

- **CLAUDE.md backups:** save as `claude_MMDDYYYY-N.md` before a manual update (N/A for generated CLAUDE.md — edit `CLAUDE.local.md` or the shared fragments instead).
- **Logging:** log to `claudeLog.md` with date, description, result. Omit sensitive data.
- **Archives:** source to `archive/`. Delete `__pycache__`, `node_modules`, `bin/`, `obj/`, logs, temp files.

## Security

- **Personal identifiers are secrets.** Personal email addresses, phone numbers, home addresses, and personal domains (e.g. `psford.com`) are credentials — never hardcoded in source committed to public repos. Use `example.com` in defaults, docs, and config templates. Real values belong in `.env` (gitignored) or environment variables only. Support/business emails created for a project are fine.
- Review SAST/DAST coverage when introducing new frameworks (SecurityCodeScan for C#, Bandit for Python).
- Hooks run automatically — if blocked, try to adjust; if stuck, ask Patrick.

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

# Stack: .NET Windows Service

<!-- Canonical source: claude-env/shared/claude-md/stack-windows-service.md. -->
<!-- Shared by SysTTS, whisper-service, and any future .NET Windows-service repo. -->

## Build & Run
- `dotnet build`, `dotnet test`, `dotnet run`. Windows-only scripts run via PowerShell (`powershell.exe -Command "..."` from WSL).
- Configuration changes (`appsettings.json`) require an application restart — there is no hot reload for a tray/service host.
- Treat compiler warnings as errors before committing.

## Testing
- Stack: **xUnit + Moq + FluentAssertions**.
- Test naming: `MethodName_Condition_Expected`.
- Structure every test Arrange / Act / Assert.
- `dotnet test` must pass before committing (run all, or filter by class during dev).

## Coding Conventions
- C# (.NET 8): PascalCase types/methods, camelCase locals/fields.
- Nullable enabled: use `??` for defaults; avoid `!` (null-forgiving) unless justified.
- Prefer `async`/`await`; offload long-running synthesis/IO with `Task.Run`.
- Logging via `ILogger` from DI; no `Console.WriteLine` in production paths.

## Service / Host Patterns
- Tray + Kestrel hosts: marshal UI/STA-thread work off background request threads via the captured `SynchronizationContext`.
- Long native callbacks (e.g. Win32 hooks) must offload work to stay within their callback deadline.

## CI / Release
- CI is the shared reusable workflow: `psford/claude-env/.github/workflows/windows-service-build-release.yml@main`. The companion repo's workflow is a thin wrapper passing `app_name`, `project_path`, `appsettings_source`.
- Releases are self-contained executables + a Windows Service install script, published as GitHub Releases (zip + SHA256).
- Deployment to a Windows host uses `infrastructure/windows-deploy/deploy-app.ps1` against the app registry.

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
