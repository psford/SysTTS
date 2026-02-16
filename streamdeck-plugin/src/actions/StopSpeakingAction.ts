import { SingletonAction, action } from "@elgato/streamdeck";
import type { KeyDownEvent } from "@elgato/streamdeck";
import { stopSpeaking } from "../common/api";

/**
 * Stop Speaking action
 * Stops all ongoing speech
 */
@action({ UUID: "com.systts.plugin.stop-speaking" })
export class StopSpeakingAction extends SingletonAction {
  /**
   * The Stream Deck plugin action has been pressed
   */
  async onKeyDown(ev: KeyDownEvent): Promise<void> {
    await stopSpeaking();
  }
}
