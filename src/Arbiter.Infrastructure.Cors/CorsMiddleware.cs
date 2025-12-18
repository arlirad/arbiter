using Arbiter.Domain.Aggregates;
using Arbiter.Domain.Enums;
using Arbiter.Domain.Interfaces;
using Arbiter.Infrastructure.Cors.Models;
using Microsoft.Extensions.Configuration;

namespace Arbiter.Infrastructure.Cors;

public class CorsMiddleware(HandleDelegate next) : IMiddleware
{
    private const string AllowOriginHeader = "Access-Control-Allow-Origin";
    private const string AllowMethodsHeader = "Access-Control-Allow-Methods";
    private const string AllowHeadersHeader = "Access-Control-Allow-Headers";
    private const string AllowCredentialsHeader = "Access-Control-Allow-Credentials";
    private const string OriginHeader = "Origin";
    private const string VaryHeader = "Vary";
    private string? _allowedCredentialsValue;
    private string? _allowedHeadersValue;
    private string? _allowedMethodsValue;

    private List<string>? _allowedOrigins;

    public Task Configure(Site site, IConfiguration config)
    {
        var typedConfig = config.Get<ConfigModel>();

        if (typedConfig?.AllowOrigin is not null)
            _allowedOrigins = typedConfig.AllowOrigin;

        if (typedConfig?.AllowMethods is not null)
            _allowedMethodsValue = string.Join(", ", typedConfig.AllowMethods);

        if (typedConfig?.AllowHeaders is not null)
            _allowedHeadersValue = string.Join(", ", typedConfig.AllowHeaders);

        if (typedConfig?.AllowCredentials.GetValueOrDefault() == true)
            _allowedCredentialsValue = "true";

        return Task.CompletedTask;
    }

    public async Task Handle(Context context)
    {
        if (context.Request.Method != Method.Options)
            await next(context);

        if (_allowedOrigins is not null)
        {
            var origin = context.Request.Headers[OriginHeader]?.FirstOrDefault();
            var allowedOrigin = _allowedOrigins.FirstOrDefault(o => o == origin) ?? _allowedOrigins.First();

            context.Response.Headers.Replace(AllowOriginHeader, allowedOrigin);

            if (context.Response.Headers[VaryHeader] is not null)
                context.Response.Headers.Replace(VaryHeader,
                    context.Response.Headers[VaryHeader]!.First() + ", " + OriginHeader);
        }

        if (_allowedMethodsValue is not null)
            context.Response.Headers.Replace(AllowMethodsHeader, _allowedMethodsValue);

        if (_allowedHeadersValue is not null)
            context.Response.Headers.Replace(AllowHeadersHeader, _allowedHeadersValue);

        if (_allowedCredentialsValue is not null)
            context.Response.Headers.Replace(AllowCredentialsHeader, _allowedCredentialsValue);

        if (context.Request.Method == Method.Options)
            await context.Response.Set(Status.Ok);
    }
}