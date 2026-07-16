using Microsoft.Azure.Functions.Worker;
using RestrictPoint.Api.Marketplace.Infrastructure;
using RestrictPoint.Database;

namespace RestrictPoint.Api.Marketplace.Functions;

/// <summary>Timer trigger hosting the shared outbox dispatcher for the Marketplace context.</summary>
public sealed class OutboxDispatcherFunction
{
    private readonly MarketplaceDbContext _dbContext;
    private readonly OutboxDispatcher _dispatcher;

    public OutboxDispatcherFunction(MarketplaceDbContext dbContext, OutboxDispatcher dispatcher)
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
