using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using AspNetCore.SpaYarp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AspNetCore.SpaYarp.IntegrationTests;

// The package reads 'spa.proxy.json' from AppContext.BaseDirectory, which is process-wide,
// so all tests that start a host share one collection and run one after another.
[CollectionDefinition(Name, DisableParallelization = true)]
public class SpaYarpCollection
{
    public const string Name = "SpaYarp";
}

internal static class TestEnvironment
{
    public static string FakeSpaDll { get; } = typeof(TestEnvironment).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(a => a.Key == "FakeSpaDll").Value!;

    public static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public static async Task WaitUntil(Func<Task<bool>> condition, string description, int timeoutSeconds = 30)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"Timed out after {timeoutSeconds}s waiting for: {description}");
    }
}

/// <summary>Starts the fake SPA as its own process (for tests where the package does not launch it).</summary>
internal sealed class FakeSpaProcess : IDisposable
{
    private readonly Process _process;

    public FakeSpaProcess(int port)
    {
        Port = port;
        _process = Process.Start(new ProcessStartInfo("dotnet", $"\"{TestEnvironment.FakeSpaDll}\" --port {port}")
        {
            UseShellExecute = false,
        })!;
    }

    public int Port { get; }

    public string Url => $"http://127.0.0.1:{Port}";

    public async Task WaitUntilListening()
    {
        using var client = new HttpClient();
        await TestEnvironment.WaitUntil(async () =>
        {
            try
            {
                return (await client.GetAsync(Url)).IsSuccessStatusCode;
            }
            catch (HttpRequestException)
            {
                return false;
            }
        }, "fake SPA to listen");
    }

    public void Dispose()
    {
        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
        }

        _process.Dispose();
    }
}

/// <summary>
/// Writes spa.proxy.json (what the build targets generate for a real project), then hosts a web app
/// using the package on a random port.
/// </summary>
internal sealed class SpaYarpHost : IAsyncDisposable
{
    private static readonly string ConfigFile = Path.Combine(AppContext.BaseDirectory, "spa.proxy.json");

    private readonly WebApplication _app;

    private SpaYarpHost(WebApplication app, HttpClient client)
    {
        _app = app;
        Client = client;
    }

    public HttpClient Client { get; }

    public Uri BaseAddress => Client.BaseAddress!;

    public static async Task<SpaYarpHost> Start(
        string clientUrl,
        string launchCommand = "",
        string publicPath = "",
        int maxTimeoutInSeconds = 60)
    {
        var config = new
        {
            SpaProxyServer = new
            {
                ClientUrl = clientUrl,
                LaunchCommand = launchCommand,
                WorkingDirectory = AppContext.BaseDirectory,
                MaxTimeoutInSeconds = maxTimeoutInSeconds,
                PublicPath = publicPath,
            },
        };
        File.WriteAllText(ConfigFile, JsonSerializer.Serialize(config));

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory });
        builder.Logging.ClearProviders();
        builder.Logging.AddFilter("AspNetCore.SpaYarp", LogLevel.Debug);
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSpaYarp();

        var app = builder.Build();
        app.MapGet("/api/ping", () => "pong");
        app.UseSpaYarp();
        await app.StartAsync();

        var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };
        return new SpaYarpHost(app, client);
    }

    /// <summary>A request as a browser navigation sends it.</summary>
    public async Task<(HttpStatusCode Status, string Body)> Navigate(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Accept.ParseAdd("text/html");
        return await Send(request);
    }

    /// <summary>A request as fetch/XHR code (not a navigation) sends it.</summary>
    public async Task<(HttpStatusCode Status, string Body)> Fetch(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Accept.ParseAdd("application/json");
        return await Send(request);
    }

    private async Task<(HttpStatusCode, string)> Send(HttpRequestMessage request)
    {
        using var response = await Client.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
        File.Delete(ConfigFile);
    }
}
