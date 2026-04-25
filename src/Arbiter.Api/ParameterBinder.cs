using System.Reflection;
using System.Text.Json;
using Arbiter.Api.Attributes;
using Arbiter.Api.Http;

namespace Arbiter.Api;

internal static class ParameterBinder
{
    public static async Task<object?[]> BindAsync(
        MethodBase method,
        HttpContext httpContext,
        Dictionary<string, string> routeParameters)
    {
        var parameters = method.GetParameters();
        var args = new object?[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
            args[i] = await BindParameterAsync(parameters[i], httpContext, routeParameters);

        return args;
    }

    private static async ValueTask<object?> BindParameterAsync(
        ParameterInfo param,
        HttpContext httpContext,
        Dictionary<string, string> routeParameters)
    {
        var paramType = param.ParameterType;
        var name = param.Name ?? "";

        if (paramType == typeof(HttpContext))
            return httpContext;

        if (paramType == typeof(CancellationToken))
            return httpContext.CancellationToken;

        if (param.GetCustomAttribute<FromRouteAttribute>() is not null)
            return ConvertOrDefault(routeParameters.GetValueOrDefault(name), param);

        if (param.GetCustomAttribute<FromQueryAttribute>() is { } fromQuery)
            return ConvertOrDefault(httpContext.Request.Query[fromQuery.Name ?? name], param);

        if (param.GetCustomAttribute<FromBodyAttribute>() is not null)
            return await DeserializeBodyAsync(httpContext.Request.Body, paramType, httpContext.JsonSerializerOptions);

        if (param.GetCustomAttribute<FromHeaderAttribute>() is { } fromHeader)
            return ConvertOrDefault(httpContext.Request.Headers[fromHeader.Name ?? name], param);

        if (param.GetCustomAttribute<FromServicesAttribute>() is not null)
            return httpContext.RequestServices.GetService(paramType);

        if (!IsSimpleType(paramType))
            return await DeserializeBodyAsync(httpContext.Request.Body, paramType, httpContext.JsonSerializerOptions);

        var routeValue = routeParameters.GetValueOrDefault(name);
        return ConvertOrDefault(routeValue ?? httpContext.Request.Query[name], param);
    }

    private static object? ConvertOrDefault(string? value, ParameterInfo param)
        => value is null && param.HasDefaultValue ? param.DefaultValue : ConvertValue(value, param.ParameterType);

    private static object? ConvertValue(string? value, Type targetType)
    {
        var underlying = Nullable.GetUnderlyingType(targetType);
        var isNullable = underlying is not null || !targetType.IsValueType;
        underlying ??= targetType;

        return value is null
            ? isNullable ? null : Activator.CreateInstance(underlying)
            : underlying == typeof(string) ? value
                : underlying == typeof(Guid) ? Guid.Parse(value)
                    : underlying == typeof(DateTime) ? DateTime.Parse(value)
                        : Convert.ChangeType(value, underlying);
    }

    private static bool IsSimpleType(Type type)
    {
        return type.IsPrimitive
            || type == typeof(string)
            || type == typeof(Guid)
            || type == typeof(DateTime)
            || type == typeof(decimal)
            || type == typeof(TimeSpan)
            || (Nullable.GetUnderlyingType(type) is { } inner && IsSimpleType(inner));
    }

    private static async ValueTask<object?> DeserializeBodyAsync(Stream? body, Type type, JsonSerializerOptions options)
    {
        if (body is null || !body.CanRead)
            return null;

        if (body is { CanSeek: true, Position: > 0 })
            body.Position = 0;

        return await JsonSerializer.DeserializeAsync(body, type, options);
    }
}