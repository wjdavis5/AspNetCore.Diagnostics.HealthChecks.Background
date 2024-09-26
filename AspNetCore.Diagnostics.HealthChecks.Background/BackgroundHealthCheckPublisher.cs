using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AspNetCore.Diagnostics.HealthChecks.Background;

public class BackgroundHealthCheckPublisher : IHealthCheckPublisher
{
    private HealthReport? _lastReport;
    private static readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<HealthReport?> GetLastReport(CancellationToken cancellationToken = default)
    {
        try
        {
            await _semaphore.WaitAsync(cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
        return _lastReport;
    }
    public async Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        try
        {
            await _semaphore.WaitAsync(cancellationToken);
            _lastReport = report;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}