# ApiAggregator

An ASP.NET Core (.NET 10) Web API that fans a single search query out to
multiple external APIs in parallel and returns the merged, filtered, and
sorted results in one response.

| Source | External API | Content category |
|---|---|---|
| `GitHub` | GitHub repository search | `Repository` |
| `Nasa` | NASA Image and Video Library | `Media` |
| `NewsApi` | NewsAPI `/v2/everything` | `Article` |

## Getting started

### Prerequisites

- .NET 10 SDK
- A [NewsAPI](https://newsapi.org) API key (optional — see below)

### Configure the NewsAPI key

The key is read from configuration at `ExternalApis:NewsApi:ApiKey`. It is
not stored in the repository; for local development use user secrets:

```bash
dotnet user-secrets set "ExternalApis:NewsApi:ApiKey" "<your-key>" --project ApiAggregator
```

(In Visual Studio: right-click the project → *Manage User Secrets*.)

In production, supply it via an environment variable
(`ExternalApis__NewsApi__ApiKey`) or your secret store of choice.

**If the key is missing**, the application still starts — the NewsAPI
provider is simply disabled. Responses list it as `Unavailable` with an
explanatory message, and requests that explicitly ask for it return
`400 Bad Request`. GitHub and NASA require no configuration.

### Run

```bash
dotnet run --project ApiAggregator
```

In development, interactive API documentation (Scalar) is served at
`/scalar/v1` and the OpenAPI document at `/openapi/v1.json`.

### Test

```bash
dotnet test
```

## API

### `GET /api/aggregation`

| Parameter | Type | Default | Description |
|---|---|---|---|
| `Query` | string (required, ≤100 chars) | — | Search term sent to every provider |
| `Sources` | array of `Nasa` \| `NewsApi` \| `GitHub` | all available | Restrict to specific providers (repeat the parameter: `Sources=GitHub&Sources=Nasa`) |
| `Category` | `Media` \| `Article` \| `Repository` | — | Restrict to providers of one content category |
| `FromDate` / `ToDate` | date (`yyyy-MM-dd`) | — | Inclusive date range on item timestamps |
| `SortBy` | `Timestamp` \| `Title` \| `Source` \| `Category` | `Timestamp` | Sort field for the merged result |
| `SortDirection` | `Ascending` \| `Descending` | `Descending` | Sort direction |
| `ResultsPerSource` | int 1–25 | 10 | Maximum items requested from each provider |

Example:

```
GET /api/aggregation?Query=apollo&Sources=GitHub&Sources=Nasa&SortBy=Title&SortDirection=Ascending
```

Response shape:

```json
{
  "items": [
    {
      "id": "nasa:as11-40-5874",
      "source": "Nasa",
      "category": "Media",
      "title": "Apollo 11 Mission image",
      "description": "…",
      "url": "https://images.nasa.gov/details/as11-40-5874",
      "timestamp": "1969-07-20T00:00:00+00:00"
    }
  ],
  "providers": [
    { "source": "Nasa",   "status": "Succeeded",   "itemCount": 10, "isFromCache": false, "isStale": false, "errorMessage": null },
    { "source": "GitHub", "status": "Succeeded",   "itemCount": 10, "isFromCache": true,  "isStale": false, "errorMessage": null },
    { "source": "NewsApi","status": "Unavailable", "itemCount": 0,  "isFromCache": false, "isStale": false, "errorMessage": "NewsApi requires an API key: set the 'ExternalApis:NewsApi:ApiKey' configuration value to enable it." }
  ],
  "totalCount": 20
}
```

The `providers` array always reports how each provider fared, so partial
failures are visible even when the overall request succeeds.

### `GET /api/aggregation/statistics`

Returns in-memory request statistics for each external API: how many
calls were made, how many succeeded or failed, the average response
time, and a breakdown into performance buckets (fast < 100 ms, average
100–200 ms, slow > 200 ms). Cache hits are not counted — only real
external calls. Statistics reset when the application restarts.

```json
[
  {
    "source": "GitHub",
    "totalRequests": 42,
    "successfulRequests": 40,
    "failedRequests": 2,
    "averageResponseTimeMs": 187.63,
    "buckets": { "fast": 10, "average": 22, "slow": 10 }
  }
]
```

### Error handling and degradation

The API distinguishes problems that are knowable up front from problems
that only appear at request time:

- **Configuration problems → client error.** Explicitly requesting a
  source that is not available on the server (e.g. NewsAPI without a
  key), or a `Sources`/`Category` combination that matches no provider,
  returns `400` with a `ValidationProblemDetails` body explaining why.
- **Runtime failures → graceful degradation.** A provider that times out
  (15 s per provider) or errors mid-request does not fail the response.
  It is reported in `providers` as `Unavailable` (no data) or `Degraded`
  (stale cached data was served instead), while healthy providers return
  normally.
- **Unexpected errors → RFC 7807.** Unhandled exceptions and empty error
  status codes (404, 405, …) are returned as `application/problem+json`
  bodies rather than empty responses.

### Caching

Each provider response is cached in memory per unique request
(source + query + dates + limit):

- **Fresh** for 30 seconds — served directly without calling the provider.
- **Stale** for 30 minutes — served only as a fallback when the provider
  fails, marked `isFromCache: true, isStale: true` with status `Degraded`.

## Project layout

```
ApiAggregator/
  Features/Aggregation/
    Controllers/     Aggregation and statistics endpoints
    Services/        AggregationService — fan-out, merge, filter, sort
    Providers/       One folder per external API (GitHub, Nasa, NewsApi)
    Caching/         Fresh/stale in-memory provider cache
    Statistics/      In-memory per-provider request statistics
    DTOs/            Request/response contracts
    Models/          Internal domain models
    Enums/           Sources, categories, statuses, sort options
ApiAggregator.Tests/ xUnit unit tests
```

Adding a provider means implementing `IAggregationProvider`, mapping its
results to `AggregatedItem`, and registering it (typed `HttpClient` +
`IAggregationProvider` mapping) in `AggregationServiceCollectionExtensions`.
