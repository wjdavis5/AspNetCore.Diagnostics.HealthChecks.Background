using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AspNetCore.Diagnostics.HealthChecks.Background;

public class BackgroundHealthCheckPublisher : IHealthCheckPublisher
{
    private HealthReport? _lastReport;
    private static readonly object Lock = new();

    public HealthReport? GetLastReport()
    {
        lock (Lock)
        {
            return _lastReport;
        }
    }
    public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        lock (Lock)
        {
            _lastReport = report;
        }

        return Task.CompletedTask;
    }
}