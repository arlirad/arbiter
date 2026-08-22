using Arbiter.Application.Interfaces;
using Arbiter.Core.Aggregates;
using Arbiter.Core.Enums;
using Arbiter.Core.Interfaces;
using Arbiter.Infrastructure.Cors.Config;
using HandleDelegate = Arbiter.Core.Interfaces.HandleDelegate;

namespace Arbiter.Infrastructure.Cors;

public class CorsMiddleware(HandleDelegate next) : IConfigurableMiddleware<CorsConfig>
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

    public Task Configure(ComponentDataContainer data, CorsConfig config)
    {
        if (config.AllowOrigin is not null)
            _allowedOrigins = config.AllowOrigin;

        if (config.AllowMethods is not null)
            _allowedMethodsValue = string.Join(", ", config.AllowMethods);

        if (config.AllowHeaders is not null)
            _allowedHeadersValue = string.Join(", ", config.AllowHeaders);

        if (config.AllowCredentials.GetValueOrDefault() == true)
            _allowedCredentialsValue = "true";

        return Task.CompletedTask;
    }

    public async Task Handle(Context context)
    {
        if (context.Request.Method != Method.Options)
            await next(context);

        if (_allowedOrigins is not null)
        {
            var origin = context.Request.Header(OriginHeader);

            if (origin is not null && (_allowedOrigins.Contains("*") || _allowedOrigins.Contains(origin)))
            {
                context.Response.SetHeader(AllowOriginHeader, origin);

                context.Response.AppendHeader(VaryHeader, OriginHeader);
            }
        }

        if (_allowedMethodsValue is not null)
            context.Response.SetHeader(AllowMethodsHeader, _allowedMethodsValue);

        if (_allowedHeadersValue is not null)
            context.Response.SetHeader(AllowHeadersHeader, _allowedHeadersValue);

        if (_allowedCredentialsValue is not null)
            context.Response.SetHeader(AllowCredentialsHeader, _allowedCredentialsValue);

        if (context.Request.Method == Method.Options)
            await context.Response.Set(Status.Ok);
    }
}
