import { SingletonAction, action } from "@elgato/streamdeck";
import type { KeyDownEvent } from "@elgato/streamdeck";
import { speakSelection } from "../common/api";

interface Settings {
  voice?: string;
}

/**
 * Speak Selected Text action
 * Captures currently selected text and speaks it aloud
 */
@action({ UUID: "com.systts.plugin.speak-selection" })
export class SpeakSelectionAction extends SingletonAction<Settings> {
  /**
   * The Stream Deck plugin action has been pressed
   */
  async onKeyDown(ev: KeyDownEvent<Settings>): Promise<void> {
    await speakSelection(ev.payload.settings.voice);
  }
}
