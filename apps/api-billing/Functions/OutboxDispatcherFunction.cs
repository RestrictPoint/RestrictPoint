using Microsoft.Azure.Functions.Worker;
using RestrictPoint.Api.Billing.Infrastructure;
using RestrictPoint.Database;

namespace RestrictPoint.Api.Billing.Functions;

/// <summary>Timer trigger hosting the shared outbox dispatcher for the Billing context.</summary>
public sealed class OutboxDispatcherFunction
{
    private readonly BillingDbContext _dbContext;
    private readonly OutboxDispatcher _dispatcher;

    public OutboxDispatcherFunction(BillingDbContext dbContext, OutboxDispatcher dispatcher)
    {
        _dbContext = dbContext;
        _dispatcher = dispatcher;
    }

    [Function("DispatchOutbox")]
    public Task RunAsync(
        [TimerTrigger("*/30 * * * * *")] TimerInfo timer,
        FunctionContext functionContext) =>
        _dispatcher.DispatchPendingAsync(_dbContext, functionContext.CancellationToken);
}
