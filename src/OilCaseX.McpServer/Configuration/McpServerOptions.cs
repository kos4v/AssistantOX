using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace OilCaseX.McpServer.Configuration;

public sealed class McpServerOptions
{
    public const string SectionName = "McpServer";
    public const long DefaultMaxRequestBodyBytes = 1_048_576;
    public const long DefaultMaxResponseBodyBytes = 4_194_304;

    [Required]
    public string OilCaseXBaseUrl { get; set; } = "https://x.stg.oilcase.ru";

    [Required]
    public string OilCaseXHealthPath { get; set; } = "/swagger/v1/swagger.json";

    [Required]
    public string McpPath { get; set; } = "/mcp";

    [Range(1, 120)]
    public int OilCaseXTimeoutSeconds { get; set; } = 15;

    [Range(1_024, 16_777_216)]
    public long MaxRequestBodyBytes { get; set; } = DefaultMaxRequestBodyBytes;

    [Range(1_024, 33_554_432)]
    public long MaxResponseBodyBytes { get; set; } = DefaultMaxResponseBodyBytes;

    [Range(30, 3_600)]
    public int ConfirmationTtlSeconds { get; set; } = 300;

    [Range(1, 256)]
    public int MaxConcurrentRequests { get; set; } = 32;

    [Range(1, 10_000)]
    public int RequestsPerMinute { get; set; } = 120;

    public string[] WriteRoles { get; set; } = ["OilCaseX.Writer", "writer"];

    public Uri GetOilCaseXHealthUri()
    {
        var baseUri = new Uri(OilCaseXBaseUrl, UriKind.Absolute);
        return new Uri(baseUri, OilCaseXHealthPath.TrimStart('/'));
    }

    public Uri GetOilCaseXBaseUri()
    {
        return new Uri($"{OilCaseXBaseUrl.TrimEnd('/')}/", UriKind.Absolute);
    }
}

public sealed class McpServerOptionsValidator : IValidateOptions<McpServerOptions>
{
    public ValidateOptionsResult Validate(string? name, McpServerOptions options)
    {
        var failures = new List<string>();

        if (!Uri.TryCreate(options.OilCaseXBaseUrl, UriKind.Absolute, out var baseUri)
            || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps)
            || !string.IsNullOrEmpty(baseUri.UserInfo)
            || !string.IsNullOrEmpty(baseUri.Query)
            || !string.IsNullOrEmpty(baseUri.Fragment))
        {
            failures.Add("McpServer:OilCaseXBaseUrl must be an absolute HTTP(S) URL without query or fragment.");
        }

        if (!options.OilCaseXHealthPath.StartsWith("/", StringComparison.Ordinal))
        {
            failures.Add("McpServer:OilCaseXHealthPath must start with '/'.");
        }

        if (!options.McpPath.StartsWith("/", StringComparison.Ordinal))
        {
            failures.Add("McpServer:McpPath must start with '/'.");
        }

        if (options.McpPath.Contains("{", StringComparison.Ordinal)
            || options.McpPath.Contains("}", StringComparison.Ordinal)
            || options.McpPath.Contains("?", StringComparison.Ordinal))
        {
            failures.Add("McpServer:McpPath must be a fixed route without route parameters or query string.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
