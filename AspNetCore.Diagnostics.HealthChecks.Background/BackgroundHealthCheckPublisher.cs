using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AspNetCore.Diagnostics.HealthChecks.Background;

/// <summary>
/// Captures each <see cref="HealthReport"/> produced by the background publisher runs
/// so the most recent one can be served from the health endpoint.
/// </summary>
public class BackgroundHealthCheckPublisher : IHealthCheckPublisher
{
    // Reference writes are atomic; volatile is enough for readers to observe the
    // latest published report without locking.
    private volatile HealthReport? _lastReport;

    /// <summary>
    /// Returns the most recently published <see cref="HealthReport"/>, or
    /// <c>null</c> if the background publisher has not run yet.
    /// </summary>
    public Task<HealthReport?> GetLastReport(CancellationToken cancellationToken = default)
        => Task.FromResult(_lastReport);

    /// <inheritdoc />
    public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        _lastReport = report;
        return Task.CompletedTask;
    }
}
