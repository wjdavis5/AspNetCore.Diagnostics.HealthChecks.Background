using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace AspNetCore.Diagnostics.HealthChecks.Background;

/// <summary>
/// Terminal middleware that serves the most recent background health check report
/// at a URL endpoint, falling back to a live check when no report exists yet.
/// </summary>
public class BackgroundHealthCheckMiddleware
{
    private readonly HealthCheckOptions _healthCheckOptions;
    private readonly BackgroundHealthCheckOptions _backgroundOptions;
    private readonly HealthCheckServiceOptions _serviceOptions;
    private readonly HealthCheckService _healthCheckService;
    private readonly BackgroundHealthCheckPublisher _healthCheckPublisher;

    /// <summary>
    /// Creates a new instance of <see cref="BackgroundHealthCheckMiddleware"/>.
    /// </summary>
    public BackgroundHealthCheckMiddleware(
        RequestDelegate next,
        IOptions<HealthCheckOptions> healthCheckOptions,
        IOptions<BackgroundHealthCheckOptions> backgroundOptions,
        IOptions<HealthCheckServiceOptions> serviceOptions,
        HealthCheckService healthCheckService,
        IEnumerable<IHealthCheckPublisher> healthCheckPublishers)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(healthCheckOptions);
        ArgumentNullException.ThrowIfNull(backgroundOptions);
        ArgumentNullException.ThrowIfNull(serviceOptions);
        ArgumentNullException.ThrowIfNull(healthCheckService);
        ArgumentNullException.ThrowIfNull(healthCheckPublishers);

        _healthCheckOptions = healthCheckOptions.Value;
        _backgroundOptions = backgroundOptions.Value;
        _serviceOptions = serviceOptions.Value;
        _healthCheckService = healthCheckService;
        _healthCheckPublisher = healthCheckPublishers.OfType<BackgroundHealthCheckPublisher>().FirstOrDefault()
            ?? throw new InvalidOperationException(
                "No BackgroundHealthCheckPublisher is registered. Call services.AddBackgroundHealthChecks() before mapping the endpoint.");
    }

    /// <summary>
    /// Processes a request.
    /// </summary>
    public async Task InvokeAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        HealthReport? result = null;
        if (!IsOnDemandCheckRequested(httpContext))
        {
            var lastReport = await _healthCheckPublisher.GetLastReport(httpContext.RequestAborted);
            if (lastReport != null)
            {
                result = ApplyPredicate(lastReport);
            }
        }

        // No cached report yet (the background publisher hasn't completed a run)
        // or an on-demand check was requested: run the checks inline.
        result ??= await _healthCheckService.CheckHealthAsync(
            _healthCheckOptions.Predicate, httpContext.RequestAborted);

        // Map status to response code - this is customizable via options.
        if (!_healthCheckOptions.ResultStatusCodes.TryGetValue(result.Status, out var statusCode))
        {
            var message =
                $"No status code mapping found for {nameof(HealthStatus)} value: {result.Status}." +
                $"{nameof(HealthCheckOptions)}.{nameof(HealthCheckOptions.ResultStatusCodes)} must contain" +
                $"an entry for {result.Status}.";

            throw new InvalidOperationException(message);
        }

        httpContext.Response.StatusCode = statusCode;

        if (!_healthCheckOptions.AllowCachingResponses)
        {
            // Similar to: https://github.com/aspnet/Security/blob/7b6c9cf0eeb149f2142dedd55a17430e7831ea99/src/Microsoft.AspNetCore.Authentication.Cookies/CookieAuthenticationHandler.cs#L377-L379
            var headers = httpContext.Response.Headers;
            headers.CacheControl = "no-store, no-cache";
            headers.Pragma = "no-cache";
            headers.Expires = "Thu, 01 Jan 1970 00:00:00 GMT";
        }

        if (_healthCheckOptions.ResponseWriter != null)
        {
            await _healthCheckOptions.ResponseWriter(httpContext, result);
        }
    }

    private bool IsOnDemandCheckRequested(HttpContext httpContext)
    {
        if (!_backgroundOptions.AllowOnDemandChecks)
        {
            return false;
        }

        return httpContext.Request.Query.TryGetValue("bg", out var value)
            && string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The cached report always contains every registered check, but this endpoint
    /// may be mapped with a <see cref="HealthCheckOptions.Predicate"/> (e.g. separate
    /// readiness/liveness endpoints). Filter the cached entries down to the checks
    /// the predicate selects and recompute the aggregate status.
    /// </summary>
    private HealthReport ApplyPredicate(HealthReport report)
    {
        var predicate = _healthCheckOptions.Predicate;
        if (predicate == null)
        {
            return report;
        }

        var includedNames = _serviceOptions.Registrations
            .Where(predicate)
            .Select(r => r.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var entries = report.Entries
            .Where(e => includedNames.Contains(e.Key))
            .ToDictionary(e => e.Key, e => e.Value);

        return new HealthReport(entries, report.TotalDuration);
    }
}
