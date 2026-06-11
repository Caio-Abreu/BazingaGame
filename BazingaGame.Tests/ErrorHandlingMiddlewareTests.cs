using System.IO;
using System.Text.Json;
using BazingaGame.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace BazingaGame.Tests;

public class ErrorHandlingMiddlewareTests
{
    private static ErrorHandlingMiddleware BuildMiddleware(RequestDelegate next) =>
        new(next, NullLogger<ErrorHandlingMiddleware>.Instance);

    private static DefaultHttpContext BuildContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<string> ReadResponseBody(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        return await new StreamReader(context.Response.Body).ReadToEndAsync();
    }

    [Fact]
    public async Task InvokeAsync_NoException_PassesThrough()
    {
        var reached = false;
        var middleware = BuildMiddleware(_ =>
        {
            reached = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(BuildContext());

        Assert.True(reached);
    }

    [Fact]
    public async Task InvokeAsync_UnhandledException_Returns500()
    {
        var middleware = BuildMiddleware(_ => throw new InvalidOperationException("boom"));
        var context = BuildContext();

        await middleware.InvokeAsync(context);

        Assert.Equal(500, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_UnhandledException_WritesJsonErrorBody()
    {
        var middleware = BuildMiddleware(_ => throw new InvalidOperationException("boom"));
        var context = BuildContext();

        await middleware.InvokeAsync(context);

        var body = await ReadResponseBody(context);
        var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task InvokeAsync_UnhandledException_ContentTypeIsJson()
    {
        var middleware = BuildMiddleware(_ => throw new InvalidOperationException("boom"));
        var context = BuildContext();

        await middleware.InvokeAsync(context);

        Assert.Equal("application/json", context.Response.ContentType);
    }

    [Fact]
    public async Task InvokeAsync_OperationCanceledException_DoesNotReturn500()
    {
        // Simulate a client disconnect: RequestAborted is cancelled
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var middleware = BuildMiddleware(_ => throw new OperationCanceledException(cts.Token));
        var context = BuildContext();
        context.RequestAborted = cts.Token;

        await middleware.InvokeAsync(context);

        // Must NOT set 500 — client disconnects are not server errors
        Assert.NotEqual(500, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_OperationCanceledException_WritesNoResponseBody()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var middleware = BuildMiddleware(_ => throw new OperationCanceledException(cts.Token));
        var context = BuildContext();
        context.RequestAborted = cts.Token;

        await middleware.InvokeAsync(context);

        var body = await ReadResponseBody(context);
        Assert.Empty(body);
    }

    [Fact]
    public async Task InvokeAsync_ResponseAlreadyStarted_DoesNotThrow()
    {
        // Simulate response already started by flushing headers
        var middleware = BuildMiddleware(async ctx =>
        {
            await ctx.Response.WriteAsync("partial");  // starts the response
            throw new InvalidOperationException("error after headers sent");
        });

        var context = BuildContext();

        // Should not throw even though response already started
        var ex = await Record.ExceptionAsync(() => middleware.InvokeAsync(context));
        Assert.Null(ex);
    }
}
