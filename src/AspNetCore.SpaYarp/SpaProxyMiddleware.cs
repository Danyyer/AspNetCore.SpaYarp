// based on https://github.com/dotnet/aspnetcore/blob/main/src/Middleware/Spa/SpaProxy/src/SpaProxyMiddleware.cs
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using System.Text;

namespace AspNetCore.SpaYarp;
/// <summary>
/// Middleware to display a page while the SPA proxy is launching and redirect to the proxy url once the proxy is
/// ready or we have given up trying to start it.
/// This is to help Visual Studio work well in several scenarios by allowing VS to:
/// 1) Launch on the URL configured for the backend (we handle the redirect to the proxy when ready).
/// 2) Ensure that the server is up and running quickly instead of waiting for the proxy to be ready to start the
///    server which causes Visual Studio to think the app failed to launch.
/// </summary>
public class SpaProxyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SpaProxyLaunchManager _spaProxyLaunchManager;
    private readonly IOptions<SpaDevelopmentServerOptions> _options;
    private readonly IHostApplicationLifetime _hostLifetime;
    private readonly ILogger<SpaProxyMiddleware> _logger;

    public SpaProxyMiddleware(
        RequestDelegate next,
        SpaProxyLaunchManager spaProxyLaunchManager,
        IOptions<SpaDevelopmentServerOptions> options,
        IHostApplicationLifetime hostLifetime,
        ILogger<SpaProxyMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _spaProxyLaunchManager = spaProxyLaunchManager ?? throw new ArgumentNullException(nameof(spaProxyLaunchManager));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _hostLifetime = hostLifetime ?? throw new ArgumentNullException(nameof(hostLifetime));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Invoke(HttpContext context)
    {
        // Only browser navigations get the temporary landing page. Other requests (API calls, scripts, ...)
        // must not receive HTML, so they pass through. The SPA is started when a page is requested.
        if (IsNavigationRequest(context.Request) && !await _spaProxyLaunchManager.IsSpaClientRunning(context.RequestAborted))
        {
            _spaProxyLaunchManager.StartInBackground(_hostLifetime.ApplicationStopping);
            _logger.LogDebug("SPA client is not ready. Returning temporary landing page.");
            context.Response.Headers[HeaderNames.CacheControl] = "no-cache, no-store, must-revalidate, max-age=0";
            context.Response.ContentType = "text/html; charset=utf-8";

            await context.Response.WriteAsync(GenerateSpaLaunchPage(_options.Value), Encoding.UTF8, context.RequestAborted);
        }
        else
        {
            await _next(context);
        }

        string GenerateSpaLaunchPage(SpaDevelopmentServerOptions options)
        {
            return $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset = ""UTF-8"" >
  <meta http-equiv=""X-UA-Compatible"" content=""IE=edge"">
  <meta http-equiv=""refresh"" content=""3"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <title>SPA client launch page</title>
</head>
<body>
  <style>
    @media (prefers-color-scheme: dark) {{
      :root {{
        background: black;
        color: gray;
      }}
    }}
  </style>
  <h1>Launching the SPA client...</h1>
  <p>This page will automatically refresh when the SPA client is ready.</p>
</body>
</html>";
        }
    }

    private static bool IsNavigationRequest(HttpRequest request) =>
        (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method)) &&
        request.Headers.Accept.Any(value => value != null && value.Contains("text/html", StringComparison.OrdinalIgnoreCase));
}