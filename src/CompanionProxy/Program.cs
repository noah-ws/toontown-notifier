using System.Net;

var listenPort = 11547;
if (int.TryParse(Environment.GetEnvironmentVariable("COMPANION_PROXY_PORT"), out var parsed))
{
    listenPort = parsed;
}

var ttrHost = Environment.GetEnvironmentVariable("COMPANION_TTR_HOST") ?? "127.0.0.1";
var ttrStart = 1547;
var ttrEnd = 1552;
if (int.TryParse(Environment.GetEnvironmentVariable("COMPANION_PORT_START"), out var start))
{
    ttrStart = start;
}
if (int.TryParse(Environment.GetEnvironmentVariable("COMPANION_PORT_END"), out var end))
{
    ttrEnd = end;
}

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://0.0.0.0:{listenPort}");
builder.Logging.AddSimpleConsole(options =>
{
    options.TimestampFormat = "HH:mm:ss ";
    options.SingleLine = true;
});

var app = builder.Build();
var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
var logger = app.Logger;

logger.LogInformation(
    "Companion proxy listening on 0.0.0.0:{ListenPort}, forwarding to {Host}:{Start}-{End}",
    listenPort,
    ttrHost,
    ttrStart,
    ttrEnd);

app.Run(async context =>
{
    Exception? lastError = null;

    for (var port = ttrStart; port <= ttrEnd; port++)
    {
        try
        {
            var targetHost = ttrHost.Contains(':') && !ttrHost.StartsWith('[') ? $"[{ttrHost}]" : ttrHost;
            var uri = $"http://{targetHost}:{port}{context.Request.Path}{context.Request.QueryString}";
            using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), uri);

            foreach (var header in context.Request.Headers)
            {
                if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase)
                    || header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }

            request.Headers.Host = $"localhost:{port}";

            using var response = await http.SendAsync(request, context.RequestAborted);
            context.Response.StatusCode = (int)response.StatusCode;
            foreach (var header in response.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }

            foreach (var header in response.Content.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }

            context.Response.Headers.Remove("transfer-encoding");
            await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
            logger.LogDebug("Proxied {Path} -> :{Port} ({Status})", context.Request.Path, port, (int)response.StatusCode);
            return;
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            lastError = ex;
        }
    }

    context.Response.StatusCode = (int)HttpStatusCode.BadGateway;
    var detail = lastError?.GetBaseException().Message ?? "connection failed";
    await context.Response.WriteAsync(
        $"TTR Companion App is not listening on {ttrHost}:{ttrStart}-{ttrEnd} ({detail}). Enable Companion App Support in Options and log into a Toon.");
});

app.Run();
