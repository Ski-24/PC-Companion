import { KeyAction, KeyDownEvent, SingletonAction, WillAppearEvent, WillDisappearEvent } from "@elgato/streamdeck";
import streamDeck from "@elgato/streamdeck";
import { spawn } from "node:child_process";
import { existsSync, watch } from "node:fs";
import { readFile } from "node:fs/promises";
import { dirname, join } from "node:path";

// The installed PC Companion lives in the per-user install dir "PC Companion" (with a
// space); the binary itself is PCCompanion.exe (no space — see <AssemblyName>). This is
// the only location the shipped plugin should assume. (Note the app's *data* dir is the
// separate "PCCompanion" folder used for status.json below.)
export const DEFAULT_EXE = join(process.env.LOCALAPPDATA ?? "", "PC Companion", "PCCompanion.exe");

// Old saved action settings may still point at a dev tree (Debug/Release/source/publish).
// Treat those — and any path that no longer exists — as invalid and fall back to the
// installed default, so existing Stream Deck profiles self-heal after install.
const DEV_PATH_RE = /[\\/](?:bin[\\/](?:Debug|Release)|PC-Control-App|publish)[\\/]/i;

export function resolveExe(savedPath?: string): string {
	const trimmed = savedPath?.trim();
	if (trimmed && !DEV_PATH_RE.test(trimmed) && existsSync(trimmed)) return trimmed;
	return DEFAULT_EXE;
}

export type CommandSettings = {
	exePath?: string;
};

export type PcStatus = {
	gopher?: boolean;
	hdr?: boolean;
	autoHdr?: boolean | null;
	couchMode?: boolean;
	morningMode?: boolean;
	audio?: string;
	audioDeviceIndex?: number;
	device1?: string;
	device2?: string;
	displayBrightness?: number | null;
	sdrBrightness?: number;
	dim?: number;
	backgroundVolume?: number;
	backgroundPlaying?: boolean;
	prayerStatus?: string;
	prayerCountdown?: string;
};

export const STATUS_PATH = join(process.env.LOCALAPPDATA ?? "", "PCCompanion", "Config", "status.json");
const activeActions = new Set<CommandAction>();
const SVG_PREFIX = "data:image/svg+xml;charset=utf8,";
const statusListeners = new Set<() => void>();
let notifyTimer: ReturnType<typeof setTimeout> | undefined;
let lastButtonFingerprint = "";
let lastGoodStatus: PcStatus | undefined;

export async function readStatus(): Promise<PcStatus | undefined> {
	try {
		lastGoodStatus = JSON.parse(await readFile(STATUS_PATH, "utf8")) as PcStatus;
		return lastGoodStatus;
	} catch {
		return lastGoodStatus;
	}
}

setInterval(() => {
	notifyStatusChanged();
}, 2000).unref();

try {
	watch(dirname(STATUS_PATH), { persistent: false }, (_event, fileName) => {
		if (!fileName || fileName.toString().toLowerCase() === "status.json") {
			notifyStatusChanged();
		}
	});
} catch (err) {
	streamDeck.logger.debug("Status watcher unavailable", err);
}

export function onStatusChanged(listener: () => void): () => void {
	statusListeners.add(listener);
	return () => statusListeners.delete(listener);
}

function notifyStatusChanged(): void {
	if (notifyTimer) return;
	notifyTimer = setTimeout(async () => {
		notifyTimer = undefined;
		const status = await readStatus();
		const buttonFingerprint = fingerprintButtonStatus(status);
		if (buttonFingerprint !== lastButtonFingerprint) {
			lastButtonFingerprint = buttonFingerprint;
			for (const action of activeActions) {
				action.refreshTitle().catch((err) => streamDeck.logger.debug("Status refresh failed", err));
			}
		}
		for (const listener of statusListeners) listener();
	}, 75);
	notifyTimer.unref();
}

function fingerprintButtonStatus(status?: PcStatus): string {
	if (!status) return "";
	return JSON.stringify({
		gopher: status.gopher,
		hdr: status.hdr,
		autoHdr: status.autoHdr,
		couchMode: status.couchMode,
		morningMode: status.morningMode,
		audio: status.audio,
		audioDeviceIndex: status.audioDeviceIndex,
		device1: status.device1,
		device2: status.device2,
		prayerStatus: status.prayerStatus,
		prayerCountdown: status.prayerCountdown,
	});
}

export abstract class CommandAction extends SingletonAction<CommandSettings> {
	protected abstract readonly flag: string;
	protected abstract readonly label: string;
	protected abstract readonly icon: IconName;
	protected readonly statusKey?: keyof PcStatus;
	protected readonly showPending: boolean = false;

	private readonly keys = new Set<KeyAction<CommandSettings>>();

	override async onWillAppear(ev: WillAppearEvent<CommandSettings>): Promise<void> {
		this.keys.add(ev.action as KeyAction<CommandSettings>);
		activeActions.add(this);
		await this.refreshTitle();
	}

	override onWillDisappear(ev: WillDisappearEvent<CommandSettings>): void {
		this.keys.delete(ev.action as KeyAction<CommandSettings>);
		if (this.keys.size === 0) activeActions.delete(this);
	}

	override async onKeyDown(ev: KeyDownEvent<CommandSettings>): Promise<void> {
		const exe = resolveExe(ev.payload.settings.exePath);
		await ev.action.setTitle("");
		if (this.showPending) {
			await ev.action.setImage(renderIcon(this.icon, this.label, { tone: "pending", stateText: "...", detailText: "" }));
		}

		try {
			const child = spawn(exe, [this.flag], { detached: true, stdio: "ignore" });
			child.on("error", (err) => {
				streamDeck.logger.error(`Failed to launch PC Companion (${exe}) for ${this.flag}`, err);
				ev.action.showAlert();
			});
			child.unref();

			setTimeout(() => this.refreshTitle().catch(() => undefined), 1400).unref();
		} catch (err) {
			streamDeck.logger.error(`Exception launching PC Companion for ${this.flag}`, err);
			await ev.action.showAlert();
		}
	}

	async refreshTitle(): Promise<void> {
		const status = await readStatus();
		const state = this.stateFromStatus(status);
		const image = renderIcon(this.icon, this.label, state);
		await Promise.all([...this.keys].map((key) => Promise.all([
			key.setImage(image),
			key.setTitle(""),
		])));
	}

	private stateFromStatus(status?: PcStatus): IconState {
		if (!this.statusKey) return { tone: "neutral", stateText: "", detailText: "" };
		if (!status) return { tone: "unknown", stateText: "--", detailText: "" };

		const value = status[this.statusKey];
		if (this.statusKey === "audio") {
			const audio = String(value || "--");
			return {
				tone: audioTone(status, audio),
				stateText: "",
				detailText: shortAudio(displayAudioName(audio)),
			};
		}
		if (this.statusKey === "prayerStatus") {
			return {
				tone: "prayer",
				stateText: "",
				detailText: prayerLines(status.prayerStatus, status.prayerCountdown),
			};
		}
		if (value === null || value === undefined) return { tone: "unknown", stateText: "N/A", detailText: "" };
		return { tone: value ? "on" : "off", stateText: value ? "ON" : "OFF", detailText: "" };
	}
}

function shortAudio(label: string): string {
	if (label.length <= 13) return label;
	return `${label.slice(0, 12)}...`;
}

function displayAudioName(label: string): string {
	const paren = label.match(/\(([^)]+)\)/);
	let name = (paren?.[1] ?? label)
		.replace(/^\s*\d+\s*-\s*/, "")
		.replace(/\b(Headphones?|Headset Earphone|Speakers?)\b/gi, "")
		.replace(/\s+/g, " ")
		.trim();

	if (/A50\s*X/i.test(name)) return "A50 X";
	if (/Fosi\s+Audio/i.test(name)) return "Fosi Audio";
	return name || label;
}

function prayerLines(status?: string, countdown?: string): string {
	const cleanStatus = (status || "Prayer")
		.replace(/\s+/g, " ")
		.trim();
	const match = cleanStatus.match(/^([A-Za-z]+)(?:\s+Iqama)?\s+(.+)$/i);
	const name = match?.[1] ?? cleanStatus;
	const time = match?.[2] ?? "";
	const cleanCountdown = (countdown || "").replace(/\s+/g, " ").trim();
	return [name, time, cleanCountdown].filter(Boolean).join("\n");
}

function audioTone(status: PcStatus, audio: string): IconTone {
	if (status.audioDeviceIndex === 1) return "audioOne";
	if (status.audioDeviceIndex === 2) return "audioTwo";
	if (sameAudio(audio, status.device1)) return "audioOne";
	if (sameAudio(audio, status.device2)) return "audioTwo";
	return "neutral";
}

function sameAudio(current: string, configured?: string): boolean {
	if (!configured) return false;
	const c = normalizeAudio(current);
	const target = normalizeAudio(configured);
	return c === target || c.includes(target) || target.includes(c);
}

function normalizeAudio(value: string): string {
	return value.toLowerCase().replace(/[^a-z0-9]+/g, "");
}

type IconName = "popup" | "controller" | "headphones" | "hdr" | "autoHdr" | "couch" | "morning" | "prayer";
type IconTone = "on" | "off" | "neutral" | "unknown" | "pending" | "audioOne" | "audioTwo" | "prayer";
type IconState = { tone: IconTone; stateText: string; detailText: string };

function renderIcon(icon: IconName, label: string, state: IconState): string {
	const accent = state.tone === "on" ? "#35E06F"
		: state.tone === "off" ? "#FF4D5A"
		: state.tone === "pending" ? "#FFD23F"
		: state.tone === "audioOne" ? "#B36BFF"
		: state.tone === "audioTwo" ? "#FFD23F"
		: state.tone === "prayer" ? "#F8D878"
		: state.tone === "unknown" ? "#8A909B"
		: "#20D6FF";
	const dim = "none";

	const maxLines = icon === "prayer" ? 3 : 2;
	const lines = state.detailText.split("\n").filter((line) => line.trim()).slice(0, maxLines);
	// When a glyph has a detail label (e.g. the audio device name), lift + shrink the
	// glyph so the label gets a clear, readable band at the bottom of the key.
	const hasDetail = icon !== "prayer" && lines.length > 0;
	const rawArt = icon === "prayer" ? "" : iconSvg(icon, accent, dim);
	const art = hasDetail
		? `<g transform="translate(72 54) scale(0.74) translate(-72 -67)">${rawArt}</g>`
		: rawArt;

	const detail = lines.map((line, index) => {
		const y = icon === "prayer"
			? 48 + index * 31
			: hasDetail
				? lines.length === 1 ? 120 : 110 + index * 20
				: lines.length === 1 ? 126 : 118 + index * 16;
		const size = icon === "prayer"
			? index === 0 ? 26 : 20
			: hasDetail
				? index === 0 ? 22 : 17
				: index === 0 ? 16 : 13;
		const fill = icon === "prayer" && index === 0 ? accent : "#F6F8FB";
		return `<text x="72" y="${y}" text-anchor="middle" font-family="Segoe UI, Arial, sans-serif" font-size="${size}" font-weight="900" fill="${fill}">${escapeXml(line)}</text>`;
	}).join("");
	const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="144" height="144" viewBox="0 0 144 144">
		${art}
		${detail}
	</svg>`;
	return `${SVG_PREFIX}${encodeURIComponent(svg)}`;
}

function iconSvg(icon: IconName, accent: string, dim: string): string {
	switch (icon) {
		case "popup":
			return `<path d="M43 38h58a7 7 0 0 1 7 7v38a7 7 0 0 1-7 7H82l-10 15-10-15H43a7 7 0 0 1-7-7V45a7 7 0 0 1 7-7Z" fill="${dim}" stroke="${accent}" stroke-width="7" stroke-linejoin="round"/>
				<path d="M52 55h40M52 69h28" stroke="${accent}" stroke-width="5" stroke-linecap="round" opacity=".9"/>`;
		case "controller":
			return `<path d="M27 67C29 48 41 39 59 42L66 43C70 43.6 74 43.6 78 43L85 42C103 39 115 48 117 67L121 93C123 105 112 112 103 105L88 93L56 93L41 105C32 112 21 105 23 93Z" fill="${dim}" stroke="${accent}" stroke-width="7" stroke-linejoin="round"/>
				<path d="M42 71h20M52 61v20" stroke="${accent}" stroke-width="7" stroke-linecap="round"/>
				<circle cx="93" cy="61.5" r="4.5" fill="${accent}"/><circle cx="93" cy="80.5" r="4.5" fill="${accent}"/><circle cx="83.5" cy="71" r="4.5" fill="${accent}"/><circle cx="102.5" cy="71" r="4.5" fill="${accent}"/>`;
		case "headphones":
			return `<path d="M31 77V64c0-24 18-42 41-42s41 18 41 42v13" fill="none" stroke="${accent}" stroke-width="9" stroke-linecap="round"/>
				<rect x="22" y="75" width="22" height="35" rx="9" fill="${dim}" stroke="${accent}" stroke-width="7"/>
				<rect x="100" y="75" width="22" height="35" rx="9" fill="${dim}" stroke="${accent}" stroke-width="7"/>`;
		case "hdr":
			return `<g transform="translate(0 7)"><circle cx="72" cy="62" r="24" fill="${dim}" stroke="${accent}" stroke-width="5"/>
				<path d="M72 30V14M72 94v16M40 62H24M104 62h16M49 39 38 28M95 39l11-11M49 85 38 96M95 85l11 11" stroke="${accent}" stroke-width="6" stroke-linecap="round"/>
				<text x="72" y="69" text-anchor="middle" font-family="Segoe UI, Arial, sans-serif" font-size="18" font-weight="900" fill="${accent}">HDR</text></g>`;
		case "autoHdr":
			return `<circle cx="72" cy="56" r="23" fill="${dim}" stroke="${accent}" stroke-width="6"/>
				<text x="72" y="66" text-anchor="middle" font-family="Segoe UI, Arial, sans-serif" font-size="30" font-weight="900" fill="${accent}">A</text>
				<text x="72" y="105" text-anchor="middle" font-family="Segoe UI, Arial, sans-serif" font-size="23" font-weight="900" fill="${accent}">HDR</text>`;
		case "couch":
			return `<path d="M35 66h74a14 14 0 0 1 14 14v25H21V80a14 14 0 0 1 14-14Z" fill="${dim}" stroke="${accent}" stroke-width="8" stroke-linejoin="round"/>
				<path d="M42 46h60a13 13 0 0 1 13 13v20H29V59a13 13 0 0 1 13-13Z" fill="${dim}" stroke="${accent}" stroke-width="8" stroke-linejoin="round"/>
				<path d="M34 108v10M110 108v10" stroke="${accent}" stroke-width="7" stroke-linecap="round"/>`;
		case "morning":
			return `<path d="M40 99A34 34 0 1 1 104 99Z" fill="${dim}" stroke="${accent}" stroke-width="7" stroke-linejoin="round"/>
				<path d="M20 99h104M40 113h64" stroke="${accent}" stroke-width="7" stroke-linecap="round"/>
				<path d="M72 50V38M56 54L51 43M88 54L93 43M43 64L34 56M101 64L110 56M35 78L24 75M109 78L120 75" stroke="${accent}" stroke-width="6" stroke-linecap="round"/>`;
		case "prayer":
			return `<path d="M34 101V57h13v44M97 101V57h13v44M46 101V72c0-15 12-27 26-27s26 12 26 27v29" fill="${dim}" stroke="${accent}" stroke-width="7" stroke-linejoin="round"/>
				<path d="M57 101V77a15 15 0 0 1 30 0v24" fill="#171C24" stroke="${accent}" stroke-width="6" stroke-linejoin="round"/>
				<path d="M32 101h80M40 47l7-13 7 13M90 47l7-13 7 13M72 31v13" stroke="${accent}" stroke-width="6" stroke-linecap="round" stroke-linejoin="round"/>`;
	}
}

function escapeXml(value: string): string {
	return value.replace(/[<>&'"]/g, (ch) => ({
		"<": "&lt;",
		">": "&gt;",
		"&": "&amp;",
		"'": "&apos;",
		'"': "&quot;",
	}[ch] ?? ch));
}
