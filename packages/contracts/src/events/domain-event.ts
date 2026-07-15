/**
 * Standard event envelope for all RestrictPoint domain events.
 *
 * Defined by docs/20-Event-Catalog.md. No event may remove fields
 * from this envelope.
 */
export interface DomainEvent<TPayload> {
  /** Unique GUID for this event instance. */
  eventId: string;

  /** Business event name, e.g. "LicenseIssued". */
  eventType: string;

  /** Schema version, e.g. "1.0". */
  eventVersion: string;

  /** UTC timestamp when the business fact occurred. */
  occurredUtc: string;

  /** Correlation identifier shared across a business workflow. */
  correlationId: string;

  /** eventId of the event that caused this event, if any. */
  causationId?: string;

  /** Publisher organization identifier. */
  organizationId: string;

  /** Customer tenant identifier, when applicable. */
  tenantId?: string;

  /** Publishing service name. */
  publisher: string;

  /** Immutable, self-contained event payload. */
  payload: TPayload;
}
