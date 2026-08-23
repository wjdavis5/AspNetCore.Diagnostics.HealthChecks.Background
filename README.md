# AspNetCore.Diagnostics.HealthChecks.Background

Run your ASP.NET Core health checks in the background on a timer and serve the
cached result from the health endpoint. This keeps chatty health checks (ones
that hit databases or other dependencies) from running on every probe request.

It plugs into the standard `Microsoft.Extensions.Diagnostics.HealthChecks`
infrastructure: the checks are executed periodically by the built-in
`IHealthCheckPublisher` hosted service, and the endpoint returns the most
recent report.

## Usage

```csharp
var builder = WebApplication.CreateBuilder(args);

// Registers the background publisher and returns the normal IHealthChecksBuilder.
builder.Services.AddBackgroundHealthChecks()
    .AddCheck<MyDatabaseCheck>("database");

// Control how often the checks run (these are the framework's own options).
builder.Services.Configure<HealthCheckPublisherOptions>(options =>
{
    options.Delay = TimeSpan.FromSeconds(5);   // wait before the first run (default 5s)
    options.Period = TimeSpan.FromSeconds(30); // interval between runs (default 30s, min 1s)
});

var app = builder.Build();

app.MapBackgroundHealthChecks("/health");

// Or with the standard HealthCheckOptions (predicate, status codes, response writer):
app.MapBackgroundHealthChecks("/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
});

app.Run();
```

### Behavior

- **Before the first background run completes**, the endpoint falls back to
  running the checks inline, so probes get a real answer instead of an error
  during startup.
- **Endpoint predicates are honored** in background mode: the cached report is
  filtered to the checks the endpoint's `Predicate` selects and the aggregate
  status is recomputed, so separate readiness/liveness endpoints work as
  expected.
- **On-demand live checks are off by default.** If you want to allow callers to
  bypass the cache with `?bg=false`, opt in:

  ```csharp
  builder.Services.AddBackgroundHealthChecks(o => o.AllowOnDemandChecks = true);
  ```

  Leave this off on internet-facing endpoints — it lets any caller trigger the
  dependency-hitting checks this library exists to avoid.

## Migrating from 2.x

Version 3.0.0 contains breaking changes:

- The `(pattern, options)` overload was renamed from `MapHealthChecks` to
  `MapBackgroundHealthChecks`. The old name collided with ASP.NET Core's
  built-in `MapHealthChecks` extension and could cause ambiguous-call compile
  errors in consuming apps.
- The `?bg=` query parameter no longer triggers a live check unless
  `AllowOnDemandChecks` is enabled, and only the explicit value `bg=false`
  (case-insensitive) triggers it.
- Requests made before the first background run now fall back to a live check
  instead of failing with a 500.
- Endpoint `Predicate`s now filter the cached report instead of being ignored.
