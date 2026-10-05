# Aspire Observability Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the API's traces, logs, and metrics (including custom provider/cache telemetry) visible in the Aspire Dashboard, without changing API behaviour.

**Architecture:** Two new projects, `ApiAggregator.AppHost` (orchestrates the API and hosts the dashboard) and `ApiAggregator.ServiceDefaults` (OpenTelemetry + health checks, template minus resilience/service discovery). The API forwards Serilog events to the OpenTelemetry logger provider and emits a custom span per provider plus three metrics through an `AggregationTelemetry` singleton.

**Tech Stack:** .NET 10, Aspire 13.6.0 (`Aspire.AppHost.Sdk`, `Aspire.ProjectTemplates`), OpenTelemetry .NET (via ServiceDefaults), Serilog.AspNetCore 10, xUnit 2.9, `Microsoft.Extensions.Diagnostics.Testing` 10.10.0.

**Spec:** `docs/superpowers/specs/2026-10-05-aspire-observability-design.md`

## Global Constraints

- Work on branch `feature/aspire-observability`. Never on `master`.
- **Do not `git commit`.** harko commits. Each "Checkpoint" step means: stop, show the changed files, and suggest a one-line commit message.
- **Do not launch the app** (`dotnet run`, starting the AppHost). harko runs it from Visual Studio. `dotnet build` and `dotnet test` are fine. Steps marked **[harko]** are manual checks for harko to do.
- Aspire version: `13.6.0` everywhere (SDK, templates, hosting packages).
- Telemetry source and meter name: `ApiAggregator`.
- Durations in seconds (`s`); tags are low-cardinality only; the search query is never a tag.
- Existing behaviour unchanged: 15 s provider timeout, stale-cache fallback, `/api/aggregation/statistics`, console + SQL Server log sinks, ProblemDetails.
- Match the surrounding code style: file-scoped namespaces, explanatory comments on non-obvious decisions, `ArgumentNullException.ThrowIfNull` on public entry points.

## Review Focus

1. **API started on its own (no AppHost, no `OTEL_EXPORTER_OTLP_ENDPOINT`).** Expected: it starts and serves requests exactly as before and exports nothing. Pinned by the [harko] check in Task 1 Step 7.
2. **Console output after `writeToProviders: true`.** Expected: every event appears once in the console, not twice (default Console provider cleared). Pinned by the [harko] check in Task 2 Step 6.
3. **No `ActivityListener` attached (all pre-existing tests, and production when not exporting).** Expected: `StartActivity` returns `null` and nothing throws. Pinned by the existing `AggregationServiceTests` passing unchanged in Task 4 Step 2, plus `StartProviderActivity_WithoutListener_ReturnsNull` in Task 3.
4. **Parallel providers.** Expected: each provider gets its own span whose parent is the request span, not another provider's span. Pinned by `AggregateAsync_ParallelProviders_EachGetSpanUnderRequest` in Task 4.
5. **Caller cancellation.** Expected: the provider span is not marked Error and no `aggregator.provider.results` measurement is written. Pinned by `AggregateAsync_CallerCancellation_SpanNotErrorAndNoResultMetric` in Task 4.

---

## File Structure

| File | Status | Responsibility |
|---|---|---|
| `ApiAggregator.AppHost/ApiAggregator.AppHost.csproj`, `AppHost.cs`, `Properties/launchSettings.json` | Create (template) | Orchestrates the `api` resource, hosts the dashboard |
| `ApiAggregator.ServiceDefaults/ApiAggregator.ServiceDefaults.csproj`, `Extensions.cs` | Create (template, trimmed) | OTel logging/tracing/metrics, OTLP export, health endpoints |
| `ApiAggregator.slnx` | Modify | Add the two projects |
| `ApiAggregator/ApiAggregator.csproj` | Modify | Reference ServiceDefaults |
| `ApiAggregator/Program.cs` | Modify | `AddServiceDefaults()`, `MapDefaultEndpoints()` |
| `ApiAggregator/Infrastructure/Logging/SerilogRegistration.cs` | Modify | `ClearProviders`, `writeToProviders`, `IsNonApiRequest` |
| `ApiAggregator/Features/Aggregation/Telemetry/ProviderCallOutcome.cs` | Create | Outcome enum for the duration histogram |
| `ApiAggregator/Features/Aggregation/Telemetry/AggregationTelemetry.cs` | Create | ActivitySource, Meter, instruments, all tag names/values |
| `ApiAggregator/Features/Aggregation/AggregationServiceCollectionExtensions.cs` | Modify | Register `AggregationTelemetry`, add source/meter to OTel |
| `ApiAggregator/Features/Aggregation/Services/AggregationService.cs` | Modify | Span + metrics around each provider execution |
| `ApiAggregator.Tests/ApiAggregator.Tests.csproj` | Modify | Add `Microsoft.Extensions.Diagnostics.Testing` |
| `ApiAggregator.Tests/TestDoubles/TelemetryCapture.cs` | Create | Test helper: real meter factory, metric collectors, scoped activity capture |
| `ApiAggregator.Tests/SerilogRegistrationTests.cs` | Create | Request filter tests |
| `ApiAggregator.Tests/AggregationTelemetryTests.cs` | Create | Instrument/tag tests |
| `ApiAggregator.Tests/AggregationServiceTests.cs` | Modify | Helper gets telemetry; new span/metric tests |
| `README.md` | Modify | Observability section, Getting started, Project layout |

---

### Task 1: AppHost and ServiceDefaults scaffolding

**Files:**
- Create: `ApiAggregator.AppHost/*`, `ApiAggregator.ServiceDefaults/*` (from templates)
- Modify: `ApiAggregator.slnx`, `ApiAggregator/ApiAggregator.csproj`, `ApiAggregator/Program.cs`

**Interfaces:**
- Produces: `IHostApplicationBuilder.AddServiceDefaults()`, `WebApplication.MapDefaultEndpoints()` (template names, namespace `Microsoft.Extensions.Hosting`); health endpoints `/health`, `/alive` in Development; AppHost resource name `api`.

This task has no unit tests; its deliverable is a clean build plus harko's manual check that the AppHost starts **without Docker** (the spec's first-verified risk).

- [ ] **Step 1: Install the Aspire templates**

Run: `dotnet new install Aspire.ProjectTemplates::13.6.0`
Expected: `aspire-apphost` and `aspire-servicedefaults` listed as installed.

- [ ] **Step 2: Generate both projects from the repo root**

Run:
```bash
dotnet new aspire-servicedefaults -n ApiAggregator.ServiceDefaults -o ApiAggregator.ServiceDefaults
dotnet new aspire-apphost -n ApiAggregator.AppHost -o ApiAggregator.AppHost
dotnet sln ApiAggregator.slnx add ApiAggregator.ServiceDefaults/ApiAggregator.ServiceDefaults.csproj ApiAggregator.AppHost/ApiAggregator.AppHost.csproj
dotnet add ApiAggregator/ApiAggregator.csproj reference ApiAggregator.ServiceDefaults/ApiAggregator.ServiceDefaults.csproj
dotnet add ApiAggregator.AppHost/ApiAggregator.AppHost.csproj reference ApiAggregator/ApiAggregator.csproj
```
Expected: both project files exist, are listed in `ApiAggregator.slnx`, and the csproj files reference Aspire `13.6.0`. If the generated SDK/package version is not `13.6.0`, pin it to `13.6.0`.

- [ ] **Step 3: Trim ServiceDefaults `Extensions.cs`**

- Remove `builder.Services.AddServiceDiscovery();` and the whole `builder.Services.ConfigureHttpClientDefaults(...)` block (resilience handler + service discovery).
- Remove the `Microsoft.Extensions.Http.Resilience` and `Microsoft.Extensions.ServiceDiscovery` package references from the csproj.
- Leave a comment where the block was, stating: the standard resilience handler (retries, 10 s attempt timeout, 30 s total) would change the providers' 15 s timeout behaviour, the stale-cache fallback, and inflate provider statistics; Aspire is used here for observability only. Service discovery is unused because external APIs are addressed by absolute URLs.
- Keep everything else as generated (OTel setup, OTLP exporter gated on `OTEL_EXPORTER_OTLP_ENDPOINT`, health checks, health paths excluded from tracing).

- [ ] **Step 4: Wire the API**

In `ApiAggregator/Program.cs`:
- `builder.AddServiceDefaults();` immediately **after** `builder.AddSerilogLogging();`, with a comment that ordering matters (Task 2 relies on the OTel logger provider being registered after Serilog clears providers).
- `app.MapDefaultEndpoints();` immediately before `app.MapControllers();`.

- [ ] **Step 5: Configure the AppHost resource**

`ApiAggregator.AppHost/AppHost.cs`:
```csharp
var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.ApiAggregator>("api")
    .WithHttpHealthCheck("/health");

builder.Build().Run();
```

- [ ] **Step 6: Build and test**

Run: `dotnet build ApiAggregator.slnx` then `dotnet test`
Expected: build succeeds with no new warnings; all existing tests pass.

- [ ] **Step 7: [harko] Manual gate: AppHost without Docker, API standalone**

harko, in Visual Studio:
1. Set `ApiAggregator.AppHost` as the startup project and press F5. Expected: the Aspire Dashboard opens; resource `api` reaches Running and Healthy. **If the AppHost fails because no container runtime is found, stop the plan here** and revisit the approach.
2. Set `ApiAggregator` as the startup project and press F5. Expected: starts as before, Scalar works, no OTLP errors in the console.

- [ ] **Step 8: Checkpoint**

Suggested message: `Add Aspire AppHost and ServiceDefaults projects`

---

### Task 2: Serilog forwarding and request log filter

**Files:**
- Modify: `ApiAggregator/Infrastructure/Logging/SerilogRegistration.cs`
- Test: `ApiAggregator.Tests/SerilogRegistrationTests.cs`

**Interfaces:**
- Consumes: Task 1's `AddServiceDefaults()` running after `AddSerilogLogging()`.
- Produces: `internal static bool SerilogRegistration.IsNonApiRequest(LogEvent logEvent)` (renamed from the private `IsDocumentationRequest`).

- [ ] **Step 1: Write the failing tests**

`SerilogRegistrationTests` builds a `LogEvent` (level Information, empty message template) with a `RequestPath` `ScalarValue` property.

```csharp
[Theory]
[InlineData("/health")]
[InlineData("/alive")]
[InlineData("/HEALTH")]
[InlineData("/scalar/v1")]
[InlineData("/openapi/v1.json")]
public void IsNonApiRequest_ReturnsTrue_ForInfrastructurePaths(string path)
    => Assert.True(SerilogRegistration.IsNonApiRequest(CreateEvent(path)));

[Theory]
[InlineData("/api/aggregation")]
[InlineData("/api/aggregation/statistics")]
public void IsNonApiRequest_ReturnsFalse_ForApiPaths(string path)
    => Assert.False(SerilogRegistration.IsNonApiRequest(CreateEvent(path)));

[Fact]
public void IsNonApiRequest_ReturnsFalse_WhenNoRequestPath()
    => Assert.False(SerilogRegistration.IsNonApiRequest(CreateEvent(path: null)));
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter FullyQualifiedName~SerilogRegistrationTests`
Expected: compile error, `IsNonApiRequest` not found.

- [ ] **Step 3: Implement**

In `SerilogRegistration.cs`:
- Rename `IsDocumentationRequest` to `IsNonApiRequest` and make it `internal`. Add `/health` and `/alive` (same `StartsWith`, `OrdinalIgnoreCase`). Update its XML summary and the `.Filter.ByExcluding(...)` comment to mention health polling by the AppHost.
- In `AddSerilogLogging`: call `builder.Logging.ClearProviders();` before `UseSerilog`, and pass `writeToProviders: true`. Comment: `CreateBuilder` registers Console/Debug/EventSource/EventLog providers that would otherwise also receive every forwarded event (duplicate console output); the only provider left after this is the OpenTelemetry one that `AddServiceDefaults` adds later, which sends logs to the dashboard.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter FullyQualifiedName~SerilogRegistrationTests`
Expected: PASS.

- [ ] **Step 5: Full test run**

Run: `dotnet test`
Expected: all tests pass.

- [ ] **Step 6: [harko] Manual check**

F5 on the AppHost and call `GET /api/aggregation?Query=apollo` once. Expected: the dashboard's Structured logs view shows the request summary line and the `Provider ... returned ...` lines, each linked to a trace. The API console shows each line **once**. No `/health` lines in the console or in `dbo.Logs`.

- [ ] **Step 7: Checkpoint**

Suggested message: `Forward Serilog events to OpenTelemetry and filter health check logs`

---

### Task 3: `AggregationTelemetry`

**Files:**
- Create: `ApiAggregator/Features/Aggregation/Telemetry/ProviderCallOutcome.cs`, `.../Telemetry/AggregationTelemetry.cs`, `ApiAggregator.Tests/TestDoubles/TelemetryCapture.cs`
- Modify: `ApiAggregator/Features/Aggregation/AggregationServiceCollectionExtensions.cs`, `ApiAggregator.Tests/ApiAggregator.Tests.csproj`
- Test: `ApiAggregator.Tests/AggregationTelemetryTests.cs`

**Interfaces:**
- Consumes: `ProviderResult` (`Source`, `Items`, `Status`, `ErrorMessage`), `AggregationSource`, `ProviderStatus`.
- Produces (namespace `ApiAggregator.Features.Aggregation.Telemetry`):
  ```csharp
  internal enum ProviderCallOutcome { Success, Timeout, HttpError, Error }

  internal sealed class AggregationTelemetry
  {
      public const string Name = "ApiAggregator";
      public AggregationTelemetry(IMeterFactory meterFactory);
      public Activity? StartProviderActivity(AggregationSource source);
      public void RecordCacheLookup(Activity? activity, AggregationSource source, bool hit);
      public void RecordProviderDuration(AggregationSource source, TimeSpan elapsed, ProviderCallOutcome outcome);
      public static void RecordException(Activity? activity, Exception exception);
      public void RecordProviderResult(Activity? activity, ProviderResult result);
  }
  ```
- Produces (tests): `TelemetryCapture : IDisposable` with `AggregationTelemetry Telemetry`, `MetricCollector<double> Durations`, `MetricCollector<long> CacheLookups`, `MetricCollector<long> Results`, `IReadOnlyList<Activity> Activities`, `Activity Parent`.

**Exact values (from the spec):**

| Member | Name | Unit | Tags written |
|---|---|---|---|
| span | operation `aggregation.provider`, `DisplayName = $"{source} provider"`, kind Internal | | `aggregator.source` at start; `aggregator.cache.hit` (bool) on lookup; `aggregator.provider.status`, `aggregator.item_count` on result |
| histogram | `aggregator.provider.duration` | `s` | `aggregator.source`, `aggregator.outcome` |
| counter | `aggregator.cache.lookups` | `{lookup}` | `aggregator.source`, `aggregator.cache.result` = `hit` / `miss` |
| counter | `aggregator.provider.results` | `{result}` | `aggregator.source`, `aggregator.provider.status` |

Outcome tag values: `Success` → `success`, `Timeout` → `timeout`, `HttpError` → `http_error`, `Error` → `error`. Source and status tags use the enum name (`GitHub`, `Degraded`).
`RecordProviderResult` sets `ActivityStatusCode.Error` with `result.ErrorMessage` as description when `ErrorMessage` is not null (both `Degraded` and `Unavailable`), then increments the results counter. `RecordException` calls `activity?.AddException(exception)`. All `Activity?` parameters are null-safe.

- [ ] **Step 1: Add the test package and `TelemetryCapture`**

Add `Microsoft.Extensions.Diagnostics.Testing` `10.10.0` to the test csproj.

`TelemetryCapture` (in `TestDoubles`):
- Builds `new ServiceCollection().AddMetrics().BuildServiceProvider()`, resolves `IMeterFactory`, creates `Telemetry`, and three `MetricCollector<T>(meterFactory, AggregationTelemetry.Name, "<instrument>")`. A meter factory per capture means parallel test classes never see each other's measurements.
- Starts `Parent = new Activity("test").Start()` and an `ActivityListener` with `ShouldListenTo = s => s.Name == AggregationTelemetry.Name`, `ActivityStopped` adding to `Activities`, and a `Sample` callback that returns `AllDataAndRecorded` **only when `options.Parent.TraceId == Parent.TraceId`**, `None` otherwise. Reason: xUnit runs test classes in parallel and the `ActivitySource` is static. Sampling by trace keeps each capture to its own test, and makes `StartActivity` return `null` for every other test, so the "without listener" test is not flaky.
- Must be created in the test method body (`using var capture = new TelemetryCapture();`) so `Activity.Current` flows into the code under test.
- `Dispose` stops `Parent`, disposes the listener, the collectors and the service provider.

- [ ] **Step 2: Write the failing tests** (`AggregationTelemetryTests`)

- `StartProviderActivity_SetsNameDisplayNameAndSourceTag`: inside a capture, start and dispose for `GitHub`. Single captured activity: `OperationName == "aggregation.provider"`, `DisplayName == "GitHub provider"`, tag `aggregator.source == "GitHub"`, `ParentSpanId == capture.Parent.SpanId`.
- `StartProviderActivity_WithoutListener_ReturnsNull`: with no capture/listener (a `new AggregationTelemetry(meterFactory)` from a plain `AddMetrics()` provider), result is `null`, and calling `RecordCacheLookup(null, …)`, `RecordException(null, …)`, `RecordProviderResult(null, …)` does not throw.
- `RecordCacheLookup_Hit_IncrementsCounterAndTagsSpan`: one `CacheLookups` measurement, value `1`, tags `aggregator.source=Nasa`, `aggregator.cache.result=hit`; activity tag `aggregator.cache.hit` is `true`.
- `RecordCacheLookup_Miss_TagsMiss`: tag `aggregator.cache.result=miss`; activity tag `aggregator.cache.hit` is `false`.
- `[Theory] RecordProviderDuration_RecordsSecondsWithOutcome` with `(Success,"success")`, `(Timeout,"timeout")`, `(HttpError,"http_error")`, `(Error,"error")`: elapsed `150 ms` → single `Durations` value `0.15`, tags `aggregator.source=GitHub`, `aggregator.outcome=<expected>`.
- `RecordProviderResult_Succeeded_TagsSpanAndCounts`: result with 2 items, status `Succeeded`, no error → activity tags `aggregator.provider.status=Succeeded`, `aggregator.item_count=2`, `Status == Unset`; one `Results` measurement tagged `aggregator.provider.status=Succeeded`.
- `[Theory] RecordProviderResult_WithError_SetsErrorStatus` for `Degraded` and `Unavailable` with `ErrorMessage = "down"`: `activity.Status == Error`, `StatusDescription == "down"`, results tag equals the status name.
- `RecordException_AddsExceptionEvent`: activity has one event named `exception`.

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test --filter FullyQualifiedName~AggregationTelemetryTests`
Expected: compile error, `AggregationTelemetry` not found.

- [ ] **Step 4: Implement `ProviderCallOutcome` and `AggregationTelemetry`**

Tag keys and outcome strings are `private const` / a `switch` inside `AggregationTelemetry`. Instruments are created in the constructor from `meterFactory.Create(Name)`. XML summary on the class explains the split: static `ActivitySource` (standard practice) vs `IMeterFactory` meter (DI scoping, testability).

- [ ] **Step 5: Register in DI and OpenTelemetry**

In `AddAggregation`: `services.AddSingleton<AggregationTelemetry>();` plus
```csharp
services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddSource(AggregationTelemetry.Name));
services.ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddMeter(AggregationTelemetry.Name));
```
Comment: registering here keeps ServiceDefaults generic (it must not reference the API). These extensions come from the `OpenTelemetry` package, available transitively through ServiceDefaults.

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test --filter FullyQualifiedName~AggregationTelemetryTests` then `dotnet test`
Expected: all PASS.

- [ ] **Step 7: Checkpoint**

Suggested message: `Add AggregationTelemetry with provider span and metrics`

---

### Task 4: Instrument `AggregationService`

**Files:**
- Modify: `ApiAggregator/Features/Aggregation/Services/AggregationService.cs`
- Test: `ApiAggregator.Tests/AggregationServiceTests.cs`

**Interfaces:**
- Consumes: Task 3's `AggregationTelemetry`, `ProviderCallOutcome`, `TelemetryCapture`.
- Produces: constructor gains `AggregationTelemetry telemetry` (after `IProviderStatisticsCollector statisticsCollector`).

**Structure:** the current body of `ExecuteProviderAsync` moves to `private async Task<ProviderResult> ExecuteProviderCoreAsync(IAggregationProvider provider, ProviderSearchRequest request, Activity? activity, CancellationToken cancellationToken)`. The new `ExecuteProviderAsync` does: `using var activity = telemetry.StartProviderActivity(provider.Source);` → `var result = await ExecuteProviderCoreAsync(...)` → `telemetry.RecordProviderResult(activity, result)` → return. Caller cancellation propagates out of the core method, so no result is recorded and no Error status is set.

Inside the core method:
- After the fresh lookup: `telemetry.RecordCacheLookup(activity, provider.Source, hit)`.
- `RecordStatistics(provider, startTimestamp, ProviderCallOutcome outcome)` replaces the `bool succeeded` parameter: it passes `outcome == ProviderCallOutcome.Success` to the statistics collector and calls `telemetry.RecordProviderDuration(provider.Source, elapsed, outcome)` with the same `elapsed`.
- Outcomes: success path `Success`; timeout catch `Timeout`; `HttpRequestException` catch `HttpError`; generic catch `Error`. Each failure catch also calls `AggregationTelemetry.RecordException(activity, exception)`.

- [ ] **Step 1: Update the test helper**

`CreateService` gains an optional `AggregationTelemetry? telemetry = null` parameter. The default is `new AggregationTelemetry(<IMeterFactory from a shared static AddMetrics() provider>)`, so existing tests keep running with no listener attached (Review Focus 3).

- [ ] **Step 2: Run the existing tests (red: compile)**

Run: `dotnet test --filter FullyQualifiedName~AggregationServiceTests`
Expected: compile error, the constructor has no `telemetry` parameter yet.

- [ ] **Step 3: Write the new failing tests** (each uses `using var capture = new TelemetryCapture();` and `CreateService(..., telemetry: capture.Telemetry)`)

- `AggregateAsync_FreshCacheHit_RecordsHitAndNoDuration`: real cache, two identical requests. `CacheLookups` has a `miss` then a `hit` for `GitHub`; `Durations` has exactly 1 measurement; the second request's span has `aggregator.cache.hit == true`.
- `AggregateAsync_SuccessfulCall_RecordsSuccessDurationAndResult`: `FakeTimeProvider`, provider advances `150 ms`. `Durations` single value `0.15` with `aggregator.outcome=success`; `Results` single with `aggregator.provider.status=Succeeded`; span `Status == Unset`, `aggregator.item_count` equals the item count.
- `AggregateAsync_TimeoutWithoutStale_RecordsTimeoutAndUnavailable`: handler throws `TaskCanceledException`. Duration tag `outcome=timeout`; results tag `Unavailable`; span `Status == Error`, one `exception` event.
- `AggregateAsync_HttpFailureWithStale_RecordsDegraded`: same setup as the existing stale test. Second request: duration tag `outcome=http_error`; results tag `Degraded`; span `Status == Error`.
- `AggregateAsync_DisabledProvider_ProducesNoTelemetry`: only GitHub registered plus a `DisabledProvider` for NewsApi. Every captured span and measurement has `aggregator.source=GitHub`; none has `NewsApi`.
- `AggregateAsync_ParallelProviders_EachGetSpanUnderRequest`: GitHub and Nasa. Two captured spans, sources `{GitHub, Nasa}`, both `ParentSpanId == capture.Parent.SpanId`.
- `AggregateAsync_CallerCancellation_SpanNotErrorAndNoResultMetric`: same setup as `AggregateAsync_CallerCancellation_Propagates`. After the expected throw: one captured span with `Status != Error`; `Results` has no measurements.

- [ ] **Step 4: Implement the instrumentation** as described in **Structure** above.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test`
Expected: all tests pass, including every pre-existing `AggregationServiceTests` test unchanged apart from the helper.

- [ ] **Step 6: [harko] Manual check**

F5 on the AppHost, call `GET /api/aggregation?Query=apollo` twice within 30 s. Expected in the dashboard:
- Traces: the first request shows `GitHub provider` and `Nasa provider` (and `NewsApi provider` if the key is set) side by side under the request span, each with an HttpClient child span. The second request shows provider spans with `aggregator.cache.hit=true` and no HttpClient children.
- Metrics: meter `ApiAggregator` lists `aggregator.provider.duration`, `aggregator.cache.lookups`, `aggregator.provider.results`.

- [ ] **Step 7: Checkpoint**

Suggested message: `Instrument provider execution with spans and metrics`

---

### Task 5: Documentation and final verification

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Update the README**

- **Getting started → Run:** two options. F5/`dotnet run --project ApiAggregator.AppHost` opens the Aspire Dashboard (no container runtime needed); `dotnet run --project ApiAggregator` runs the API alone with no telemetry export.
- **New `## Observability` section** (after Logging): what the dashboard shows (traces, structured logs, metrics, health). The custom span (`aggregation.provider` and its tags) and the three metrics as a table (name, type, unit, tags), copied from the spec. One paragraph on why ServiceDefaults has no standard resilience handler. One sentence that the `traceId` in ProblemDetails responses is the dashboard trace id.
- **Logging:** one sentence that events are also forwarded to OpenTelemetry, and that health check requests are excluded alongside Scalar/OpenAPI.
- **Project layout:** add `ApiAggregator.AppHost/`, `ApiAggregator.ServiceDefaults/`, and `Features/Aggregation/Telemetry/`.

- [ ] **Step 2: Final verification**

Run: `dotnet build ApiAggregator.slnx` and `dotnet test`
Expected: build succeeds with no new warnings; all tests pass. Report the test count.

- [ ] **Step 3: Checkpoint**

Suggested message: `Document Aspire observability in README`
