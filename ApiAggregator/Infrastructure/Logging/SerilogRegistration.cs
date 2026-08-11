using Serilog;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Sinks.MSSqlServer;
using System.Data;

namespace ApiAggregator.Infrastructure.Logging
{
    /// <summary>
    /// Serilog wiring, configured entirely in code rather than from a
    /// "Serilog" section in appsettings.json. Code configuration is
    /// compile-checked and refactor-safe; the JSON alternative is only worth
    /// it when levels or sinks must change without a redeploy.
    /// </summary>
    public static class SerilogRegistration
    {
        private const string LogTableName = "Logs";

        private const string ConsoleTemplate =
            "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}";

        /// <summary>
        /// Minimal logger used before the host exists, so that a failure during
        /// startup (bad configuration, port in use) still produces output
        /// instead of an unlogged crash. Replaced by the full logger once the
        /// host is built.
        /// </summary>
        public static void CreateBootstrapLogger()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console(outputTemplate: ConsoleTemplate)
                .CreateBootstrapLogger();
        }

        /// <summary>
        /// Replaces the default logging providers with Serilog. The delegate
        /// receives the built <see cref="IServiceProvider"/>, so sinks can
        /// resolve services if they ever need to.
        /// </summary>
        public static WebApplicationBuilder AddSerilogLogging(
            this WebApplicationBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            // Sinks never throw: a logging failure must not take down the app.
            // The cost is that a broken sink loses events in complete silence
            // (wrong connection string, missing column, unreachable server).
            // SelfLog is the only way to see those failures.
            if (builder.Environment.IsDevelopment())
            {
                SelfLog.Enable(Console.Error);
            }

            builder.Host.UseSerilog((context, _, loggerConfiguration) =>
                Configure(
                    loggerConfiguration,
                    context.Configuration,
                    context.HostingEnvironment));

            return builder;
        }

        private static void Configure(
            LoggerConfiguration loggerConfiguration,
            IConfiguration configuration,
            IHostEnvironment environment)
        {
            loggerConfiguration
                .MinimumLevel.Information()

                // Per-namespace overrides: the framework is chatty at
                // Information, our own code is not.
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
                .MinimumLevel.Override("ApiAggregator", LogEventLevel.Debug)

                // FromLogContext is what makes ILogger.BeginScope properties
                // flow into every log event written inside that scope.
                .Enrich.FromLogContext()

                // Makes the traceId shown to the caller in a ProblemDetails
                // response searchable in the logs.
                .Enrich.With<ActivityEnricher>()

                .Enrich.WithProperty("Application", "ApiAggregator")
                .Enrich.WithProperty("Environment", environment.EnvironmentName)
                .Enrich.WithProperty("MachineName", Environment.MachineName)

                // The docs UI issues a handful of requests per page load.
                // Without this, half the log is Scalar fetching its own assets.
                .Filter.ByExcluding(IsDocumentationRequest)

                .WriteTo.Console(outputTemplate: ConsoleTemplate);

            AddSqlServerSink(loggerConfiguration, configuration);
        }

        /// <summary>
        /// True for requests to the OpenAPI document or the Scalar UI and its
        /// static assets. These say nothing about the API's behaviour.
        /// </summary>
        private static bool IsDocumentationRequest(LogEvent logEvent)
        {
            if (!logEvent.Properties.TryGetValue("RequestPath", out var value)
                || value is not ScalarValue { Value: string requestPath })
            {
                return false;
            }

            return requestPath.StartsWith("/scalar", StringComparison.OrdinalIgnoreCase)
                || requestPath.StartsWith("/openapi", StringComparison.OrdinalIgnoreCase);
        }

        private static void AddSqlServerSink(
            LoggerConfiguration loggerConfiguration,
            IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("LogDatabase");

            // Same principle as the disabled NewsAPI provider: a missing
            // dependency degrades the feature, it does not stop the app.
            // Logging must never be the reason a service will not start.
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Log.Warning(
                    "Connection string 'LogDatabase' is not configured; "
                    + "logging to the console only.");

                return;
            }

            loggerConfiguration.WriteTo.MSSqlServer(
                connectionString: connectionString,
                sinkOptions: new MSSqlServerSinkOptions
                {
                    TableName = LogTableName,
                    SchemaName = "dbo",

                    // Fine for LocalDB in development. In production the
                    // database and table belong to a migration, and the app's
                    // login should not have permission to create either.
                    AutoCreateSqlDatabase = true,
                    AutoCreateSqlTable = true,

                    // The sink batches; without a period, low-traffic apps
                    // would appear to lose their most recent entries.
                    BatchPostingLimit = 50,
                    BatchPeriod = TimeSpan.FromSeconds(5)
                },
                restrictedToMinimumLevel: LogEventLevel.Information,
                columnOptions: BuildColumnOptions());
        }

        /// <summary>
        /// Where structured logging actually pays off: named properties from
        /// message templates are written into their own queryable columns,
        /// and the whole event is kept as JSON for anything not promoted.
        /// </summary>
        private static ColumnOptions BuildColumnOptions()
        {
            var columnOptions = new ColumnOptions();

            // The default Properties column stores XML, which is awkward to
            // query. LogEvent stores the same data as JSON instead.
            columnOptions.Store.Remove(StandardColumn.Properties);
            columnOptions.Store.Add(StandardColumn.LogEvent);

            // Promoted properties. "Provider" is filled automatically by
            // templates such as "Provider {Provider} timed out." - no extra
            // code at the call site.
            columnOptions.AdditionalColumns =
            [
                new SqlColumn("SourceContext", SqlDbType.NVarChar, dataLength: 256),
                new SqlColumn("Application", SqlDbType.NVarChar, dataLength: 64),
                new SqlColumn("Environment", SqlDbType.NVarChar, dataLength: 32),
                new SqlColumn("MachineName", SqlDbType.NVarChar, dataLength: 64),
                new SqlColumn("RequestId", SqlDbType.NVarChar, dataLength: 64),

                // The identifier the caller actually sees in an error response.
                new SqlColumn("TraceId", SqlDbType.NVarChar, dataLength: 32),
                new SqlColumn("SpanId", SqlDbType.NVarChar, dataLength: 16),

                new SqlColumn("Provider", SqlDbType.NVarChar, dataLength: 32),
                new SqlColumn("SearchQuery", SqlDbType.NVarChar, dataLength: 128),

                // "Elapsed" comes from the request-logging template,
                // "ElapsedMilliseconds" from the per-provider Debug entry.
                new SqlColumn("Elapsed", SqlDbType.Float),
                new SqlColumn("ElapsedMilliseconds", SqlDbType.Float),
                new SqlColumn("StatusCode", SqlDbType.Int),
                new SqlColumn("RequestPath", SqlDbType.NVarChar, dataLength: 256)
            ];

            return columnOptions;
        }

        /// <summary>
        /// One summary line per HTTP request instead of the framework's
        /// several, enriched with details worth querying later.
        /// </summary>
        public static WebApplication UseRequestLogging(this WebApplication app)
        {
            ArgumentNullException.ThrowIfNull(app);

            app.UseSerilogRequestLogging(
                options =>
                {
                    options.MessageTemplate =
                        "HTTP {RequestMethod} {RequestPath} responded {StatusCode} "
                        + "in {Elapsed:0.0000} ms";

                    // Failed requests deserve a louder level than 200s.
                    options.GetLevel = (httpContext, elapsed, exception) =>
                        exception is not null || httpContext.Response.StatusCode > 499
                            ? LogEventLevel.Error
                            : httpContext.Response.StatusCode > 399
                                ? LogEventLevel.Warning
                                : LogEventLevel.Information;

                    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
                    {
                        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                        diagnosticContext.Set("QueryString", httpContext.Request.QueryString.Value);
                        diagnosticContext.Set(
                            "UserAgent",
                            httpContext.Request.Headers.UserAgent.ToString());

                        // The middleware runs outside the service's logging
                        // scope, so the summary entry would otherwise have no
                        // SearchQuery. Lifting it from the query string keeps
                        // the service free of any logging-infrastructure
                        // dependency.
                        if (httpContext.Request.Query.TryGetValue("Query", out var searchQuery))
                        {
                            diagnosticContext.Set("SearchQuery", searchQuery.ToString());
                        }
                    };
                });

            return app;
        }
    }
}
