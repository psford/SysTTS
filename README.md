# SysTTS

System-level text-to-speech service for Windows with HTTP API, global hotkeys, Stream Deck support, and Piper neural voices.

SysTTS provides a unified TTS endpoint that applications can integrate with. Select text anywhere on your system, press a hotkey, or send HTTP requests to have it spoken aloud. Control pronunciation with custom voice models and fine-grained source-based routing.

## Prerequisites

- **.NET 8 SDK** — [Download from microsoft.com](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- **Windows 10/11** — WinForms and Win32 keyboard hooks required
- **Optional:** Stream Deck + Elgato Stream Deck software for hardware control

## Quick Start

### 1. Clone the Repository

```bash
git clone https://github.com/psford/SysTTS.git
cd SysTTS
```

### 2. Download Voice Models

Download Piper voice models and espeak-ng phonemization data:

```powershell
powershell.exe -File scripts/download-models.ps1
```

This downloads the default voice (`en_US-amy-medium`, ~45 MB) and shared espeak-ng data required for phonemization. Models are stored in `voices/` and `espeak-ng-data/` directories (both gitignored).

### 3. Configure (Optional)

Edit `appsettings.json` to customize:

```json
{
  "Service": {
    "Port": 5100,
    "DefaultVoice": "en_US-amy-medium",
    "VoicesPath": "voices",
    "EspeakDataPath": "espeak-ng-data",
    "MaxQueueDepth": 10,
    "InterruptOnHigherPriority": true
  },
  "Sources": {
    "t-tracker": {
      "voice": "custom-bear",
      "filters": ["approaching", "arrived"],
      "priority": 1
    },
    "default": {
      "voice": "en_US-amy-medium",
      "filters": null,
      "priority": 3
    }
  },
  "Hotkeys": [
    { "Key": "F23", "Mode": "direct", "Voice": "en_US-amy-medium" },
    { "Key": "F22", "Mode": "picker" }
  ],
  "Audio": {
    "OutputDevice": null,
    "Volume": 1.0
  }
}
```

See **Configuration Reference** section below for details.

### 4. Run

```bash
dotnet run --project src/SysTTS/SysTTS.csproj
```

The application starts in the system tray. Kestrel listens on `http://127.0.0.1:5100`.

### 5. Verify

```bash
curl http://localhost:5100/api/status
```

Expected response:
```json
{
  "running": true,
  "activeVoices": 1,
  "queueDepth": 0
}
```

## Input Modes

### HTTP API

Send JSON requests to HTTP endpoints:

```bash
curl -X POST http://localhost:5100/api/speak \
  -H "Content-Type: application/json" \
  -d '{"text": "Hello world", "voice": "en_US-amy-medium"}'
```

Response: `202 Accepted` with `{ "queued": true, "id": "uuid" }`

### Global Hotkeys

Press F23 (configurable) to speak selected text using the default voice. Press F22 to open a voice picker dialog before speaking. Hotkeys work in any application.

### Stream Deck

Install the SysTTS plugin and add buttons to your Stream Deck profile. Each button can speak fixed text or selection with a custom voice.

## Configuration Reference

| Section | Key | Type | Default | Purpose |
|---------|-----|------|---------|---------|
| **Service** | Port | int | 5100 | HTTP server port |
| | VoicesPath | string | `voices` | Voice models directory |
| | DefaultVoice | string | `en_US-amy-medium` | Default voice ID |
| | EspeakDataPath | string | `espeak-ng-data` | Phonemization data directory |
| | MaxQueueDepth | int | 10 | Max queued requests before eviction |
| | InterruptOnHigherPriority | bool | true | Cancel low-priority speech for high-priority requests |
| **Sources** | (keys) | object | see above | Source-based routing and filtering |
| (per-source) | voice | string | — | Default voice for this source |
| | filters | string[] or null | — | Regex patterns to match; null = all text |
| | priority | int | — | Lower = higher priority (1 = highest) |
| **Hotkeys** | (array) | object | see above | Keyboard hotkey definitions |
| (per-hotkey) | Key | string | — | Key name (F1-F24, A-Z, etc.) |
| | Mode | string | — | `"direct"` (speak) or `"picker"` (voice selection dialog) |
| | Voice | string | (optional) | Voice override for this hotkey |
| **Audio** | OutputDevice | string or null | null | Audio device name; null = default device |
| | Volume | float | 1.0 | Output volume (0.0 = silent, 1.0 = max) |

## Available Voices

Browse pre-built voices on [rhasspy/piper-voices](https://huggingface.co/rhasspy/piper-voices) (HuggingFace).

Quality levels:
- **low** (~15 MB) — Fast, lower fidelity
- **medium** (~45 MB) — Balanced (default: `en_US-amy-medium`)
- **high** (~75 MB) — High fidelity, slower

Download voice models and place `.onnx` + `.onnx.json` files in the `voices/` directory. Voice ID is the filename without extension. SysTTS detects new voices automatically via FileSystemWatcher.

## Further Reading

- **[CUSTOM_VOICES.md](docs/CUSTOM_VOICES.md)** — Training custom voices with GPT-SoVITS or Piper
- **[INTEGRATION.md](docs/INTEGRATION.md)** — HTTP API reference and code examples
- **[TECHNICAL_SPEC.md](docs/TECHNICAL_SPEC.md)** — Architecture, component catalog, thread model

## License

MIT
