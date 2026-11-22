using Greenhouse.Api.Realtime;
using Greenhouse.Core.Contracts;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Greenhouse.Tests;

public class RealtimeTests : IDisposable
{
    private readonly GreenhouseFactory _factory = new();

    public void Dispose()
    {
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    private HubConnection Connect() =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, TelemetryHub.Path), o =>
            {
                o.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                o.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
            })
            .AddJsonProtocol(o => o.PayloadSerializerOptions = GreenhouseFactory.Json)
            .Build();

    [Fact]
    public async Task Clients_receive_new_readings_and_alerts()
    {
        _factory.CreateClient().Dispose();
        await using var hub = Connect();
        var readings = new TaskCompletionSource<List<ReadingOut>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var alerts = new TaskCompletionSource<List<AlertOut>>(TaskCreationOptions.RunContinuationsAsynchronously);
        hub.On<List<ReadingOut>>(SignalRTelemetryNotifier.ReadingsEvent, r => readings.TrySetResult(r));
        hub.On<List<AlertOut>>(SignalRTelemetryNotifier.AlertsEvent, a => alerts.TrySetResult(a));
        await hub.StartAsync();

        using var device = _factory.CreateSensorClient();
        await device.PostReadingAsync(new { sensor = "temp-1", value = 35 });

        var r = await readings.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("temp-1", Assert.Single(r).Sensor);
        var a = await alerts.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("high", Assert.Single(a).Kind);
    }

    [Fact]
    public async Task Subscriber_of_other_greenhouse_gets_nothing()
    {
        _factory.CreateClient().Dispose();
        await using var hub = Connect();
        var received = 0;
        hub.On<List<ReadingOut>>(SignalRTelemetryNotifier.ReadingsEvent, _ => Interlocked.Increment(ref received));
        await hub.StartAsync();
        await hub.InvokeAsync("Subscribe", 2);

        using var device = _factory.CreateSensorClient();
        await device.PostReadingAsync(new { sensor = "temp-1", value = 22 });
        await Task.Delay(500);

        Assert.Equal(0, received);
    }
}
