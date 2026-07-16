using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RestrictPoint.Api.Identity.Infrastructure;
using RestrictPoint.Messaging;

namespace RestrictPoint.Api.Identity.Functions;

/// <summary>
/// Dispatches pending outbox rows to Service Bus every 30 seconds. Rows are dispatched
/// in occurrence order; a failed row does not block later rows destined for other topics
/// but is retried with its attempt count recorded for monitoring.
/// Duplicate publishes are possible by design (at-least-once); consumers deduplicate by
/// eventId and Service Bus duplicate detection uses the message id.
/// </summary>
public sealed partial class OutboxDispatcherFunction
{
    private const int BatchSize = 50;
    private const int MaxAttemptsBeforeAlert = 10;

    private readonly IdentityDbContext _dbContext;
    private readonly IEventPublisher _publisher;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OutboxDispatcherFunction> _logger;

    public OutboxDispatcherFunction(
        IdentityDbContext dbContext,
        IEventPublisher publisher,
        TimeProvider timeProvider,
        ILogger<OutboxDispatcherFunction> logger)
    {
        _dbContext = dbContext;
        _publisher = publisher;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    [Function("DispatchOutbox")]
    public async Task RunAsync(
        [TimerTrigger("*/30 * * * * *")] TimerInfo timer,
        FunctionContext functionContext)
    {
        var cancellationToken = functionContext.CancellationToken;

        var pending = await _dbContext.OutboxMessages
            .Where(m => m.DispatchedUtc == null)
            .OrderBy(m => m.OccurredUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            return;
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

                await _publisher.PublishAsync(message.Topic, envelope, cancellationToken);

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

        await _dbContext.SaveChangesAsync(cancellationToken);

        LogDispatchCycle(_logger, dispatched, pending.Count);
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
