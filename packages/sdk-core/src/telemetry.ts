/**
 * SDK telemetry (docs/14): batched, fire-and-forget, never blocks or throws into the
 * host application. Events are delivered through an injectable transport; when none is
 * configured the queue is a bounded in-memory buffer (inspectable, dropped on overflow).
 */

export type TelemetryEventName =
  | "licenseValidated"
  | "licenseRejected"
  | "featureAccessed"
  | "fallbackTriggered"
  | "offlineModeActivated";

export interface TelemetryEvent {
  name: TelemetryEventName;
  occurredUtc: string;
  properties: Record<string, string>;
}

/** Delivers a batch of events. Implementations must not throw. */
export type TelemetryTransport = (events: readonly TelemetryEvent[]) => Promise<void> | void;

export class TelemetryQueue {
  private static readonly MaxBuffered = 100;
  private static readonly FlushThreshold = 20;

  private buffer: TelemetryEvent[] = [];

  public constructor(
    private readonly transport: TelemetryTransport | null,
    private readonly clock: () => number = () => Date.now(),
  ) {}

  public emit(name: TelemetryEventName, properties: Record<string, string> = {}): void {
    if (this.buffer.length >= TelemetryQueue.MaxBuffered) {
      this.buffer.shift(); // Bounded: oldest events are dropped, the host app never pays.
    }

    this.buffer.push({
      name,
      occurredUtc: new Date(this.clock()).toISOString(),
      properties,
    });

    if (this.transport && this.buffer.length >= TelemetryQueue.FlushThreshold) {
      void this.flush();
    }
  }

  /** Sends all buffered events. Safe to call at any time. */
  public async flush(): Promise<void> {
    if (!this.transport || this.buffer.length === 0) {
      return;
    }

    const batch = this.buffer;
    this.buffer = [];

    try {
      await this.transport(batch);
    } catch {
      // Telemetry must never surface errors into the host application.
    }
  }

  /** Buffered events (primarily for tests and diagnostics). */
  public get pending(): readonly TelemetryEvent[] {
    return this.buffer;
  }
}
