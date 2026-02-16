"""Ensure Piper ONNX models have required sherpa-onnx metadata.

sherpa-onnx reads metadata from the ONNX model's metadata_props at init time.
If required fields are missing, the native C++ code crashes with an AccessViolation.

Piper models from HuggingFace don't include this metadata — only the companion
.onnx.json has the information. This script reads the JSON config and patches
the ONNX file with all required metadata.

Required by sherpa-onnx (crash if missing):
    sample_rate, n_speakers, language, comment

Important for correct behavior:
    add_blank, frontend

This script is idempotent: running on an already-patched model is safe.

Usage:
    python scripts/ensure_model_metadata.py <model.onnx> <model.onnx.json>
    python scripts/ensure_model_metadata.py voices/en_US-amy-medium.onnx voices/en_US-amy-medium.onnx.json

Exit codes:
    0 = success (patched or already had metadata)
    1 = error
"""

import json
import os
import sys

try:
    import onnx
except ImportError:
    print("ERROR: 'onnx' package not installed. Run: pip install onnx", file=sys.stderr)
    sys.exit(1)


# Fields that cause native crashes if missing
REQUIRED_METADATA = {"sample_rate", "n_speakers", "language", "comment", "has_espeak", "voice"}

# Fields that affect behavior but have safe defaults
RECOMMENDED_METADATA = {"add_blank", "frontend", "model_type"}


def check_metadata(model_path: str) -> dict[str, str]:
    """Return existing metadata as a dict."""
    mdl = onnx.load(model_path)
    return {prop.key: prop.value for prop in mdl.metadata_props}


def read_config(config_path: str) -> dict:
    """Read the Piper .onnx.json config file."""
    with open(config_path, "r", encoding="utf-8") as f:
        return json.load(f)


# Map ISO language codes to full names that sherpa-onnx expects.
# sherpa-onnx uses the full language name from ONNX metadata to configure espeak-ng.
LANGUAGE_MAP = {
    "af": "Afrikaans", "am": "Amharic", "ar": "Arabic", "bn": "Bengali",
    "ca": "Catalan", "cs": "Czech", "cy": "Welsh", "da": "Danish",
    "de": "German", "el": "Greek", "en": "English", "es": "Spanish",
    "et": "Estonian", "fa": "Persian", "fi": "Finnish", "fr": "French",
    "gl": "Galician", "gu": "Gujarati", "ha": "Hausa", "he": "Hebrew",
    "hi": "Hindi", "hr": "Croatian", "hu": "Hungarian", "id": "Indonesian",
    "is": "Icelandic", "it": "Italian", "ja": "Japanese", "jv": "Javanese",
    "ka": "Georgian", "kk": "Kazakh", "km": "Khmer", "kn": "Kannada",
    "ko": "Korean", "lb": "Luxembourgish", "lt": "Lithuanian", "lv": "Latvian",
    "ml": "Malayalam", "mn": "Mongolian", "mr": "Marathi", "ms": "Malay",
    "my": "Burmese", "ne": "Nepali", "nl": "Dutch", "no": "Norwegian",
    "pa": "Punjabi", "pl": "Polish", "pt": "Portuguese", "ro": "Romanian",
    "ru": "Russian", "sk": "Slovak", "sl": "Slovenian", "so": "Somali",
    "sr": "Serbian", "sv": "Swedish", "sw": "Swahili", "ta": "Tamil",
    "te": "Telugu", "th": "Thai", "tr": "Turkish", "uk": "Ukrainian",
    "ur": "Urdu", "vi": "Vietnamese", "yo": "Yoruba", "zh": "Chinese",
}


def derive_language_and_voice(model_path: str, config: dict) -> tuple[str, str]:
    """Derive full language name and espeak voice from model config/filename.

    sherpa-onnx expects 'language' as the full English name (e.g. 'English')
    and 'voice' as the espeak-ng voice identifier (e.g. 'en-us').

    Piper model naming: {lang}_{REGION}-{speaker}-{quality}.onnx
    e.g. en_US-amy-medium.onnx -> language=English, voice=en-us
    """
    iso_code = ""
    region = ""

    # Try config first
    lang = config.get("language", {})
    if isinstance(lang, dict):
        code = lang.get("code", "")
        if code:
            parts = code.split("_")
            iso_code = parts[0].lower()
            region = parts[1].lower() if len(parts) > 1 else ""

    # Fall back to filename
    if not iso_code:
        basename = os.path.splitext(os.path.basename(model_path))[0]
        if "_" in basename:
            parts = basename.split("_")
            iso_code = parts[0].lower()
            # Extract region: en_US-amy-medium -> US
            if len(parts) > 1:
                region = parts[1].split("-")[0].lower()

    if not iso_code:
        iso_code = "en"

    # Build full language name
    language = LANGUAGE_MAP.get(iso_code, iso_code.title())

    # Build espeak voice identifier (e.g. "en-us")
    voice = f"{iso_code}-{region}" if region else iso_code

    return language, voice


def build_metadata(model_path: str, config: dict) -> dict[str, str]:
    """Build the complete metadata dict from the Piper config.

    Metadata format matches official sherpa-onnx pre-converted Piper models.
    """
    sample_rate = config.get("audio", {}).get("sample_rate", 22050)
    num_speakers = config.get("num_speakers", 1)
    language, voice = derive_language_and_voice(model_path, config)

    # Piper models use espeak-ng for phonemization and add blanks between phonemes
    phoneme_type = config.get("phoneme_type", "espeak")

    return {
        # Required (native crash without these)
        "sample_rate": str(sample_rate),
        "n_speakers": str(num_speakers),
        "language": language,
        "comment": "piper",
        "has_espeak": "1",
        "voice": voice,
        # Recommended (correct behavior)
        "add_blank": "1",
        "frontend": phoneme_type,
        "model_type": "vits",
    }


def patch_model(model_path: str, metadata: dict[str, str]) -> None:
    """Add missing metadata entries to an ONNX model file."""
    mdl = onnx.load(model_path)

    existing_keys = {prop.key for prop in mdl.metadata_props}

    added = []
    for key, value in metadata.items():
        if key not in existing_keys:
            prop = mdl.metadata_props.add()
            prop.key = key
            prop.value = value
            added.append(f"{key}={value}")

    if added:
        onnx.save(mdl, model_path)
        print(f"PATCHED: Added metadata to {model_path}: {', '.join(added)}")
    else:
        print(f"OK: {model_path} already has all metadata")


def main() -> None:
    if len(sys.argv) != 3:
        print(f"Usage: {sys.argv[0]} <model.onnx> <model.onnx.json>", file=sys.stderr)
        sys.exit(1)

    model_path = sys.argv[1]
    config_path = sys.argv[2]

    # Read config and build target metadata
    config = read_config(config_path)
    metadata = build_metadata(model_path, config)

    # Check current state
    existing = check_metadata(model_path)
    all_target_keys = set(metadata.keys())
    missing = all_target_keys - existing.keys()

    if missing:
        missing_required = REQUIRED_METADATA & missing
        missing_recommended = RECOMMENDED_METADATA & missing
        if missing_required:
            print(f"Missing REQUIRED metadata: {', '.join(missing_required)}")
        if missing_recommended:
            print(f"Missing recommended metadata: {', '.join(missing_recommended)}")
        patch_model(model_path, metadata)
    else:
        print(f"OK: {model_path} has all metadata")


if __name__ == "__main__":
    main()
