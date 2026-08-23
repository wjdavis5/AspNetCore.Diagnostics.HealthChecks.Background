namespace AspNetCore.Diagnostics.HealthChecks.Background;

/// <summary>
/// Options controlling the behavior of the background health check endpoint.
/// </summary>
public class BackgroundHealthCheckOptions
{
    /// <summary>
    /// When <c>true</c>, a caller may append <c>?bg=false</c> to the endpoint URL to
    /// force the health checks to run synchronously for that request instead of
    /// serving the cached background report. Off by default, because it lets any
    /// caller trigger the dependency-hitting checks this library exists to avoid.
    /// </summary>
    public bool AllowOnDemandChecks { get; set; }
}
