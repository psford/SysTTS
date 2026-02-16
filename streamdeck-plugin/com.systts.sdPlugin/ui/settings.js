// Property Inspector script for SysTTS actions
const SPEAK_TEXT_ACTION_UUID = "com.systts.plugin.speak-text";
const API_BASE_URL = "http://localhost:5100";

let currentActionUUID = null;
let currentSettings = {};
let websocket = null;
let websocketUUID = null;
let websocketRegistrationEvent = null;

/**
 * Stream Deck calls this function automatically when loading the Property Inspector.
 * This is the callback pattern used by Stream Deck SDK v2.
 */
function connectElgatoStreamDeckSocket(inPort, inPropertyInspectorUUID, inRegisterEvent, inInfo, inActionInfo) {
  // Store UUID and registration event for WebSocket
  websocketUUID = inPropertyInspectorUUID;
  websocketRegistrationEvent = inRegisterEvent;

  // Open WebSocket connection
  websocket = new WebSocket('ws://127.0.0.1:' + inPort);

  websocket.onopen = function () {
    // Register with Stream Deck
    websocket.send(JSON.stringify({ event: inRegisterEvent, uuid: inPropertyInspectorUUID }));
  };

  websocket.onmessage = function (evt) {
    const data = JSON.parse(evt.data);
    if (data.event === 'didReceiveSettings') {
      if (data.payload && data.payload.settings) {
        currentSettings = data.payload.settings;
        updateUIFromSettings();
      }
    }
  };

  websocket.onerror = function (error) {
    console.error('WebSocket error:', error);
  };

  // Extract action UUID from the inActionInfo parameter
  const actionInfo = JSON.parse(inActionInfo);
  currentActionUUID = actionInfo.action;

  // Determine if this is the speak-text action
  const isSpeakTextAction = currentActionUUID === SPEAK_TEXT_ACTION_UUID;

  // Show/hide text field based on action type
  const textSection = document.getElementById('text-section');
  textSection.style.display = isSpeakTextAction ? 'block' : 'none';

  // Load initial settings from actionInfo payload
  if (actionInfo.payload && actionInfo.payload.settings) {
    currentSettings = actionInfo.payload.settings;
    updateUIFromSettings();
  }

  // Load voices from API
  loadVoices();
}

/**
 * Load voices from the SysTTS API and populate the dropdown
 */
async function loadVoices() {
  const voiceSelect = document.getElementById('voice-select');

  try {
    const response = await fetch(`${API_BASE_URL}/api/voices`);

    if (!response.ok) {
      throw new Error(`HTTP ${response.status}`);
    }

    const voices = await response.json();

    if (!Array.isArray(voices) || voices.length === 0) {
      voiceSelect.innerHTML =
        '<option value="">No voices available</option>';
      return;
    }

    // Clear loading option
    voiceSelect.innerHTML = '';

    // Add default/empty option
    const defaultOption = document.createElement('option');
    defaultOption.value = '';
    defaultOption.textContent = 'Default Voice';
    voiceSelect.appendChild(defaultOption);

    // Add voice options
    voices.forEach((voice) => {
      const option = document.createElement('option');
      option.value = voice.id;
      option.textContent = voice.name;
      voiceSelect.appendChild(option);
    });

    // Restore saved voice selection
    if (currentSettings.voice) {
      voiceSelect.value = currentSettings.voice;
    }
  } catch (error) {
    console.error('Failed to load voices:', error);
    voiceSelect.innerHTML =
      '<option value="">Service unavailable</option>';
  }
}

/**
 * Update the UI controls from current settings
 */
function updateUIFromSettings() {
  const voiceSelect = document.getElementById('voice-select');
  const textInput = document.getElementById('text-input');

  if (currentSettings.voice) {
    voiceSelect.value = currentSettings.voice;
  }

  if (currentSettings.text !== undefined) {
    textInput.value = currentSettings.text;
  }
}

/**
 * Save settings via WebSocket
 */
function saveSettings(settings) {
  if (websocket && websocket.readyState === WebSocket.OPEN) {
    websocket.send(JSON.stringify({
      event: 'setSettings',
      context: websocketUUID,
      payload: settings
    }));
  } else {
    console.warn('WebSocket not ready, cannot save settings');
  }
}

/**
 * Save settings when voice dropdown changes
 */
document.getElementById('voice-select').addEventListener('change', function (e) {
  const newSettings = {
    ...currentSettings,
    voice: e.target.value === '' ? undefined : e.target.value,
  };

  saveSettings(newSettings);
  currentSettings = newSettings;
});

/**
 * Save settings when text textarea changes
 */
document.getElementById('text-input').addEventListener('change', function (e) {
  const newSettings = {
    ...currentSettings,
    text: e.target.value,
  };

  saveSettings(newSettings);
  currentSettings = newSettings;
});
