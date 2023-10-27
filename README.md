# AspNetCore.Diagnostics.HealthChecks.Background

Move your HealthChecks to the background and return the cached results from the last run.
This can help with chatty health checks that hit depencies such as databases.
