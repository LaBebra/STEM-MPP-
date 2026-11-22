using Greenhouse.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Greenhouse.Core;

public static class ServiceRegistration
{
    /// <summary>Реєструє сервіси ядра.</summary>
    public static IServiceCollection AddGreenhouseCore(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<UserService>();
        return services;
    }
}
