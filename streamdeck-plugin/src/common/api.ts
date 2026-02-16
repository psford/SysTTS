import { streamDeck } from "@elgato/streamdeck";

// BASE_URL defaults to localhost:5100 (matching SysTTS default service port).
// If SysTTS runs on a different machine or port, this will need to be updated.
const BASE_URL = "http://localhost:5100";

export type Voice = {
  id: string;
  name: string;
  sampleRate: number;
};

/**
 * Speak the currently selected text in the system
 * @param voice Optional voice ID to use for speech
 */
export async function speakSelection(voice?: string): Promise<void> {
  try {
    const response = await fetch(`${BASE_URL}/api/speak-selection`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ voice }),
    });

    if (!response.ok) {
      streamDeck.logger.error(
        `speakSelection failed: ${response.status} ${response.statusText}`
      );
    }
  } catch (error) {
    streamDeck.logger.error(
      `speakSelection error: ${error instanceof Error ? error.message : String(error)}`
    );
  }
}

/**
 * Speak custom text
 * @param text The text to speak
 * @param voice Optional voice ID to use for speech
 */
export async function speakText(text: string, voice?: string): Promise<void> {
  try {
    const response = await fetch(`${BASE_URL}/api/speak`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ text, voice }),
    });

    if (!response.ok) {
      streamDeck.logger.error(
        `speakText failed: ${response.status} ${response.statusText}`
      );
    }
  } catch (error) {
    streamDeck.logger.error(
      `speakText error: ${error instanceof Error ? error.message : String(error)}`
    );
  }
}

/**
 * Stop all ongoing speech
 */
export async function stopSpeaking(): Promise<void> {
  try {
    const response = await fetch(`${BASE_URL}/api/stop`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({}),
    });

    if (!response.ok) {
      streamDeck.logger.error(
        `stopSpeaking failed: ${response.status} ${response.statusText}`
      );
    }
  } catch (error) {
    streamDeck.logger.error(
      `stopSpeaking error: ${error instanceof Error ? error.message : String(error)}`
    );
  }
}

/**
 * Get available voices from the SysTTS service
 * @returns Array of available voices
 *
 * Note: The response is trusted as it comes from the local SysTTS service.
 * Validation is minimal since it's a trusted internal API.
 */
export async function getVoices(): Promise<Array<Voice>> {
  try {
    const response = await fetch(`${BASE_URL}/api/voices`, {
      method: "GET",
    });

    if (!response.ok) {
      streamDeck.logger.error(
        `getVoices failed: ${response.status} ${response.statusText}`
      );
      return [];
    }

    const voices = await response.json();
    return Array.isArray(voices) ? voices : [];
  } catch (error) {
    streamDeck.logger.error(
      `getVoices error: ${error instanceof Error ? error.message : String(error)}`
    );
    return [];
  }
}
