using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using Arbiter.Application.Interfaces;
using Arbiter.Application.Managers;
using Arbiter.Configuration;
using Arbiter.Core.Enums;
using Arbiter.Transport.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ConfigurationProvider = Arbiter.Configuration.ConfigurationProvider;

namespace Arbiter.Application.Tests;

public class TransportManagerTests
{
    [Test]
    public async Task ReconfigureAsync_starts_transport_for_added_key()
    {
        var transport = new StubTransport();
        using var manager = CreateManager("tcp", transport);

        await InvokeReconfigureAsync(manager, ["tcp"], [IPAddress.Loopback], [Protocol.Http11]);

        Assert.That(manager.ActiveTransports.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task Transport_receives_reconfigure_on_add()
    {
        var transport = new StubTransport();
        using var manager = CreateManager("tcp", transport);

        await InvokeReconfigureAsync(manager, ["tcp"], [IPAddress.Loopback], [Protocol.Http11]);

        Assert.That(transport.ReconfigureCount, Is.EqualTo(1));
    }

    [Test]
    public async Task ReconfigureAsync_moves_transport_to_draining_on_removal()
    {
        var transport = new StubTransport();
        using var manager = CreateManager("tcp", transport);

        await InvokeReconfigureAsync(manager, ["tcp"], [IPAddress.Loopback], [Protocol.Http11]);
        await InvokeReconfigureAsync(manager, [], [IPAddress.Loopback], [Protocol.Http11]);

        var draining = GetDraining(manager);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(manager.ActiveTransports, Is.Empty);
            Assert.That(draining.ContainsKey("tcp"), Is.True);
        }
    }

    [Test]
    public async Task ReconfigureAsync_axe_draining_on_re_enable()
    {
        var transport = new StubTransport();
        var newTransport = new StubTransport();
        using var manager = CreateManager("tcp", newTransport);

        await InvokeReconfigureAsync(manager, ["tcp"], [IPAddress.Loopback], [Protocol.Http11]);
        await InvokeReconfigureAsync(manager, [], [IPAddress.Loopback], [Protocol.Http11]);

        var draining = GetDraining(manager);
        Assert.That(draining.ContainsKey("tcp"), Is.True);

        await InvokeReconfigureAsync(manager, ["tcp"], [IPAddress.Loopback], [Protocol.Http11]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(manager.ActiveTransports.Count(), Is.EqualTo(1));
            Assert.That(GetDraining(manager), Is.Empty);
        }
    }

    [Test]
    public async Task ReconfigureAsync_skips_unknown_transport_key()
    {
        using var manager = CreateManager(null, null);

        await InvokeReconfigureAsync(manager, ["unknown"], [IPAddress.Loopback], [Protocol.Http11]);

        Assert.That(manager.ActiveTransports, Is.Empty);
    }

    [Test]
    public async Task ReconfigureAsync_reconfigures_existing_transport()
    {
        var transport = new StubTransport();
        using var manager = CreateManager("tcp", transport);

        await InvokeReconfigureAsync(manager, ["tcp"], [IPAddress.Loopback], [Protocol.Http11]);
        await InvokeReconfigureAsync(manager, ["tcp"], [IPAddress.Loopback], [Protocol.Http11]);

        Assert.That(transport.ReconfigureCount, Is.EqualTo(2));
    }

    [Test]
    public async Task ReconfigureAsync_disposes_transport_on_removal()
    {
        var transport = new StubTransport();
        using var manager = CreateManager("tcp", transport);

        await InvokeReconfigureAsync(manager, ["tcp"], [IPAddress.Loopback], [Protocol.Http11]);
        await InvokeReconfigureAsync(manager, [], [IPAddress.Loopback], [Protocol.Http11]);

        Assert.That(transport.IsDisposed, Is.True);
    }

    private static TransportManager CreateManager(string? transportKey, ITransport? transport)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Transports:tcp:backlog"] = "128",
                ["Transports:tcp:queueSize"] = "4096",
                ["Transports:tcp:ports:0"] = "80",
            })
            .Build();

        var configProvider = new ConfigurationProvider(configuration);
        var serviceProvider = new StubServiceProvider(transportKey, transport);

        return new TransportManager(serviceProvider, configuration, configProvider);
    }

    private static async ValueTask InvokeReconfigureAsync(
        TransportManager manager,
        HashSet<string> keys,
        List<IPAddress> addresses,
        HashSet<Protocol> protocols)
    {
        var method = typeof(TransportManager).GetMethod("ReconfigureAsync", BindingFlags.NonPublic | BindingFlags.Instance);

        await (ValueTask)method!.Invoke(manager, [keys, addresses, protocols])!;
    }

    private static ConcurrentDictionary<string, ITransport> GetDraining(TransportManager manager)
    {
        var field = typeof(TransportManager).GetField("_draining", BindingFlags.NonPublic | BindingFlags.Instance);

        return (ConcurrentDictionary<string, ITransport>)field!.GetValue(manager)!;
    }

    private sealed class StubTransport : ITransport, IAsyncConfigurable<List<IPAddress>, IpTransportConfig, HashSet<Protocol>>, IDisposable
    {
        private int _reconfigureCount;

        public int ReconfigureCount => _reconfigureCount;
        public bool IsDisposed
        {
            get;
            private set;
        }

        public Task<IConnection> Accept(CancellationToken ct) => Task.FromException<IConnection>(new InvalidOperationException("stub"));

        public ValueTask ReconfigureAsync(List<IPAddress> addresses, IpTransportConfig config, HashSet<Protocol> protocols)
        {
            Interlocked.Increment(ref _reconfigureCount);

            return ValueTask.CompletedTask;
        }

        public void Dispose() => IsDisposed = true;
    }

    private sealed class StubServiceProvider(string? transportKey, ITransport? transport) : IKeyedServiceProvider
    {
        public object? GetService(Type serviceType) => null;

        public object? GetKeyedService(Type serviceType, object? key) => key?.ToString() == transportKey && serviceType == typeof(ITransport) ? transport : null;

        public object GetRequiredKeyedService(Type serviceType, object? key) => GetKeyedService(serviceType, key) ?? throw new InvalidOperationException();
    }
}
