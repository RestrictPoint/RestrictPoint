using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RestrictPoint.Messaging;

namespace RestrictPoint.Database;

/// <summary>
/// Dispatches pending outbox rows to Service Bus in occurrence order. At-least-once by
/// design: consumers deduplicate by eventId and Service Bus duplicate detection uses the
/// message id. A failed row is retried with its attempt count recorded for monitoring.
/// Hosted by each service's timer trigger.
/// </summary>
public sealed partial class OutboxDispatcher
{
    private const int BatchSize = 50;
    private const int MaxAttemptsBeforeAlert = 10;

    private readonly IEventPublisher _publisher;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OutboxDispatcher> _logger;

    public OutboxDispatcher(
        IEventPublisher publisher,
        TimeProvider timeProvider,
        ILogger<OutboxDispatcher> logger)
    {
        _publisher = publisher;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>Dispatches one batch of pending messages from the given context.</summary>
    public async Task<int> DispatchPendingAsync(DbContext dbContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var pending = await dbContext.Set<OutboxMessage>()
            .Where(m => m.DispatchedUtc == null)
            .OrderBy(m => m.OccurredUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (pending.Count == 0)
        {
            return 0;
        }

        var dispatched = 0;

        foreach (var message in pending)
        {
            try
            {
                var envelope = JsonSerializer.Deserialize<DomainEventEnvelope>(
                    message.Payload,
                    DomainEventEnvelope.SerializerOptions)
                    ?? throw new JsonException("Outbox payload deserialized to null.");

                await _publisher.PublishAsync(message.Topic, envelope, cancellationToken)
                    .ConfigureAwait(false);

                message.DispatchedUtc = _timeProvider.GetUtcNow();
                message.LastError = null;
                dispatched++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                message.AttemptCount++;
                message.LastError = Truncate(exception.Message, 1024);

                if (message.AttemptCount >= MaxAttemptsBeforeAlert)
                {
                    LogDispatchExhausted(_logger, exception, message.Id, message.EventType, message.AttemptCount);
                }
                else
                {
                    LogDispatchRetry(_logger, exception, message.AttemptCount, message.Id, message.EventType);
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        LogDispatchCycle(_logger, dispatched, pending.Count);
        return dispatched;
    }

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Outbox message {MessageId} ({EventType}) failed {AttemptCount} dispatch attempts.")]
    private static partial void LogDispatchExhausted(
        ILogger logger, Exception exception, Guid messageId, string eventType, int attemptCount);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Outbox dispatch attempt {AttemptCount} failed for message {MessageId} ({EventType}).")]
    private static partial void LogDispatchRetry(
        ILogger logger, Exception exception, int attemptCount, Guid messageId, string eventType);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Outbox dispatch cycle complete: {Dispatched}/{Pending} messages published.")]
    private static partial void LogDispatchCycle(ILogger logger, int dispatched, int pending);

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
