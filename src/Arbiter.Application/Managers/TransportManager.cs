using System.Collections.Concurrent;
using System.Net;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using Arbiter.Application.Configuration;
using Arbiter.Application.Interfaces;
using Arbiter.Configuration;
using Arbiter.Core.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using AppConfigurationProvider = Arbiter.Configuration.ConfigurationProvider;

namespace Arbiter.Application.Managers;

public class TransportManager(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    AppConfigurationProvider configProvider) : IDisposable
{
    private static readonly ILogger Log = Serilog.Log.ForContext("SourceContext", "transport");

    private readonly ConcurrentDictionary<string, ITransport> _active = new();
    private readonly List<IPAddress> _addresses = [];
    private readonly AppConfigurationProvider _configProvider = configProvider;
    private readonly IConfiguration _configuration = configuration;
    private readonly ConcurrentDictionary<string, ITransport> _draining = new();
    private readonly Subject<ITransport> _newTransport = new();
    private readonly HashSet<Protocol> _protocols = [Protocol.Http11, Protocol.Http3];
    private readonly SemaphoreSlim _reconfigureLock = new(1, 1);
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly CompositeDisposable _subscriptions = [];

    private HashSet<string> _previousKeys = [];

    public IObservable<ITransport> NewTransport => _newTransport;
    public IEnumerable<ITransport> ActiveTransports => _active.Values;

    public void Dispose()
    {
        _subscriptions.Dispose();
        _reconfigureLock.Dispose();
        _newTransport.Dispose();
    }

    public void Initialize()
    {
        var config = _configProvider.Observe<Dictionary<string, object>>("Transports").CombineLatest(_configProvider.Observe<List<string>>("ListenOn"),
            _configProvider.Observe<ProtocolsConfig>("Protocols"),
            (transports, listenOn, protocols) => new {
                TransportKeys = transports.Keys.ToHashSet(),
                Addresses = listenOn.Select(IPAddress.Parse).ToList(),
                Protocols = protocols.ToSet(),
            }
        );

        _subscriptions.Add(config.Subscribe(async void (c) => {
            try
            {
                await _reconfigureLock.WaitAsync();

                try
                {
                    await ReconfigureAsync(c.TransportKeys, c.Addresses, c.Protocols);
                }
                finally
                {
                    _reconfigureLock.Release();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to reconfigure");
            }
        }));
    }

    private async ValueTask ReconfigureAsync(
        HashSet<string> currentKeys,
        List<IPAddress> addresses,
        HashSet<Protocol> protocols)
    {
        var removed = _previousKeys.Except(currentKeys);
        var added = currentKeys.Except(_previousKeys);
        var existing = currentKeys.Intersect(_previousKeys);

        foreach (var key in removed)
        {
            try
            {
                if (_active.TryRemove(key, out var transport))
                {
                    _draining[key] = transport;

                    if (transport is IDisposable d)
                        d.Dispose();

                    Log.Information("'{Key}' removed, draining active connections", key);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to drain '{Key}'", key);
            }
        }

        foreach (var key in added)
        {
            try
            {
                if (_draining.TryRemove(key, out var drainer))
                {
                    if (drainer is IAsyncDisposable ad)
                        await ad.DisposeAsync();
                    else if (drainer is IDisposable d)
                        d.Dispose();
                }

                var transport = ResolveTransport(key);

                if (transport is null)
                    continue;

                var config = BindConfig(key, transport);

                if (config is null)
                    continue;

                await ConfigureTransport(transport, config, addresses, protocols);
                _active[key] = transport;
                _newTransport.OnNext(transport);
                Log.Information("'{Key}' started", key);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to start '{Key}'", key);
            }
        }

        foreach (var key in existing)
        {
            try
            {
                if (_active.TryGetValue(key, out var transport))
                {
                    var config = BindConfig(key, transport);
                    if (config is not null)
                        await ConfigureTransport(transport, config, addresses, protocols);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to reconfigure '{Key}'", key);
            }
        }

        _previousKeys = currentKeys;
    }

    private object? BindConfig(string key, ITransport transport)
    {
        var configType = transport.GetType().GetConfigType<ITransportConfig>();

        return configType is not null
            ? _configuration.GetSection($"Transports:{key}").Get(configType)
            : null;
    }

    private ITransport? ResolveTransport(string key)
    {
        try
        {
            return _serviceProvider.GetRequiredKeyedService<ITransport>(key);
        }
        catch
        {
            Log.Warning("No transport registered for '{Key}'", key);

            return null;
        }
    }

    private async ValueTask ConfigureTransport(
        ITransport transport,
        object config,
        List<IPAddress> addresses,
        HashSet<Protocol> protocols)
    {
        var method = transport.GetType().GetMethod("ReconfigureAsync",
            BindingFlags.Instance | BindingFlags.Public);

        if (method is null)
        {
            Log.Warning("Transport '{Type}' does not implement ReconfigureAsync", transport.GetType().Name);

            return;
        }

        var args = method.GetParameters().Select(p => p.ParameterType switch {
            var t when t.IsInstanceOfType(config) => config,
            var t when t == typeof(List<IPAddress>) => addresses,
            var t when t == typeof(HashSet<Protocol>) => protocols,
            var t => throw new InvalidOperationException($"Unknown parameter type {t} in ReconfigureAsync"),
        }).ToArray();

        var result = method.Invoke(transport, args);

        if (result is null)
            return;

        await (ValueTask)result;
    }
}
