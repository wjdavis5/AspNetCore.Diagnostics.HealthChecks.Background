using System;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Builder;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for <see cref="IEndpointRouteBuilder"/> to add health checks.
/// </summary>
public static class BackgroundHealthCheckEndpointRouteBuilderExtensions
{
    private const string DefaultDisplayName = "Background Health checks";

    /// <summary>
    /// Adds a health checks endpoint to the <see cref="IEndpointRouteBuilder"/> with the specified template.
    /// </summary>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> to add the health checks endpoint to.</param>
    /// <param name="pattern">The URL pattern of the health checks endpoint.</param>
    /// <returns>A convention routes for the health checks endpoint.</returns>
    public static IEndpointConventionBuilder MapBackgroundHealthChecks(
        this IEndpointRouteBuilder endpoints,
        string pattern)
    {
        if (endpoints == null)
        {
            throw new ArgumentNullException(nameof(endpoints));
        }

        return MapHealthChecksCore(endpoints, pattern, null);
    }

    /// <summary>
    /// Adds a health checks endpoint to the <see cref="IEndpointRouteBuilder"/> with the specified template and options.
    /// </summary>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> to add the health checks endpoint to.</param>
    /// <param name="pattern">The URL pattern of the health checks endpoint.</param>
    /// <param name="options">A <see cref="HealthCheckOptions"/> used to configure the health checks.</param>
    /// <returns>A convention routes for the health checks endpoint.</returns>
    public static IEndpointConventionBuilder MapHealthChecks(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        HealthCheckOptions options)
    {
        if (endpoints == null)
        {
            throw new ArgumentNullException(nameof(endpoints));
        }

        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        return MapHealthChecksCore(endpoints, pattern, options);
    }

    private static IEndpointConventionBuilder MapHealthChecksCore(IEndpointRouteBuilder endpoints, string pattern, HealthCheckOptions? options)
    {
        if (endpoints.ServiceProvider.GetService(typeof(HealthCheckService)) == null)
        {
            throw new InvalidOperationException("HealthCheckService is not registered in the service provider.");
        }
        

        var middlewareArgs = options != null ? new object[] { Options.Options.Create(options) } : Array.Empty<object>();
        
        var pipeline = endpoints.CreateApplicationBuilder()
            .UseMiddleware<BackgroundHealthCheckMiddleware>(middlewareArgs)
            .Build();

        return endpoints.Map(pattern, pipeline).WithDisplayName(DefaultDisplayName);
    }
}