# Aspire observability dashboard: design

Date: 2026-10-05
Branch: `feature/aspire-observability`
Status: approved in conversation, pending written-spec review

## Goal

Make the API's runtime behaviour visible in the Aspire Dashboard: distributed
traces of the parallel provider fan-out, structured logs correlated with those
traces, and metrics for provider latency, cache effectiveness, and
degradation.

The dashboard is a **developer-facing observability tool**. It is not a
product UI and not a production monitoring solution.

### Success criteria

- F5 on `ApiAggregator.AppHost` in Visual Studio opens the Aspire Dashboard
  with the `api` resource running and reported healthy.
- One `GET /api/aggregation` request shows up as a single trace: the ASP.NET
  Core request span, one `aggregation.provider` span per executed provider
  running in parallel, and the outgoing HttpClient spans nested under each
  provider span.
- Logs written through Serilog appear in the dashboard's structured logs view,
  linked to their trace.
- The three custom metrics appear under the `ApiAggregator` meter.
- The API still runs on its own (F5 on `ApiAggregator`) with no behaviour
  change and no telemetry export.
- Existing behaviour is unchanged: timeouts, stale-cache fallback,
  statistics endpoint, logging to console and SQL Server.

### Constraints

- No container runtime is installed (no Docker or Podman). The standalone
  dashboard container and a SQL Server container are therefore out of
  reach; the log database stays on LocalDB.
- The user runs the app from Visual Studio and commits changes themselves.

## Approach

Standard Aspire setup: an AppHost project that orchestrates the API and hosts
the dashboard, plus a ServiceDefaults project that configures OpenTelemetry
and health checks.

Rejected alternatives:

- **ServiceDefaults with the standalone dashboard container.** Lighter, but
  requires Docker.
- **Hand-written OpenTelemetry setup without ServiceDefaults.** Re-implements
  what the template already provides, for no benefit.

## 1. Solution structure and wiring

### New projects (added to `ApiAggregator.slnx`)

| Project | Purpose |
|---|---|
| `ApiAggregator.AppHost` | Aspire AppHost (`Aspire.AppHost.Sdk`). Single resource: `builder.AddProject<Projects.ApiAggregator>("api").WithHttpHealthCheck("/health")`. Becomes the startup project in Visual Studio. |
| `ApiAggregator.ServiceDefaults` | Generated from the `aspire-servicedefaults` template, with the changes listed below. |

### ServiceDefaults changes from the template

- **Remove `ConfigureHttpClientDefaults(... AddStandardResilienceHandler() ...)`.**
  The standard resilience handler applies retries, a 10 s attempt timeout,
  and a 30 s total timeout to every HttpClient. That would silently change
  the API's behaviour: providers have a 15 s timeout, the stale-cache
  fallback depends on it, and retries would inflate the provider statistics.
  Aspire is introduced for observability only. A comment in the code
  records this reasoning.
- **Remove service discovery.** There are no inter-service calls; the
  external APIs are addressed by absolute URLs from configuration.
- Everything else stays as generated: OpenTelemetry logging, tracing, and
  metrics (ASP.NET Core, HttpClient, runtime instrumentation), the OTLP
  exporter enabled only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set, health
  check endpoints only in Development, and health check requests excluded
  from tracing.

### Changes to `ApiAggregator`

- Project reference to `ApiAggregator.ServiceDefaults`.
- `Program.cs`: `builder.AddServiceDefaults()` after `builder.AddSerilogLogging()`,
  and `app.MapDefaultEndpoints()`.
- The API registers its own telemetry sources so that ServiceDefaults stays
  generic and does not reference the API project:
  `builder.Services.ConfigureOpenTelemetryTracerProvider(t => t.AddSource(AggregationTelemetry.Name))`
  and `ConfigureOpenTelemetryMeterProvider(m => m.AddMeter(AggregationTelemetry.Name))`.
  This lives in the aggregation feature's registration, not in `Program.cs`.

### Configuration and secrets

The NewsAPI key stays in the API project's user secrets. The AppHost launches
the API in the Development environment, where the API reads its own user
secrets as it does today, so the AppHost needs no secret configuration.

### Running

- F5 on `ApiAggregator.AppHost`: dashboard opens, `api` is listed with logs,
  traces, and metrics.
- F5 on `ApiAggregator`: runs exactly as before. Without
  `OTEL_EXPORTER_OTLP_ENDPOINT`, nothing is exported.

### Risk to verify first

Aspire documentation states in general terms that a container runtime is
required. That requirement applies to container resources; an AppHost with
only project resources is expected to start without one. **The first
implementation task verifies this.** If the AppHost cannot start without
Docker, work stops and the approach is revisited with the user.

## 2. Custom telemetry

### `AggregationTelemetry`

New file `ApiAggregator/Features/Aggregation/Telemetry/AggregationTelemetry.cs`:
an `internal sealed` class registered as a singleton. It owns every telemetry
name, tag key, and tag value, so `AggregationService` does not accumulate
string literals.

- `public const string Name = "ApiAggregator"`, used for both the
  `ActivitySource` and the `Meter`.
- The `ActivitySource` is a static field (standard practice).
- The `Meter` is created through `IMeterFactory` in the constructor, which
  makes it DI-friendly and testable with `MetricCollector<T>`.

### Span per provider

Started at the top of `AggregationService.ExecuteProviderAsync`, so each
parallel provider task has its own span under the request span, and the
HttpClient spans nest under it automatically.

| Aspect | Value |
|---|---|
| Operation name | `aggregation.provider` |
| Display name | `"{Source} provider"`, e.g. `GitHub provider` |
| Tags | `aggregator.source`, `aggregator.cache.hit` (bool), `aggregator.provider.status` (`Succeeded` / `Degraded` / `Unavailable`), `aggregator.item_count` |
| On failure | `SetStatus(ActivityStatusCode.Error, errorMessage)` and `AddException(exception)`. This also applies when the result is `Degraded`: the call failed, even though stale data was served. |

Caller cancellation (the `OperationCanceledException` that is rethrown)
leaves the span without an error status; the request span records the
cancellation.

### Metrics

OpenTelemetry conventions: durations in seconds, low-cardinality tags only
(the search query is never a tag).

| Instrument | Type | Unit | Tags | Recorded when |
|---|---|---|---|---|
| `aggregator.provider.duration` | Histogram\<double\> | `s` | `aggregator.source`, `aggregator.outcome` = `success` / `timeout` / `http_error` / `error` | A real external call completes, the same points where statistics are recorded today (never on a cache hit) |
| `aggregator.cache.lookups` | Counter\<long\> | `{lookup}` | `aggregator.source`, `aggregator.cache.result` = `hit` / `miss` | Every fresh-cache lookup |
| `aggregator.provider.results` | Counter\<long\> | `{result}` | `aggregator.source`, `aggregator.provider.status` | Once per executed provider per request |

### Changes to `AggregationService`

- New constructor dependency: `AggregationTelemetry`.
- `RecordStatistics` also calls the telemetry duration recording, so elapsed
  time is measured once and feeds both the statistics collector and the
  histogram. It gains an outcome parameter for the `aggregator.outcome` tag.
- `ProviderStatisticsCollector` and `GET /api/aggregation/statistics` are
  unchanged.
- Disabled providers (NewsAPI without a key) are never executed, so they
  produce no span and no metric.

## 3. Logging, health checks, noise

### Serilog to the dashboard

- In `SerilogRegistration.AddSerilogLogging`: call
  `builder.Logging.ClearProviders()`, then
  `UseSerilog(..., writeToProviders: true)`. `ClearProviders` is required
  because `WebApplication.CreateBuilder` registers Console, Debug,
  EventSource, and EventLog providers; with `writeToProviders: true` they
  would also receive every event and duplicate the console output.
- `AddServiceDefaults()` runs after `AddSerilogLogging()`, so the
  OpenTelemetry logger provider is the only registered provider. Serilog
  filters first (minimum levels, overrides, request filter) and then
  forwards, so the dashboard shows the same events as the console and SQL
  sinks.
- Console and SQL Server sinks are unchanged. `ActivityEnricher` stays: it
  populates the `TraceId` column in SQL Server. The dashboard correlates logs
  with traces natively.
- The `UseRequestLogging` summary line is unchanged.

### Health checks

- From ServiceDefaults: `/health` (readiness) and `/alive` (liveness), mapped
  only in Development.
- AppHost: `.WithHttpHealthCheck("/health")`, so the dashboard reports the
  resource as Healthy or Unhealthy.

### Noise

The AppHost polls `/health` every few seconds. ServiceDefaults already
excludes health checks from tracing. For logs, the existing
`IsDocumentationRequest` filter is renamed to `IsNonApiRequest`, made
`internal`, and extended to exclude `/health` and `/alive` as well as
`/scalar` and `/openapi`. Without this, the SQL `Logs` table would fill up
with health check entries.

### Unchanged

Exception handling, ProblemDetails, and the `traceId` in error responses. That
`traceId` is the same W3C trace id the dashboard shows, so an error response
leads straight to its trace.

## 4. Testing and documentation

### Unit tests (existing `ApiAggregator.Tests` project)

- New package: `Microsoft.Extensions.Diagnostics.Testing`, for
  `MetricCollector<T>` over a real `IMeterFactory` (`services.AddMetrics()`).
- `AggregationTelemetryTests`: each recording method writes the right
  instrument with the right tags.
- `AggregationServiceTests`: the helper that builds the service gets the new
  dependency. New tests:
  - cache hit: `aggregator.cache.lookups{result=hit}`, no duration recorded,
    span tagged `aggregator.cache.hit=true`;
  - successful call: duration with `outcome=success`,
    `aggregator.provider.results{status=Succeeded}`;
  - timeout with no stale entry: `outcome=timeout`, `status=Unavailable`,
    span status Error;
  - failure with a stale entry: `status=Degraded`, span status Error;
  - disabled provider: no span, no metric.
  - Spans are asserted with an `ActivityListener` filtered to the
    `ApiAggregator` source.
- Request filter test: `/health`, `/alive`, `/scalar/...`, and `/openapi/...`
  are excluded; `/api/aggregation` is not.
- No AppHost-level tests (`Aspire.Hosting.Testing`): not worth it for a single
  resource. Verification is a successful build plus an F5 on the AppHost in
  Visual Studio by the user.

### Documentation

README:

- New **Observability** section: how to run the AppHost, what the dashboard
  shows, the custom span and metrics, and why the standard resilience
  handler was removed.
- Getting started and Project layout updated for the two new projects.

## Out of scope

- HTTP resilience and retries.
- SQL Server or any other container resource.
- Production telemetry exporters (Application Insights or others).
- A custom application dashboard UI.
- Aspire integration tests.
