using System;
using AspNetCore.Diagnostics.HealthChecks.Background;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection
{
    public static class BackgroundHealthCheckExtensions
    {
        /// <summary>
        /// Registers the <see cref="BackgroundHealthCheckPublisher"/> that caches the
        /// periodic background health check results, and returns the
        /// <see cref="IHealthChecksBuilder"/> for registering checks. Safe to call
        /// more than once; only a single publisher is registered.
        /// </summary>
        public static IHealthChecksBuilder AddBackgroundHealthChecks(
            this IServiceCollection services,
            Action<BackgroundHealthCheckOptions>? configure = null)
        {
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IHealthCheckPublisher, BackgroundHealthCheckPublisher>());
            if (configure != null)
            {
                services.Configure(configure);
            }
            return services.AddHealthChecks();
        }
    }
}
