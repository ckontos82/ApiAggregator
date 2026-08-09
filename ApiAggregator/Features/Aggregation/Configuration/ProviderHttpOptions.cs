using System.ComponentModel.DataAnnotations;

namespace ApiAggregator.Features.Aggregation.Configuration
{
    /// <summary>
    /// HTTP settings for one external API, bound from
    /// <c>ExternalApis:{name}</c> as a named options instance.
    /// </summary>
    public sealed class ProviderHttpOptions
    {
        /// <summary>Configuration section that holds one subsection per provider.</summary>
        public const string SectionName = "ExternalApis";

        // Read-write properties, as the options pattern documents: the binder
        // is reflection based and only guarantees binding to { get; set; }.

        /// <summary>Root address of the external API. Provider request URIs are relative to it.</summary>
        [Required(AllowEmptyStrings = false)]
        [Url]
        public string BaseAddress { get; set; } = string.Empty;

        /// <summary>Per-request timeout. See the note on stale cache lifetime before lowering it.</summary>
        [Range(1, 120)]
        public int TimeoutSeconds { get; set; } = 15;

        /// <summary>Value sent as the User-Agent header. Some APIs (GitHub) reject requests without one.</summary>
        [Required(AllowEmptyStrings = false)]
        public string UserAgent { get; set; } = "ApiAggregator/1.0";

        /// <summary>Provider-specific: GitHub's X-GitHub-Api-Version header.</summary>
        public string? ApiVersion { get; set; }

        /// <summary>Provider-specific: NewsAPI's key. Supplied via user secrets, never appsettings.</summary>
        public string? ApiKey { get; set; }
    }
}
