using System.Text.Json;
using Arbiter.Api.Controllers;
using Arbiter.Api.Formatters;
using Arbiter.Api.Http;
using Arbiter.Core.Aggregates;
using Arbiter.Core.Enums;
using Arbiter.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Arbiter.Api;

public class ApiMiddleware(IReadOnlyList<Type> controllerTypes, IServiceProvider serviceProvider)
    : IMiddleware
{
    private readonly RouteTable _routeTable = RouteTable.BuildFromTypes(controllerTypes);
    private JsonSerializerOptions _jsonOptions = null!;
    private OutputFormatterSelector _outputFormatterSelector = null!;

    public Task Configure(string path, ComponentDataContainer data, IConfiguration config)
    {
        _jsonOptions = serviceProvider.GetRequiredService<JsonSerializerOptions>();
        _outputFormatterSelector = serviceProvider.GetRequiredService<OutputFormatterSelector>();
        return Task.CompletedTask;
    }

    public async Task Handle(Context context)
    {
        var fullPath = context.Request.Path;
        var queryIndex = fullPath.IndexOf('?');
        var path = queryIndex >= 0 ? fullPath[..queryIndex] : fullPath;
        var queryString = queryIndex >= 0 ? fullPath[queryIndex..] : null;

        var match = _routeTable.Match(context.Request.Method, path);
        if (match is null)
        {
            await context.Response.Set(Status.NotFound, Stream.Null);
            return;
        }

        var (route, parameters) = match.Value;

        await using var scope = serviceProvider.CreateAsyncScope();
        var httpContext = new HttpContext(context, scope.ServiceProvider, queryString, CancellationToken.None, _jsonOptions, _outputFormatterSelector);

        var controller = (IApiController)scope.ServiceProvider.GetRequiredService(route.ControllerType);
        await route.Invoke(controller, httpContext, parameters);
        httpContext.Response.Apply();
    }
}