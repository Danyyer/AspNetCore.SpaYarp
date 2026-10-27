using System.Net;
using System.Net.WebSockets;
using System.Text;
using Xunit;

namespace AspNetCore.SpaYarp.IntegrationTests;

/// <summary>The SPA is already running (no launch command): the package only proxies.</summary>
[Collection(SpaYarpCollection.Name)]
public class ProxyTests
{
    [Fact]
    public async Task Forwards_requests_to_the_spa()
    {
        using var spa = new FakeSpaProcess(TestEnvironment.GetFreePort());
        await spa.WaitUntilListening();
        await using var host = await SpaYarpHost.Start(spa.Url);

        Assert.Equal((HttpStatusCode.OK, "FAKE-SPA-INDEX"), await host.Navigate("/"));
        Assert.Equal((HttpStatusCode.OK, "FAKE-SPA path=/some/deep/route"), await host.Navigate("/some/deep/route"));
    }

    [Fact]
    public async Task Backend_endpoints_win_over_the_spa_catch_all()
    {
        using var spa = new FakeSpaProcess(TestEnvironment.GetFreePort());
        await spa.WaitUntilListening();
        await using var host = await SpaYarpHost.Start(spa.Url);

        Assert.Equal((HttpStatusCode.OK, "pong"), await host.Fetch("/api/ping"));
    }

    [Fact]
    public async Task Sends_the_spa_host_instead_of_the_original_host_header()
    {
        // Dev servers such as webpack-dev-server reject unknown Host headers.
        using var spa = new FakeSpaProcess(TestEnvironment.GetFreePort());
        await spa.WaitUntilListening();
        await using var host = await SpaYarpHost.Start(spa.Url);

        var (_, body) = await host.Fetch("/echo-host");

        Assert.Equal($"127.0.0.1:{spa.Port}", body);
        Assert.NotEqual(host.BaseAddress.Authority, body);
    }

    [Fact]
    public async Task Forwards_websocket_connections()
    {
        // Live reload / HMR use a WebSocket to the dev server.
        using var spa = new FakeSpaProcess(TestEnvironment.GetFreePort());
        await spa.WaitUntilListening();
        await using var host = await SpaYarpHost.Start(spa.Url);

        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri($"ws://{host.BaseAddress.Authority}/ws"), CancellationToken.None);
        await socket.SendAsync(Encoding.UTF8.GetBytes("hello"), WebSocketMessageType.Text, true, CancellationToken.None);
        var buffer = new byte[256];
        var result = await socket.ReceiveAsync(buffer, CancellationToken.None);

        Assert.Equal("echo:hello", Encoding.UTF8.GetString(buffer, 0, result.Count));
    }

    [Theory]
    [InlineData("/dist")]
    [InlineData("dist")]
    [InlineData("/dist/")]
    public async Task Public_path_limits_forwarding_to_that_prefix(string publicPath)
    {
        using var spa = new FakeSpaProcess(TestEnvironment.GetFreePort());
        await spa.WaitUntilListening();
        await using var host = await SpaYarpHost.Start(spa.Url, publicPath: publicPath);

        Assert.Equal((HttpStatusCode.OK, "FAKE-SPA path=/dist/main.js"), await host.Fetch("/dist/main.js"));
        Assert.Equal(HttpStatusCode.NotFound, (await host.Fetch("/not-spa")).Status);
        Assert.Equal((HttpStatusCode.OK, "pong"), await host.Fetch("/api/ping"));
    }
}
