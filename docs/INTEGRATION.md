# SysTTS Integration Guide

How to integrate third-party applications with the SysTTS HTTP API for text-to-speech functionality.

---

## API Endpoint Reference

All endpoints respond with JSON and require the SysTTS service to be running on the configured port (default: 5100).

### POST /api/speak

Queue a text message for speech synthesis.

**Request:**

```json
{
  "text": "Hello, world!",
  "source": "my-app",
  "voice": "en_US-amy-medium"
}
```

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `text` | string | Yes | The text to speak. Empty text is rejected with a 400 error. |
| `source` | string | No | Source identifier for configuration lookup (default: uses "default" source config). Determines voice mapping, filters, and priority. |
| `voice` | string | No | Override the default voice for this request. Voice ID is resolved against available voices. Takes precedence over source voice configuration. |

**Response (202 Accepted):**

```json
{
  "queued": true,
  "id": "550e8400-e29b-41d4-a716-446655440000"
}
```

**Response (400 Bad Request):**

```json
{
  "error": "text is required"
}
```

---

### POST /api/speak-selection

Capture selected text from the clipboard and queue it for speech.

This endpoint extracts the currently selected text (from Ctrl+C clipboard), applies source filters and voice resolution, then queues the result.

**Request:**

```json
{
  "voice": "en_US-amy-medium"
}
```

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `voice` | string | No | Override the default voice for this request. Resolved against available voices. |

**Response (202 Accepted):**

```json
{
  "queued": true,
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "text": "The selected text that was captured"
}
```

**Response (200 OK, no text selected):**

```json
{
  "queued": false,
  "text": ""
}
```

---

### GET /api/voices

List all available voices on the system.

**Request:** No body.

**Response (200 OK):**

```json
[
  {
    "id": "en_US-amy-medium",
    "name": "Amy (US English, Medium Quality)",
    "sampleRate": 22050
  },
  {
    "id": "en_GB-alba-medium",
    "name": "Alba (UK English, Medium Quality)",
    "sampleRate": 22050
  }
]
```

---

### GET /api/status

Get the current service status.

**Request:** No body.

**Response (200 OK):**

```json
{
  "running": true,
  "activeVoices": 2,
  "queueDepth": 3
}
```

| Field | Type | Description |
|-------|------|-------------|
| `running` | boolean | Always `true` if responding (service is running). |
| `activeVoices` | integer | Number of voice models currently loaded or available. |
| `queueDepth` | integer | Current number of pending speech requests in the queue. |

---

### POST /api/stop

Stop all ongoing playback and clear the speech queue.

**Request:** No body.

**Response (200 OK):**

```json
{
  "stopped": true
}
```

---

## Per-Source Configuration

Sources allow you to organize requests and apply different voice mappings, filters, and priorities based on the caller.

### Source Mapping

The `source` parameter in `/api/speak` requests maps to the `Sources` section in `appsettings.json`:

```json
{
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
  }
}
```

**Lookup process:**

1. Request specifies `source: "t-tracker"`
2. Service looks up `Sources:t-tracker` in config
3. If not found, falls back to `Sources:default`
4. If neither exists, request is rejected

### Voice Resolution

Voice selection follows a precedence hierarchy:

1. **Request voice override** (`voice` parameter in POST) — highest priority
2. **Source default voice** (from `Sources:{source}.voice`)
3. **Global default voice** (from `Service.DefaultVoice`)

Voice IDs are resolved against available voices via the VoiceManager. Unrecognized IDs fall back to the global default.

**Example:**

```
POST /api/speak with voice="en_GB-alba-medium" and source="t-tracker"
→ Uses "en_GB-alba-medium" (request override)

POST /api/speak with source="t-tracker" (no voice override)
→ Uses "custom-bear" (from Sources:t-tracker.voice)

POST /api/speak with source="unknown" (no voice override)
→ Uses "default" config, then falls back to "en_US-amy-medium"
```

### Regex Filters

Each source can define regex patterns to filter incoming text. If filters are specified, text is only queued if it matches at least one pattern.

**Configuration:**

```json
{
  "Sources": {
    "notifications": {
      "filters": ["alert", "warning", "error"],
      "priority": 2
    },
    "chat": {
      "filters": null
    }
  }
}
```

**Behavior:**

- `filters: null` or `[]` — all text passes through (unfiltered)
- `filters: ["pattern1", "pattern2"]` — text must match at least one regex (case-insensitive)
- Matching is case-insensitive and uses standard .NET regex syntax
- Invalid regex patterns are logged as warnings; the request is not queued

**Example:**

```
Source "notifications" with filters: ["error", "warning"]

Request text: "Error: connection failed"    → Queued (matches "error")
Request text: "Connection established"      → NOT queued (no match)
Request text: "WARNING: high latency"       → Queued (matches "warning", case-insensitive)
```

### Priority and Interrupt Behavior

Requests have a numeric priority (lower number = higher priority). Higher-priority requests can interrupt playback of lower-priority ones.

**Configuration:**

```json
{
  "Service": {
    "InterruptOnHigherPriority": true
  },
  "Sources": {
    "alerts": { "priority": 1 },
    "notifications": { "priority": 2 },
    "default": { "priority": 3 }
  }
}
```

**Behavior:**

- Higher priority (lower number) requests interrupt lower priority ones if `InterruptOnHigherPriority` is true
- Equal-priority requests queue after the current playback
- Queue has a max depth (default: 10); oldest low-priority items are evicted if queue is full

---

## Integration Examples

### JavaScript (Browser)

Fire-and-forget POST with error suppression:

```javascript
// Simple speak request
fetch('http://localhost:5100/api/speak', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ text: 'Hello!' })
})
.catch(() => {});  // SysTTS optional — suppress errors if unavailable

// With source and voice override
fetch('http://localhost:5100/api/speak', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    text: 'Alert: connection lost',
    source: 'alerts',
    voice: 'en_US-amy-medium'
  })
})
.catch(() => {});
```

### Python

With timeout and exception handling:

```python
import requests

def speak(text, source=None, voice=None):
    """Queue text for speech synthesis."""
    try:
        response = requests.post(
            'http://localhost:5100/api/speak',
            json={'text': text, 'source': source, 'voice': voice},
            timeout=2  # Connection timeout
        )
        if response.status_code == 202:
            data = response.json()
            print(f"Queued: {data['id']}")
        elif response.status_code == 400:
            print(f"Error: {response.json()['error']}")
    except requests.RequestException:
        # SysTTS unavailable — continue gracefully
        print("SysTTS not responding")

# Usage
speak("Text to speak")
speak("Alert message", source="alerts", voice="en_US-amy-medium")
```

### PowerShell

One-liner for testing:

```powershell
# Simple speak
Invoke-RestMethod -Uri "http://localhost:5100/api/speak" `
  -Method Post `
  -ContentType "application/json" `
  -Body '{"text":"Hello from PowerShell"}'

# Get status
Invoke-RestMethod -Uri "http://localhost:5100/api/status" -Method Get

# List voices
Invoke-RestMethod -Uri "http://localhost:5100/api/voices" -Method Get | Select-Object id, name
```

### C#

With HttpClient and fire-and-forget:

```csharp
using System.Net.Http;
using System.Text.Json;

public class SysTtsClient
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "http://localhost:5100";

    public SysTtsClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task SpeakAsync(string text, string? source = null, string? voice = null)
    {
        var request = new { text, source, voice };
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            System.Text.Encoding.UTF8,
            "application/json"
        );

        try
        {
            // Fire-and-forget: don't await response
            _ = _httpClient.PostAsync($"{BaseUrl}/api/speak", content);
        }
        catch (HttpRequestException)
        {
            // SysTTS unavailable — continue gracefully
        }
    }

    public async Task<IEnumerable<Voice>> GetVoicesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}/api/voices");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<Voice>>(json) ?? new();
        }
        catch (HttpRequestException)
        {
            return Enumerable.Empty<Voice>();
        }
    }

    public record Voice(string Id, string Name, int SampleRate);
}
```

### curl (Command Line)

Testing via curl:

```bash
# Speak text
curl -X POST http://localhost:5100/api/speak \
  -H "Content-Type: application/json" \
  -d '{"text":"Hello from curl"}'

# Speak with source and voice
curl -X POST http://localhost:5100/api/speak \
  -H "Content-Type: application/json" \
  -d '{"text":"Alert","source":"alerts","voice":"en_US-amy-medium"}'

# List voices
curl http://localhost:5100/api/voices | jq '.'

# Get status
curl http://localhost:5100/api/status | jq '.'

# Stop all speech
curl -X POST http://localhost:5100/api/stop
```

---

## Graceful Degradation

**SysTTS is optional.** Client applications must not require it to function.

### Design Pattern

Never let TTS failures block normal operation:

```javascript
// Good: fire-and-forget with error suppression
async function notifyUser(message) {
  // Always show notification
  ui.showNotification(message);

  // Optionally speak — but failure doesn't matter
  fetch('http://localhost:5100/api/speak', {
    method: 'POST',
    body: JSON.stringify({ text: message })
  }).catch(() => {});  // Ignore errors
}
```

```csharp
// Good: wrap in try-catch, continue on failure
async Task NotifyUser(string message)
{
    // Always show notification
    await ui.ShowNotificationAsync(message);

    // Try to speak, but don't block
    try
    {
        await ttsClient.SpeakAsync(message);
    }
    catch (HttpRequestException)
    {
        // SysTTS not available — app continues normally
    }
}
```

### Implementation Guidelines

1. **Timeouts:** Set reasonable HTTP timeouts (2-5 seconds) to avoid hanging if SysTTS is slow
2. **No Retry Loops:** Don't retry failed TTS requests — just log and move on
3. **Check `/api/status`:** Call `GET /api/status` to probe if available before using TTS features
4. **Log Errors:** Log TTS failures to help diagnose issues, but don't surface to users
5. **Configuration:** Make TTS optional in application settings (e.g., a toggle to enable/disable)

---

## Stream Deck Plugin

### Installation

**Development:**

```bash
streamdeck link [plugin-path]
```

**Distribution:**

Package plugin as `.streamDeckPlugin` file and distribute via Elgato app store or directly.

### Actions

The plugin provides three actions:

| Action | Function | Configuration |
|--------|----------|---------------|
| **Speak Selection** | Capture selected text and speak | Voice dropdown (optional) |
| **Speak Text** | Queue fixed text | Text field, voice dropdown |
| **Stop Speaking** | Stop all playback | None |

### Property Inspector

The plugin includes a Property Inspector (right sidebar) for configuring voice selection and text:

- **Voice Dropdown:** Fetches available voices from `/api/voices`, allows per-button override
- **Text Field:** Enter custom text for "Speak Text" action
- **Status Display:** Shows queue depth and service status

---

## Troubleshooting

### "Connection refused" or "Connection timed out"

- Verify SysTTS is running: `curl http://localhost:5100/api/status`
- Check port configuration in `appsettings.json` (default: 5100)
- Ensure firewall allows localhost connections
- Windows 10/11: check Windows Defender Firewall settings for Kestrel

### Voice not found / falling back to default

- List available voices: `curl http://localhost:5100/api/voices`
- Verify voice ID exactly matches (case-sensitive)
- Check `appsettings.json` `DefaultVoice` configuration
- Ensure Piper voice models are in `voices/` directory

### Text filtered out (not queued)

- Check `/api/speak` returns `"queued": false`
- Verify source regex filters in `Sources` config
- Filters are case-insensitive; use `.*` for any text
- Invalid regex patterns are logged as warnings; check logs

### High priority request not interrupting playback

- Verify `Service.InterruptOnHigherPriority` is `true` in config
- Check priority numbers: lower = higher priority
- Check queue depth — if queue is full, oldest low-priority items are evicted

---

## Rate Limiting and Best Practices

- **Single Concurrency:** SysTTS processes one speech request at a time (queue-based)
- **Max Queue Depth:** Default 10 requests; additional requests may be dropped
- **No Rate Limiting:** API itself has no rate limits; rely on source configuration for filtering
- **Typical Latency:** 100-500ms queue latency + synthesis time (voice-dependent, usually 1-5 seconds)

---

## See Also

- **README.md** — Quick start and configuration overview
- **CUSTOM_VOICES.md** — Adding custom voices
- **TECHNICAL_SPEC.md** — Architecture and component details
