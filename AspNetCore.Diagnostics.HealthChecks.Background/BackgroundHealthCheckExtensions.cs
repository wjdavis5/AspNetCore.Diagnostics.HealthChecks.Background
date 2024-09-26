using AspNetCore.Diagnostics.HealthChecks.Background;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection
{
    public static class BackgroundHealthCheckExtensions
    {
        public static IHealthChecksBuilder AddBackgroundHealthChecks(
            this IServiceCollection services)
        {
            services.AddSingleton<IHealthCheckPublisher, BackgroundHealthCheckPublisher>();
            return services.AddHealthChecks();
        }
    }
}
