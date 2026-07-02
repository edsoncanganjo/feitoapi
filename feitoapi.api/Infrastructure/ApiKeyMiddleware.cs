namespace feitoapi.api.Infrastructure;

/// <summary>
/// Simple API-key gate. Two modes:
///  - Standalone: set Auth:ApiKeys (comma-separated) and callers must send X-API-Key.
///  - Behind RapidAPI: RapidAPI injects X-RapidAPI-Proxy-Secret; set Auth:RapidApiProxySecret
///    and only requests carrying that secret are allowed (so nobody bypasses the marketplace).
///
/// If neither is configured, auth is DISABLED (useful for local dev) and a warning is logged.
/// </summary>
public sealed class ApiKeyMiddleware
{
    public const string ApiKeyHeader = "X-API-Key";
    public const string RapidApiSecretHeader = "X-RapidAPI-Proxy-Secret";

    private readonly RequestDelegate _next;
    private readonly HashSet<string> _apiKeys;
    private readonly string? _rapidSecret;
    private readonly bool _enabled;

    public ApiKeyMiddleware(RequestDelegate next, IConfiguration config, ILogger<ApiKeyMiddleware> logger)
    {
        _next = next;

        _apiKeys = (config["Auth:ApiKeys"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);

        _rapidSecret = config["Auth:RapidApiProxySecret"];
        if (string.IsNullOrWhiteSpace(_rapidSecret)) _rapidSecret = null;

        _enabled = _apiKeys.Count > 0 || _rapidSecret is not null;
        if (!_enabled)
            logger.LogWarning("API-key auth is DISABLED (no Auth:ApiKeys or Auth:RapidApiProxySecret configured). Do not run like this in production.");
    }

    public async Task Invoke(HttpContext context)
    {
        var path = context.Request.Path;

        // Public endpoints: health + docs.
        if (!_enabled
            || path.StartsWithSegments("/v1/health")
            || path.StartsWithSegments("/openapi")
            || path.StartsWithSegments("/scalar")
            || path == "/")
        {
            await _next(context);
            return;
        }

        if (IsAuthorized(context.Request))
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { error = "unauthorized", message = $"Missing or invalid {ApiKeyHeader}." });
    }

    private bool IsAuthorized(HttpRequest request)
    {
        if (_rapidSecret is not null
            && request.Headers.TryGetValue(RapidApiSecretHeader, out var proxy)
            && FixedEquals(proxy.ToString(), _rapidSecret))
        {
            return true;
        }

        if (_apiKeys.Count > 0
            && request.Headers.TryGetValue(ApiKeyHeader, out var key)
            && _apiKeys.Contains(key.ToString()))
        {
            return true;
        }

        return false;
    }

    /// <summary>Length-constant comparison to avoid timing side-channels on the proxy secret.</summary>
    private static bool FixedEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        var diff = 0;
        for (int i = 0; i < a.Length; i++)
            diff |= a[i] ^ b[i];
        return diff == 0;
    }
}
