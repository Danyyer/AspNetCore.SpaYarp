using System.Diagnostics;
using System.Net;
using Xunit;

namespace AspNetCore.SpaYarp.IntegrationTests;

/// <summary>The package launches the SPA itself (launch command set).</summary>
[Collection(SpaYarpCollection.Name)]
public class LaunchTests : IDisposable
{
    private const string LandingPageText = "Launching the SPA client";

    private readonly string _tempDir = Directory.CreateTempSubdirectory("spayarp-tests").FullName;

    private string LogFile => Path.Combine(_tempDir, "launches.log");

    private int LaunchCount => File.Exists(LogFile) ? File.ReadAllLines(LogFile).Length : 0;

    private string FakeSpaCommand(int port, int delayMs) =>
        $"dotnet {TestEnvironment.FakeSpaDll} --port {port} --delay-ms {delayMs} --log {LogFile}";

    [Fact]
    public async Task Shows_landing_page_while_launching_then_proxies_the_spa()
    {
        var port = TestEnvironment.GetFreePort();
        await using var host = await SpaYarpHost.Start($"http://127.0.0.1:{port}", FakeSpaCommand(port, delayMs: 2000));

        var (status, body) = await host.Navigate("/");
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains(LandingPageText, body);

        await TestEnvironment.WaitUntil(async () => (await host.Navigate("/")).Body == "FAKE-SPA-INDEX", "SPA to be proxied");
        Assert.Equal(1, LaunchCount);
    }

    [Fact]
    public async Task Non_navigation_requests_pass_through_while_the_spa_is_starting()
    {
        var port = TestEnvironment.GetFreePort();
        await using var host = await SpaYarpHost.Start($"http://127.0.0.1:{port}", FakeSpaCommand(port, delayMs: 5000));

        Assert.Contains(LandingPageText, (await host.Navigate("/")).Body);
        Assert.Equal((HttpStatusCode.OK, "pong"), await host.Fetch("/api/ping"));
    }

    [Fact]
    public async Task Does_not_start_a_second_process_while_the_first_is_still_starting()
    {
        // The SPA takes longer to start than the configured timeout.
        var port = TestEnvironment.GetFreePort();
        await using var host = await SpaYarpHost.Start(
            $"http://127.0.0.1:{port}", FakeSpaCommand(port, delayMs: 5000), maxTimeoutInSeconds: 1);

        var deadline = DateTime.UtcNow.AddSeconds(4);
        while (DateTime.UtcNow < deadline)
        {
            await host.Navigate("/");
            await Task.Delay(300);
        }

        Assert.Equal(1, LaunchCount);
    }

    [Fact]
    public async Task Restarts_the_spa_after_it_died()
    {
        var port = TestEnvironment.GetFreePort();
        await using var host = await SpaYarpHost.Start($"http://127.0.0.1:{port}", FakeSpaCommand(port, delayMs: 500));

        await TestEnvironment.WaitUntil(async () => (await host.Navigate("/")).Body == "FAKE-SPA-INDEX", "SPA to be proxied");
        await host.Fetch("/__exit");

        await TestEnvironment.WaitUntil(async () => LaunchCount == 2 && (await host.Navigate("/")).Body == "FAKE-SPA-INDEX",
            "SPA to be launched again and proxied");
    }

    [Fact]
    public async Task Supports_a_launch_command_without_arguments()
    {
        if (OperatingSystem.IsWindows())
        {
            return; // The test uses a shell script.
        }

        var port = TestEnvironment.GetFreePort();
        var script = Path.Combine(_tempDir, "start-spa.sh");
        await File.WriteAllTextAsync(script, $"#!/bin/sh\nexec {FakeSpaCommand(port, delayMs: 500)}\n");
        File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        await using var host = await SpaYarpHost.Start($"http://127.0.0.1:{port}", script);

        await host.Navigate("/");

        await TestEnvironment.WaitUntil(async () => (await host.Navigate("/")).Body == "FAKE-SPA-INDEX", "SPA to be proxied");
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);
}
