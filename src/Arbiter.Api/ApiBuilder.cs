using System.Net;
using System.Reflection;
using System.Text.Json;
using Arbiter.Api.Controllers;
using Arbiter.Api.Formatters;
using Arbiter.Application.Interfaces;
using Arbiter.Application.Managers;
using Arbiter.Core.Factories;
using Arbiter.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using HandleDelegate = Arbiter.Core.Interfaces.HandleDelegate;

namespace Arbiter.Api;

public sealed class ApiBuilder
{
    private readonly JsonSerializerOptions _jsonOptions = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly IServiceCollection _services;

    private ApiBuilder(IServiceCollection services)
    {
        _services = services;
    }

    internal List<IPAddress> Addresses
    {
        get;
    } = [];
    internal int Port
    {
        get;
        set;
    } = 80;
    internal bool UseTls
    {
        get;
        set;
    }
    internal List<Type> ControllerTypes
    {
        get;
    } = [];
    internal List<(Type Type, IConfiguration? Config)> MiddlewareEntries
    {
        get;
    } = [];
    internal TimeSpan DefaultRequestTimeout
    {
        get;
        set;
    } = TimeSpan.FromSeconds(30);
    internal List<IOutputFormatter> OutputFormatters
    {
        get;
    } = [];

    public IServiceProvider ServiceProvider
    {
        get;
        private set;
    } = null!;

    public static ApiBuilder Create(IServiceCollection services) => new(services);

    public ApiBuilder ConfigureJson(Action<JsonSerializerOptions> configure)
    {
        configure(_jsonOptions);
        return this;
    }

    public ApiBuilder Configure<TOptions>(Action<TOptions> configure) where TOptions : class, new()
    {
        _services.Configure(configure);
        return this;
    }

    public ApiBuilder WithTcp(IPAddress[] listenOn, int port)
    {
        Addresses.AddRange(listenOn);
        Port = port;
        return this;
    }

    public ApiBuilder WithTcp(int port)
    {
        Port = port;
        return this;
    }

    public ApiBuilder WithTls()
    {
        UseTls = true;
        return this;
    }

    public ApiBuilder WithRequestTimeout(TimeSpan timeout)
    {
        DefaultRequestTimeout = timeout;
        return this;
    }

    public ApiBuilder WithControllers(params Assembly[] assemblies)
    {
        IEnumerable<Assembly> scan = assemblies.Length > 0
            ? assemblies
            : AppDomain.CurrentDomain.GetAssemblies();

        foreach (var assembly in scan)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (type is { IsAbstract: false, IsInterface: false } && typeof(IApiController).IsAssignableFrom(type))
                    ControllerTypes.Add(type);
            }
        }

        return this;
    }

    public ApiBuilder UseMiddleware<TMiddleware>(IConfiguration? config = null) where TMiddleware : class, IMiddleware
    {
        _services.AddTransient<TMiddleware>();
        MiddlewareEntries.Add((typeof(TMiddleware), config));

        return this;
    }

    public ApiBuilder AddFormatter(IOutputFormatter formatter)
    {
        OutputFormatters.Add(formatter);
        return this;
    }

    public IApi Build()
    {
        foreach (var type in ControllerTypes)
            _services.AddTransient(type);

        _services.AddSingleton(_ => ControllerTypes.AsReadOnly());

        _services.AddSingleton(_jsonOptions);
        _services.AddSingleton<IContextFactory, ContextFactory>();
        _services.AddSingleton<ICertificateManager, CertificateManager>();

        _services.AddScoped<MiddlewareChainOrchestrator>();
        _services.AddTransient<HandleDelegate>(sp => sp.GetRequiredService<MiddlewareChainOrchestrator>().GetNext());

        _services.AddSingleton(sp => {
            var selector = new OutputFormatterSelector();

            if (OutputFormatters.Count == 0)
            {
                selector.Add(new SystemTextJsonOutputFormatter(_jsonOptions));
                selector.Add(new TextPlainOutputFormatter());
            }
            else
            {
                foreach (var formatter in OutputFormatters)
                    selector.Add(formatter);
            }

            return selector;
        });

        _services.AddSingleton<IApi>(sp => {
            var contextFactory = sp.GetRequiredService<IContextFactory>();
            var certManager = sp.GetRequiredService<ICertificateManager>();

            return new Api(this, contextFactory, certManager, sp);
        });

        ServiceProvider = _services.BuildServiceProvider();
        return ServiceProvider.GetRequiredService<IApi>();
    }
}