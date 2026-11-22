using Greenhouse.Api.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Greenhouse.Tests;

public class StartupTests
{
    [Fact]
    public void Missing_signing_key_stops_startup_with_clear_message()
    {
        using var factory = new GreenhouseFactory().WithWebHostBuilder(b => b.UseSetting("Auth:JwtSigningKey", "short"));
        var e = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains("JwtSigningKey", e.ToString(), StringComparison.Ordinal);
    }
}

public class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task Unexpected_exception_becomes_500_without_details()
    {
        var services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = new MemoryStream();
        var logger = new CollectingLogger();
        var handler = new GlobalExceptionHandler(services.GetRequiredService<IProblemDetailsService>(), logger);

        var handled = await handler.TryHandleAsync(context, new InvalidOperationException("секретна деталь"), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.DoesNotContain("секретна деталь", body, StringComparison.Ordinal);
        Assert.Contains(logger.Entries, e => e.Level == Microsoft.Extensions.Logging.LogLevel.Error && e.Exception is not null);
    }

    private sealed class CollectingLogger : Microsoft.Extensions.Logging.ILogger<GlobalExceptionHandler>
    {
        public List<(Microsoft.Extensions.Logging.LogLevel Level, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullLogger.Instance.BeginScope(state);

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId,
            TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, exception));
    }
}
