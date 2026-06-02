import { DialAction, DialDownEvent, DialRotateEvent, SingletonAction, WillAppearEvent, WillDisappearEvent } from "@elgato/streamdeck";
import streamDeck from "@elgato/streamdeck";
import { spawn } from "node:child_process";
import { CommandSettings, onStatusChanged, PcStatus, readStatus, resolveExe } from "./command-action";

const activeDials = new Set<BrightnessDialAction>();

onStatusChanged(() => {
	for (const action of activeDials) {
		action.refreshFeedback().catch((err) => streamDeck.logger.debug("Dial refresh failed", err));
	}
});

export abstract class BrightnessDialAction extends SingletonAction<CommandSettings> {
	protected abstract readonly label: string;
	protected abstract readonly commandPrefix: string;
	protected abstract readonly step: number;
	protected abstract readonly min: number;
	protected abstract readonly max: number;
	protected abstract readonly unit: string;
	protected abstract readonly accent: string;
	protected canRotate(_status?: PcStatus): boolean {
		return true;
	}

	private readonly dials = new Set<DialAction<CommandSettings>>();
	private localValue: number | undefined;
	private optimisticUntil = 0;
	private queuedExePath: string | undefined;
	private queuedCommand: string | undefined;
	private commandTimer: ReturnType<typeof setTimeout> | undefined;
	private commandInFlight = false;

	override async onWillAppear(ev: WillAppearEvent<CommandSettings>): Promise<void> {
		const dial = ev.action as DialAction<CommandSettings>;
		this.dials.add(dial);
		activeDials.add(this);
		await dial.setFeedbackLayout("$B1");
		await dial.setTriggerDescription({
			rotate: `Adjust ${this.label}`,
			push: "Show popup",
		});
		await this.refreshFeedback();
	}

	override onWillDisappear(ev: WillDisappearEvent<CommandSettings>): void {
		this.dials.delete(ev.action as DialAction<CommandSettings>);
		if (this.dials.size === 0) activeDials.delete(this);
	}

	override async onDialRotate(ev: DialRotateEvent<CommandSettings>): Promise<void> {
		const status = await readStatus();
		if (!this.canRotate(status)) {
			this.optimisticUntil = 0;
			await this.refreshFeedback();
			return;
		}
		const base = this.localValue ?? this.valueFromStatus(status);
		const next = this.clamp(base + ev.payload.ticks * this.step);
		this.localValue = next;
		this.optimisticUntil = Date.now() + 2500;
		await this.setFeedback(next);
		this.queueCommand(ev.payload.settings.exePath, `${this.commandPrefix}${this.formatCommandValue(next)}`);
		setTimeout(() => this.refreshFeedback().catch(() => undefined), 900).unref();
	}

	override async onDialDown(ev: DialDownEvent<CommandSettings>): Promise<void> {
		this.spawnCommand(ev.payload.settings.exePath, "--show-popup");
	}

	async refreshFeedback(): Promise<void> {
		const status = await readStatus();
		const value = this.valueFromStatus(status);
		if (Date.now() < this.optimisticUntil && this.localValue !== undefined && Math.abs(value - this.localValue) > this.step / 2) {
			await this.setFeedback(this.localValue);
			return;
		}
		this.localValue = value;
		this.optimisticUntil = 0;
		await this.setFeedback(value);
	}

	protected abstract valueFromStatus(status?: PcStatus): number;

	private async setFeedback(value: number): Promise<void> {
		const clamped = this.clamp(value);
		const display = this.formatValue(clamped);
		await Promise.all([...this.dials].map((dial) => dial.setFeedback({
			title: this.label,
			value: display,
			indicator: Math.round(((clamped - this.min) / (this.max - this.min)) * 100),
			icon: this.iconSvg(),
		})));
	}

	private spawnCommand(exePath: string | undefined, command: string): void {
		const exe = resolveExe(exePath);
		try {
			const child = spawn(exe, [command], { detached: true, stdio: "ignore" });
			child.on("error", (err) => streamDeck.logger.error(`Failed to launch PC Companion (${exe}) for ${command}`, err));
			child.unref();
		} catch (err) {
			streamDeck.logger.error(`Exception launching PC Companion for ${command}`, err);
		}
	}

	private queueCommand(exePath: string | undefined, command: string): void {
		this.queuedExePath = exePath;
		this.queuedCommand = command;
		if (this.commandTimer) clearTimeout(this.commandTimer);
		this.commandTimer = setTimeout(() => {
			const queuedCommand = this.queuedCommand;
			const queuedExePath = this.queuedExePath;
			this.commandTimer = undefined;
			this.queuedCommand = undefined;
			this.queuedExePath = undefined;
			if (queuedCommand) this.spawnQueuedCommand(queuedExePath, queuedCommand);
		}, 75);
		this.commandTimer.unref();
	}

	private spawnQueuedCommand(exePath: string | undefined, command: string): void {
		this.queuedExePath = exePath;
		this.queuedCommand = command;
		if (this.commandInFlight) return;
		this.flushQueuedCommand();
	}

	private flushQueuedCommand(): void {
		const command = this.queuedCommand;
		const exePath = this.queuedExePath;
		this.queuedCommand = undefined;
		this.queuedExePath = undefined;
		if (!command) return;

		const exe = resolveExe(exePath);
		this.commandInFlight = true;
		try {
			const child = spawn(exe, [command], { stdio: "ignore" });
			let finished = false;
			const done = () => {
				if (finished) return;
				finished = true;
				this.commandInFlight = false;
				if (this.queuedCommand) this.flushQueuedCommand();
			};
			child.once("error", (err) => {
				streamDeck.logger.error(`Failed to launch PC Companion (${exe}) for ${command}`, err);
				done();
			});
			child.once("close", done);
		} catch (err) {
			this.commandInFlight = false;
			streamDeck.logger.error(`Exception launching PC Companion for ${command}`, err);
			if (this.queuedCommand) this.flushQueuedCommand();
		}
	}

	private clamp(value: number): number {
		return Math.min(this.max, Math.max(this.min, value));
	}

	private formatValue(value: number): string {
		return this.unit === "%" ? `${Math.round(value)}%` : value.toFixed(1);
	}

	private formatCommandValue(value: number): string {
		return this.unit === "%" ? `${Math.round(value)}` : value.toFixed(1);
	}

	protected glyphMarkup(): string {
		// Default: a sun (used by the brightness dial).
		return `<circle cx="36" cy="36" r="11" fill="${this.accent}"/>
			<path d="M36 6v9M36 57v9M6 36h9M57 36h9M16 16l6 6M50 50l6 6M56 16l-6 6M16 56l6-6" stroke="${this.accent}" stroke-width="5" stroke-linecap="round"/>`;
	}

	private iconSvg(): string {
		const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="72" height="72" viewBox="0 0 72 72">${this.glyphMarkup()}</svg>`;
		return `data:image/svg+xml;charset=utf8,${encodeURIComponent(svg)}`;
	}
}
