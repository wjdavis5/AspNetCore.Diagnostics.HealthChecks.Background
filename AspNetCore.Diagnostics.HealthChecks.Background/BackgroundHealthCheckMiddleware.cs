using System;
using System.Threading.Tasks;
using AspNetCore.Diagnostics.HealthChecks.Background;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Middleware that exposes a health checks response with a URL endpoint.
    /// </summary>
    public class BackgroundHealthCheckMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly HealthCheckOptions _healthCheckOptions;
        private readonly HealthCheckService _healthCheckService;
        private readonly BackgroundHealthCheckPublisher _healthCheckPublisher;

        /// <summary>
        /// Creates a new instance of <see cref="AspNetCore.Diagnostics.HealthChecks.Background.BackgroundHealthCheckMiddleware"/>.
        /// </summary>
        public BackgroundHealthCheckMiddleware(
            RequestDelegate next,
            IOptions<HealthCheckOptions> healthCheckOptions,
            HealthCheckService healthCheckService,
            IHealthCheckPublisher healthCheckPublisher)
        {
            if (next == null)
            {
                throw new ArgumentNullException(nameof(next));
            }

            if (healthCheckOptions == null)
            {
                throw new ArgumentNullException(nameof(healthCheckOptions));
            }

            if (healthCheckService == null)
            {
                throw new ArgumentNullException(nameof(healthCheckService));
            }

            _next = next;
            _healthCheckOptions = healthCheckOptions.Value;
            _healthCheckService = healthCheckService;
            _healthCheckPublisher = healthCheckPublisher as BackgroundHealthCheckPublisher 
                ?? throw new ArgumentException("Invalid health check publisher type.", nameof(healthCheckPublisher));
        }

        /// <summary>
        /// Processes a request.
        /// </summary>
        /// <param name="httpContext"></param>
        /// <returns></returns>
        public async Task InvokeAsync(HttpContext httpContext)
        {
            if (httpContext == null)
            {
                throw new ArgumentNullException(nameof(httpContext));
            }
            var queryParams = httpContext.Request.Query;
            var getBackgroundValue = queryParams.TryGetValue("bg", out var backgroundValue);
            if (!getBackgroundValue) backgroundValue = "true";
            // Get results
            HealthReport result;
            if (backgroundValue != "true")
            {
                result =
                    await _healthCheckService.CheckHealthAsync(_healthCheckOptions.Predicate,
                        httpContext.RequestAborted);
            }
            else
            {
                result = await _healthCheckPublisher.GetLastReport() ?? throw new InvalidOperationException("Last health report is null.");
            }
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
    }
}