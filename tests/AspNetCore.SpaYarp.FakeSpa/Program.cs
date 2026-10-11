using System.Net.WebSockets;
using System.Text;

// Arguments: --port <n> [--delay-ms <n>] [--log <file>]
//   --delay-ms  waits before listening, to simulate a dev server that is slow to compile/start
//   --log       appends one line per process start, so tests can count how often the SPA was launched
var options = new Dictionary<string, string>();
for (var i = 0; i + 1 < args.Length; i += 2)
{
    options[args[i]] = args[i + 1];
}

if (options.TryGetValue("--log", out var logFile))
{
    File.AppendAllText(logFile, $"started {Environment.ProcessId}{Environment.NewLine}");
}

if (options.TryGetValue("--delay-ms", out var delay))
{
    await Task.Delay(int.Parse(delay));
}

var builder = WebApplication.CreateBuilder();
builder.Logging.ClearProviders();
builder.WebHost.UseUrls($"http://127.0.0.1:{options["--port"]}");

var app = builder.Build();
app.UseWebSockets();

app.MapGet("/", () => "FAKE-SPA-INDEX");
app.MapGet("/echo-host", (HttpContext context) => context.Request.Host.ToString());

// Simulates the dev server dying.
app.MapGet("/__exit", () =>
{
    _ = Task.Run(async () =>
    {
        await Task.Delay(100);
        Environment.Exit(0);
    });
    return "bye";
});

// Echo WebSocket, like the live reload socket of a dev server.
app.Map("/ws", async (HttpContext context) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    var buffer = new byte[4096];
    while (true)
    {
        var result = await socket.ReceiveAsync(buffer, context.RequestAborted);
        if (result.MessageType == WebSocketMessageType.Close)
        {
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, context.RequestAborted);
            break;
        }

        await socket.SendAsync(
            Encoding.UTF8.GetBytes("echo:" + Encoding.UTF8.GetString(buffer, 0, result.Count)),
            WebSocketMessageType.Text, true, context.RequestAborted);
    }
});

// Explicit pattern: the default MapFallback pattern has a 'nonfile' constraint and would not match paths like main.js.
app.MapFallback("{**path}", (HttpContext context) => $"FAKE-SPA path={context.Request.Path}");

await app.RunAsync();
