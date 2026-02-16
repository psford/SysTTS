# Custom Voices Guide

This guide explains how to add and manage custom voices in SysTTS. SysTTS uses **Piper-compatible ONNX models** for text-to-speech synthesis. You can download pre-built voices or train custom models using external tools.

---

## Piper Model Format

A Piper voice consists of three components:

1. **`.onnx` model file** — The neural network model containing the voice synthesis weights (typically 15-75 MB depending on quality)
2. **`.onnx.json` config file** — Metadata and configuration for the model, including:
   - Audio sample rate (e.g., 22050 Hz)
   - Phoneme type (e.g., "espeak" for English)
   - Model architecture details
3. **`espeak-ng-data/` directory** — Shared phonemization data (required for all voices, configured once)

### File Naming

Voice files must follow this pattern:
- Model: `voice_name.onnx`
- Config: `voice_name.onnx.json`

The filename (without extension) becomes the **voice ID** used in configuration and API requests.

Example:
- Files: `en_US-amy-medium.onnx`, `en_US-amy-medium.onnx.json`
- Voice ID: `en_US-amy-medium`

---

## Downloading Pre-Built Voices

Pre-trained Piper voices are available on HuggingFace. These are ready to use immediately.

### Browse Available Voices

1. Visit [rhasspy/piper-voices](https://huggingface.co/rhasspy/piper-voices) on HuggingFace
2. Browse directories for your language (e.g., `en_US/`, `fr_FR/`, etc.)
3. Each language includes multiple speakers and quality levels

### Quality Levels

Piper voices are provided in three quality tiers:

| Quality | File Size | Audio Quality | Use Case |
|---------|-----------|---------------|----------|
| **low** | ~15 MB | Acceptable, faster synthesis | Real-time applications, low-bandwidth scenarios |
| **medium** | ~45 MB | Good quality, balanced | Default choice for most applications |
| **high** | ~75 MB | Excellent quality, slower synthesis | High-quality output, when speed is not critical |

**Recommendation:** Start with `medium` quality. Use `low` if you need fast synthesis, or `high` if you prioritize audio quality.

### Download Steps

1. **Browse the HuggingFace repository** and select a language/speaker/quality combination
2. **Download both files**:
   - The `.onnx` file (the model)
   - The `.onnx.json` file (the configuration)
3. **Place files in the `voices/` directory** at the SysTTS root:
   ```
   SysTTS/
   ├── voices/
   │   ├── en_US-amy-medium.onnx
   │   ├── en_US-amy-medium.onnx.json
   │   ├── fr_FR-gilles-medium.onnx
   │   └── fr_FR-gilles-medium.onnx.json
   ```

### Automatic Detection

SysTTS uses a `FileSystemWatcher` to detect new voices in real-time:

- **Files are scanned immediately** when placed in `voices/`
- **No restart required** for voice availability in the API or UI
- **Voice picker** (hotkey mode) updates automatically
- **Configuration changes** require an application restart (see configuration section below)

---

## Custom Training Workflows

While SysTTS itself is a consumer application, you can train custom voices using external tools and import them. Training is outside the scope of SysTTS but is documented here for reference.

### GPT-SoVITS Training

**GPT-SoVITS** is a modern neural voice cloning framework that produces high-quality custom voices.

**Overview:**
- Uses generative AI to capture voice characteristics from audio samples
- Produces ONNX-exportable models compatible with Piper
- Supports fast cloning (requires minimal audio) and fine-tuning (higher quality)

**Data Preparation:**
1. Record or gather 5-30 minutes of clean audio clips of the target voice
2. Create transcripts for each clip (text that was spoken)
3. Organize as `{name}.wav` and `{name}.txt` pairs in a dataset folder

**Training Process:**
1. Prepare reference audio (5-10 seconds of the voice in isolation)
2. Configure training parameters (duration, learning rate, epochs)
3. Run training pipeline: `python train.py --config config.yaml`
4. Monitor training loss and quality on validation set

**ONNX Export:**
1. Export trained checkpoint to ONNX format: `python export_onnx.py --checkpoint model.pt`
2. Convert to Piper-compatible format (extract audio config, rename files)

**Resources:**
- [GPT-SoVITS GitHub](https://github.com/RVC-Boss/GPT-SoVITS)
- Requires Python 3.8+, PyTorch, and CUDA/GPU for reasonable training times

### Piper Fine-Tuning

**Piper** is the TTS engine used by SysTTS. You can fine-tune existing voices or train from scratch using Piper's native pipeline.

**Dataset Format (LJSpeech):**
```
dataset/
├── wavs/
│   ├── 0001.wav
│   ├── 0002.wav
│   └── ...
└── metadata.csv
```

The `metadata.csv` contains one line per audio file:
```
0001|This is the transcribed text.
0002|Another example sentence.
```

**Training Commands:**
```bash
# Fine-tune an existing voice
python -m piper.train --dataset-dir ./dataset --model-path model.onnx --output-path fine_tuned.onnx

# Train from scratch (requires more data)
python -m piper.train --dataset-dir ./dataset --output-path custom_voice.onnx
```

**Export Checkpoint:**
1. Training produces periodic checkpoints
2. Export the final checkpoint to ONNX: `python -m piper.export --checkpoint model.pt --output voice.onnx`

**Important:** Piper training requires:
- 1-5+ hours of aligned audio-text data for good results
- GPU acceleration (CUDA) for reasonable training time
- Proper preprocessing (silence trimming, audio normalization)

**Resources:**
- [Piper GitHub](https://github.com/rhasspy/piper)
- [Piper Training Guide](https://github.com/rhasspy/piper/blob/master/TRAINING.md)

---

## Importing a Custom Voice

Once you have a trained model or downloaded a pre-built voice, follow these steps:

### Step 1: Place Files in `voices/` Directory

Copy the `.onnx` and `.onnx.json` files to the `voices/` directory:

```bash
cp my_voice.onnx voices/
cp my_voice.onnx.json voices/
```

VoiceManager will automatically scan and register the voice within seconds.

### Step 2: Verify Configuration File

Open the `.onnx.json` file and ensure it contains the required fields:

```json
{
  "audio": {
    "sample_rate": 22050
  },
  "espeak": {
    "voice": "en-us"
  },
  "...": "other metadata"
}
```

**Key fields:**
- `audio.sample_rate` — must match the model's output rate (usually 22050 or 24000 Hz)
- `espeak.voice` — phoneme language code (e.g., `en-us`, `fr`, `de`)

If these fields are missing, add them manually. Incorrect values will cause audio artifacts or synthesis failures.

### Step 3: Update Configuration (If Using Source Mappings)

If you want to assign this voice as the default for a specific source, edit `appsettings.json`:

```json
{
  "Service": {
    "DefaultVoice": "my_voice"
  },
  "Sources": {
    "myapp": {
      "voice": "my_voice",
      "filters": ["important", "alert"],
      "priority": 1
    }
  }
}
```

**Mapping elements:**
- `Service.DefaultVoice` — fallback voice if none specified in request
- `Sources.<name>.voice` — voice for a specific source (e.g., "myapp")
- Voice ID must match the filename without extension

### Step 4: Restart Application

Configuration changes in `appsettings.json` require an application restart:

```bash
dotnet run --project src/SysTTS/SysTTS.csproj
```

The voice is now available for use via:
- **HTTP API** — `POST /api/speak` with `voice: "my_voice"`
- **Voice picker** — hotkey with `mode: "picker"` shows the voice in the dropdown
- **Direct hotkey** — hotkey with `mode: "direct"` and `Voice: "my_voice"`

---

## Troubleshooting

### Model Not Appearing in Voice List

**Symptom:** You placed a voice file in `voices/` but it doesn't show up in the API or picker.

**Diagnosis:**
1. Verify both files exist:
   - `voice_name.onnx`
   - `voice_name.onnx.json`
2. Check file permissions — SysTTS must have read access
3. Review application logs for scan errors

**Resolution:**
- Ensure the JSON file is named exactly `{modelname}.onnx.json`
- Verify JSON syntax: `python -m json.tool voice_name.onnx.json`
- If corrupted, re-download or regenerate the `.onnx.json` config file

### Wrong Pitch or Playback Speed

**Symptom:** Audio sounds unnaturally high-pitched or too fast/slow.

**Cause:** Sample rate mismatch between the model and configuration.

**Resolution:**
1. Verify the model's actual output sample rate (check training logs or Piper documentation)
2. Update `.onnx.json` to match:
   ```json
   {
     "audio": {
       "sample_rate": 24000
     }
   }
   ```
3. Restart SysTTS

**Common sample rates:** 22050 Hz (default), 24000 Hz, 44100 Hz

### Garbled or Robotic Audio

**Symptom:** Synthesized speech sounds distorted, robotic, or unintelligible.

**Causes:**
1. Missing or incorrect `espeak-ng-data/` directory
2. Phoneme language mismatch (e.g., English model configured for French phonemes)
3. Corrupted ONNX file

**Resolution:**
1. Verify `espeak-ng-data/` directory exists at the configured path in `appsettings.json`:
   ```json
   {
     "Service": {
       "EspeakDataPath": "espeak-ng-data"
     }
   }
   ```
2. Download espeak-ng-data from [rhasspy/piper-phonemizer](https://github.com/rhasspy/piper-phonemizer)
3. Verify the `espeak.voice` in `.onnx.json` matches the model's language:
   - English: `"en-us"`, `"en-gb"`
   - French: `"fr"`
   - German: `"de"`
   - Spanish: `"es"`
4. Re-download the voice files if corruption is suspected

### Model Loading Error

**Symptom:** Application logs show "Failed to load ONNX model" or Sherpa-ONNX errors.

**Causes:**
1. ONNX file is corrupted
2. Model uses unsupported Piper architecture version
3. Sherpa-ONNX version mismatch

**Resolution:**
1. Verify the ONNX file is not corrupted:
   ```bash
   # Check file size — should be at least 10 MB for low quality
   ls -lh voice_name.onnx
   ```
2. Re-download the voice from HuggingFace
3. Check application version — ensure Sherpa-ONNX dependencies are up-to-date:
   ```bash
   dotnet list package --vulnerable
   ```
4. Try a known-working voice (e.g., `en_US-amy-medium`) to isolate the issue

---

## Advanced: Adding New Language Support

SysTTS supports any Piper voice, including languages beyond English. To add a new language:

1. **Download voice files** from [rhasspy/piper-voices](https://huggingface.co/rhasspy/piper-voices) for the desired language
2. **Place in `voices/`** — automatic detection applies to all languages
3. **Verify phoneme data** — ensure `espeak-ng-data/` includes the language (usually includes all common languages by default)
4. **Test with a POST request**:
   ```bash
   curl -X POST http://localhost:5100/api/speak \
     -H "Content-Type: application/json" \
     -d '{"text": "Bonjour le monde", "voice": "fr_FR-gilles-medium"}'
   ```

---

## Links

- **Piper GitHub:** https://github.com/rhasspy/piper
- **Pre-trained Voices:** https://huggingface.co/rhasspy/piper-voices
- **GPT-SoVITS Training:** https://github.com/RVC-Boss/GPT-SoVITS
- **espeak-ng Phonemizer:** https://github.com/rhasspy/piper-phonemizer
- **SysTTS HTTP API:** See `INTEGRATION.md`
- **SysTTS Technical Details:** See `TECHNICAL_SPEC.md`
