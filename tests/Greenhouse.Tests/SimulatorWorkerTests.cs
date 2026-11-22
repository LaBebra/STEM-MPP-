using Greenhouse.Api.Background;
using Greenhouse.Core.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Greenhouse.Tests;

public class SimulatorWorkerTests : IDisposable
{
    private readonly GreenhouseFactory _factory = new();

    public void Dispose()
    {
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Tick_writes_reading_for_every_sensor()
    {
        _factory.CreateClient().Dispose();
        var worker = new SimulatorWorker(
            _factory.Services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new SimulatorOptions()),
            TimeProvider.System,
            NullLogger<SimulatorWorker>.Instance);

        await worker.TickAsync(hour: 12, anomalyRate: 0, CancellationToken.None);

        using var client = _factory.CreateClient();
        var readings = await client.ReadingsAsync();
        Assert.Equal(["hum-1", "light-1", "temp-1"], readings.Select(r => r.Sensor).Order());
    }

    [Fact]
    public async Task Enabled_worker_runs_in_background()
    {
        using var factory = _factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Simulator:Enabled", "true");
            b.UseSetting("Simulator:IntervalSeconds", "0.1");
        });
        using var client = factory.CreateClient();
        Assert.Contains(factory.Services.GetServices<IHostedService>(), s => s is SimulatorWorker);

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while ((await client.ReadingsAsync()).Count < 6 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
        }

        Assert.True((await client.ReadingsAsync()).Count >= 6);
    }

    [Fact]
    public async Task Tick_without_sensors_does_nothing()
    {
        _factory.CreateClient().Dispose();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GreenhouseDbContext>();
            db.Sensors.RemoveRange(db.Sensors);
            await db.SaveChangesAsync();
        }

        var worker = new SimulatorWorker(_factory.Services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new SimulatorOptions()), TimeProvider.System, NullLogger<SimulatorWorker>.Instance);
        await worker.TickAsync(12, 0, CancellationToken.None);

        using var client = _factory.CreateClient();
        Assert.Empty(await client.ReadingsAsync());
    }
}
