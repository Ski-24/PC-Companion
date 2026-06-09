import streamDeck from "@elgato/streamdeck";

import { DialBrightness, DialDim, DialSdr, Prayer, ShowPopup, SwitchAudio, ToggleAutoHdr, ToggleCouchMode, ToggleGopher, ToggleHdr, ToggleMorningMode } from "./actions";

streamDeck.actions.registerAction(new ShowPopup());
streamDeck.actions.registerAction(new ToggleGopher());
streamDeck.actions.registerAction(new SwitchAudio());
streamDeck.actions.registerAction(new Prayer());
streamDeck.actions.registerAction(new ToggleHdr());
streamDeck.actions.registerAction(new ToggleAutoHdr());
streamDeck.actions.registerAction(new ToggleCouchMode());
streamDeck.actions.registerAction(new ToggleMorningMode());
streamDeck.actions.registerAction(new DialBrightness());
streamDeck.actions.registerAction(new DialSdr());
streamDeck.actions.registerAction(new DialDim());

streamDeck.connect();
