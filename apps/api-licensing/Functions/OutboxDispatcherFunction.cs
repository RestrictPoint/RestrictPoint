using Microsoft.Azure.Functions.Worker;
using RestrictPoint.Api.Licensing.Infrastructure;
using RestrictPoint.Database;

namespace RestrictPoint.Api.Licensing.Functions;

/// <summary>Timer trigger hosting the shared outbox dispatcher for the Licensing context.</summary>
public sealed class OutboxDispatcherFunction
{
    private readonly LicensingDbContext _dbContext;
    private readonly OutboxDispatcher _dispatcher;

    public OutboxDispatcherFunction(LicensingDbContext dbContext, OutboxDispatcher dispatcher)
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
