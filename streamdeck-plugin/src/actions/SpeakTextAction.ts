import { SingletonAction, action, streamDeck } from "@elgato/streamdeck";
import type { KeyDownEvent } from "@elgato/streamdeck";
import { speakText } from "../common/api";

type Settings = {
  text?: string;
  voice?: string;
};

/**
 * Speak Custom Text action
 * Speaks configured static text with optional voice selection
 */
@action({ UUID: "com.systts.plugin.speak-text" })
export class SpeakTextAction extends SingletonAction<Settings> {
  /**
   * The Stream Deck plugin action has been pressed
   */
  async onKeyDown(ev: KeyDownEvent<Settings>): Promise<void> {
    const { text, voice } = ev.payload.settings;

    if (!text || text.trim().length === 0) {
      streamDeck.logger.warn("SpeakTextAction: No text configured");
      return;
    }

    await speakText(text, voice);
  }
}
