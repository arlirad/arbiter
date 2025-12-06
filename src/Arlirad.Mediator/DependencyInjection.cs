using Arlirad.Mediator.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Arlirad.Mediator;

public static class DependencyInjection
{
    public static void AddMediator(this IServiceCollection services, Type type)
    {
        services.AddSingleton<IMediator>(sp => new ReflectionMediator(sp, type));
    }
}