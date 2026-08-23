using System.Net;
using AspNetCore.Diagnostics.HealthChecks.Background;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace AspNetCore.Diagnostics.HealthChecks.Background.Tests;

public class BackgroundHealthCheckEndpointTests
{
    private sealed class ToggleHealthCheck : IHealthCheck
    {
        public volatile HealthStatus Status = HealthStatus.Healthy;

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(new HealthCheckResult(Status));
    }

    private static async Task<IHost> StartHostAsync(
        Action<IServiceCollection> configureServices,
        Action<IEndpointRouteBuilder> mapEndpoints)
    {
        var host = new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    configureServices(services);
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(mapEndpoints);
                }))
            .Build();
        await host.StartAsync();
        return host;
    }

    // Publish immediately, then effectively never again — so tests can mutate check
    // state after the first report without racing a refresh.
    private static void UseSingleImmediatePublish(IServiceCollection services)
        => services.Configure<HealthCheckPublisherOptions>(options =>
        {
            options.Delay = TimeSpan.Zero;
            options.Period = TimeSpan.FromHours(1);
        });

    private static async Task<BackgroundHealthCheckPublisher> WaitForFirstReportAsync(IHost host)
    {
        var publisher = host.Services.GetServices<IHealthCheckPublisher>()
            .OfType<BackgroundHealthCheckPublisher>()
            .Single();
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (await publisher.GetLastReport() == null)
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Background publisher never produced a report.");
            }
            await Task.Delay(50);
        }
        return publisher;
    }

    [Fact]
    public async Task FallsBackToLiveCheck_BeforeFirstBackgroundRun()
    {
        // Default publisher Delay is 5s, so no background report exists when we hit
        // the endpoint immediately after startup.
        using var host = await StartHostAsync(
            services => services.AddBackgroundHealthChecks().AddCheck("always", new ToggleHealthCheck()),
            endpoints => endpoints.MapBackgroundHealthChecks("/health"));

        var response = await host.GetTestClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ServesCachedReport_NotLiveState()
    {
        var check = new ToggleHealthCheck();
        using var host = await StartHostAsync(
            services =>
            {
                services.AddBackgroundHealthChecks().AddCheck("toggle", check);
                UseSingleImmediatePublish(services);
            },
            endpoints => endpoints.MapBackgroundHealthChecks("/health"));

        await WaitForFirstReportAsync(host);
        check.Status = HealthStatus.Unhealthy;

        var response = await host.GetTestClient().GetAsync("/health");

        // The live check would now be Unhealthy; the cached report is still Healthy.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task OnDemandCheck_IsIgnored_WhenNotEnabled()
    {
        var check = new ToggleHealthCheck();
        using var host = await StartHostAsync(
            services =>
            {
                services.AddBackgroundHealthChecks().AddCheck("toggle", check);
                UseSingleImmediatePublish(services);
            },
            endpoints => endpoints.MapBackgroundHealthChecks("/health"));

        await WaitForFirstReportAsync(host);
        check.Status = HealthStatus.Unhealthy;

        var response = await host.GetTestClient().GetAsync("/health?bg=false");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OnDemandCheck_RunsLive_WhenEnabled()
    {
        var check = new ToggleHealthCheck();
        using var host = await StartHostAsync(
            services =>
            {
                services.AddBackgroundHealthChecks(o => o.AllowOnDemandChecks = true)
                    .AddCheck("toggle", check);
                UseSingleImmediatePublish(services);
            },
            endpoints => endpoints.MapBackgroundHealthChecks("/health"));

        await WaitForFirstReportAsync(host);
        check.Status = HealthStatus.Unhealthy;

        var cached = await host.GetTestClient().GetAsync("/health");
        var live = await host.GetTestClient().GetAsync("/health?bg=false");

        Assert.Equal(HttpStatusCode.OK, cached.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, live.StatusCode);
    }

    [Fact]
    public async Task Predicate_FiltersCachedReport()
    {
        var core = new ToggleHealthCheck();
        var db = new ToggleHealthCheck { Status = HealthStatus.Unhealthy };
        using var host = await StartHostAsync(
            services =>
            {
                var builder = services.AddBackgroundHealthChecks();
                builder.AddCheck("core", core);
                builder.AddCheck("db", db, tags: new[] { "db" });
                UseSingleImmediatePublish(services);
            },
            endpoints =>
            {
                endpoints.MapBackgroundHealthChecks("/live", new HealthCheckOptions
                {
                    Predicate = registration => !registration.Tags.Contains("db"),
                });
                endpoints.MapBackgroundHealthChecks("/health");
            });

        await WaitForFirstReportAsync(host);

        var filtered = await host.GetTestClient().GetAsync("/live");
        var unfiltered = await host.GetTestClient().GetAsync("/health");

        // The cached report contains the unhealthy "db" check, but /live's predicate
        // excludes it, so /live is Healthy while /health reflects the full report.
        Assert.Equal(HttpStatusCode.OK, filtered.StatusCode);
        Assert.Equal("Healthy", await filtered.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unfiltered.StatusCode);
    }

    [Fact]
    public void AddBackgroundHealthChecks_IsIdempotent()
    {
        var services = new ServiceCollection();
        services.AddBackgroundHealthChecks();
        services.AddBackgroundHealthChecks();

        using var provider = services.BuildServiceProvider();
        var publishers = provider.GetServices<IHealthCheckPublisher>()
            .OfType<BackgroundHealthCheckPublisher>();

        Assert.Single(publishers);
    }
}

public class BackgroundHealthCheckPublisherTests
{
    private static HealthReport EmptyReport()
        => new(new Dictionary<string, HealthReportEntry>(), TimeSpan.Zero);

    [Fact]
    public async Task GetLastReport_ReturnsNull_BeforeFirstPublish()
    {
        var publisher = new BackgroundHealthCheckPublisher();
        Assert.Null(await publisher.GetLastReport());
    }

    [Fact]
    public async Task GetLastReport_ReturnsPublishedReport()
    {
        var publisher = new BackgroundHealthCheckPublisher();
        var report = EmptyReport();

        await publisher.PublishAsync(report, CancellationToken.None);

        Assert.Same(report, await publisher.GetLastReport());
    }

    [Fact]
    public async Task CanceledToken_DoesNotThrow()
    {
        // Regression test: the old semaphore-based implementation threw
        // SemaphoreFullException when the token was already canceled.
        var publisher = new BackgroundHealthCheckPublisher();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await publisher.PublishAsync(EmptyReport(), cts.Token);
        Assert.NotNull(await publisher.GetLastReport(cts.Token));
    }
}
