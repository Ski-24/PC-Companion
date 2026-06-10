import { action } from "@elgato/streamdeck";
import { BrightnessDialAction } from "./brightness-dial-action";
import { CommandAction, PcStatus } from "./command-action";

@action({ UUID: "com.abdulla.pccompanion.show-popup" })
export class ShowPopup extends CommandAction {
	protected readonly flag = "--show-popup";
	protected readonly label = "POPUP";
	protected readonly icon = "popup" as const;
}

@action({ UUID: "com.abdulla.pccompanion.toggle-gopher" })
export class ToggleGopher extends CommandAction {
	protected readonly flag = "--toggle-gopher";
	protected readonly label = "G360";
	protected readonly icon = "controller" as const;
	protected readonly statusKey = "gopher" as const;
}

@action({ UUID: "com.abdulla.pccompanion.switch-audio" })
export class SwitchAudio extends CommandAction {
	protected readonly flag = "--switch-audio";
	protected readonly label = "AUDIO";
	protected readonly icon = "headphones" as const;
	protected readonly statusKey = "audio" as const;
	protected readonly showPending = false;
}

@action({ UUID: "com.abdulla.pccompanion.prayer" })
export class Prayer extends CommandAction {
	protected readonly flag = "--show-popup";
	protected readonly label = "PRAYER";
	protected readonly icon = "prayer" as const;
	protected readonly statusKey = "prayerStatus" as const;
}

@action({ UUID: "com.abdulla.pccompanion.toggle-hdr" })
export class ToggleHdr extends CommandAction {
	protected readonly flag = "--toggle-hdr";
	protected readonly label = "HDR";
	protected readonly icon = "hdr" as const;
	protected readonly statusKey = "hdr" as const;
}

@action({ UUID: "com.abdulla.pccompanion.toggle-auto-hdr" })
export class ToggleAutoHdr extends CommandAction {
	protected readonly flag = "--toggle-auto-hdr";
	protected readonly label = "AUTO";
	protected readonly icon = "autoHdr" as const;
	protected readonly statusKey = "autoHdr" as const;
}

@action({ UUID: "com.abdulla.pccompanion.toggle-couch-mode" })
export class ToggleCouchMode extends CommandAction {
	protected readonly flag = "--toggle-couch-mode";
	protected readonly label = "COUCH";
	protected readonly icon = "couch" as const;
	protected readonly statusKey = "couchMode" as const;
}

@action({ UUID: "com.abdulla.pccompanion.toggle-morning-mode" })
export class ToggleMorningMode extends CommandAction {
	protected readonly flag = "--toggle-morning-mode";
	protected readonly label = "MORN";
	protected readonly icon = "morning" as const;
	protected readonly statusKey = "morningMode" as const;
}

@action({ UUID: "com.abdulla.pccompanion.dial-brightness" })
export class DialBrightness extends BrightnessDialAction {
	protected readonly label = "Brightness";
	protected readonly commandPrefix = "--set-brightness=";
	protected readonly step = 2;
	protected readonly min = 0;
	protected readonly max = 100;
	protected readonly unit = "%";
	protected readonly accent = "#20D6FF";

	protected valueFromStatus(status?: PcStatus): number {
		return typeof status?.displayBrightness === "number" ? status.displayBrightness : 50;
	}
}

@action({ UUID: "com.abdulla.pccompanion.dial-sdr" })
export class DialSdr extends BrightnessDialAction {
	protected readonly label = "SDR Balance";
	protected readonly commandPrefix = "--set-sdr=";
	protected readonly step = 0.1;
	protected readonly min = 3.0;
	protected readonly max = 6.0;
	protected readonly unit = "x";
	protected readonly accent = "#A8FFB8";

	protected valueFromStatus(status?: PcStatus): number {
		return typeof status?.sdrBrightness === "number" ? status.sdrBrightness : 3.0;
	}

	protected canRotate(status?: PcStatus): boolean {
		return status?.hdr === true;
	}

	protected override glyphMarkup(): string {
		// Contrast / brightness-balance symbol: a ring with the right half filled.
		return `<circle cx="36" cy="36" r="20" fill="none" stroke="${this.accent}" stroke-width="5"/>
			<path d="M36 16a20 20 0 0 1 0 40Z" fill="${this.accent}"/>`;
	}
}

@action({ UUID: "com.abdulla.pccompanion.dial-dim" })
export class DialDim extends BrightnessDialAction {
	protected readonly label = "Dim";
	protected readonly commandPrefix = "--set-dim=";
	protected readonly step = 2;
	protected readonly min = 0;
	protected readonly max = 100;
	protected readonly unit = "%";
	protected readonly accent = "#9B8CFF";

	protected valueFromStatus(status?: PcStatus): number {
		return typeof status?.dim === "number" ? status.dim : 0;
	}

	// Software dim is an SDR-mode tool (HDR uses the SDR Balance dial instead).
	protected canRotate(status?: PcStatus): boolean {
		return status?.hdr !== true;
	}

	protected override glyphMarkup(): string {
		// Crescent moon — "dim".
		return `<path d="M44 8a28 28 0 1 0 18 50A22 22 0 0 1 44 8Z" fill="${this.accent}"/>`;
	}
}

@action({ UUID: "com.abdulla.pccompanion.dial-background" })
export class DialBackground extends BrightnessDialAction {
	protected readonly label = "Background";
	protected readonly commandPrefix = "--set-bg-volume=";
	protected readonly step = 2;
	protected readonly min = 0;
	protected readonly max = 100;
	protected readonly unit = "%";
	protected readonly accent = "#35E06F";
	// Push toggles play/pause of the looping background sound.
	protected override readonly pushCommand = "--toggle-background";
	protected override readonly pushLabel = "Play / Pause";

	protected valueFromStatus(status?: PcStatus): number {
		return typeof status?.backgroundVolume === "number" ? status.backgroundVolume : 50;
	}

	protected override glyphMarkup(): string {
		// Musical note — "background sound".
		return `<path d="M50 12v34a12 12 0 1 1-6-10V20l-20 5v27a12 12 0 1 1-6-10V20Z" fill="${this.accent}"/>`;
	}
}
