import { streamDeck } from "@elgato/streamdeck";
import { SpeakSelectionAction } from "./actions/SpeakSelectionAction";
import { SpeakTextAction } from "./actions/SpeakTextAction";
import { StopSpeakingAction } from "./actions/StopSpeakingAction";

/**
 * The Stream Deck plugin initializer
 * Registers all available actions and connects to the Stream Deck app
 */

// Register all action classes
streamDeck.actions.registerAction(new SpeakSelectionAction());
streamDeck.actions.registerAction(new SpeakTextAction());
streamDeck.actions.registerAction(new StopSpeakingAction());

// Connect to the Stream Deck application
streamDeck.connect();
