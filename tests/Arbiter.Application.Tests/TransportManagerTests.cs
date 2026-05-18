using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using Arbiter.Application.Configuration;
using Arbiter.Application.Interfaces;
using Arbiter.Application.Managers;
using Arbiter.Configuration;
using Arbiter.Core.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Arbiter.Application.Tests;

public class TransportManagerTests
{
    [Test]
    public async Task ReconfigureAsync_starts_acceptor_for_added_transport()
    {
        var acceptor = new StubAcceptor();
        using var manager = CreateManager("tcp", acceptor, [new TransportDescriptor("tcp", typeof(StubAcceptor), typeof(IpTransportConfig))]);

        await InvokeReconfigureAsync(manager, ["tcp"], [IPAddress.Loopback], [Protocol.Http11]);

        Assert.That(manager.ActiveAcceptors.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task Acceptor_receives_reconfigure_on_add()
    {
        var acceptor = new StubAcceptor();
        using var manager = CreateManager("tcp", acceptor, [new TransportDescriptor("tcp", typeof(StubAcceptor), typeof(IpTransportConfig))]);

        await InvokeReconfigureAsync(manager, ["tcp"], [IPAddress.Loopback], [Protocol.Http11]);

        Assert.That(acceptor.ReconfigureCount, Is.EqualTo(1));
    }

    [Test]
    public async Task ReconfigureAsync_moves_acceptor_to_draining_on_removal()
    {
        var acceptor = new StubAcceptor();
        using var manager = CreateManager("tcp", acceptor, [new TransportDescriptor("tcp", typeof(StubAcceptor), typeof(IpTransportConfig))]);

        await InvokeReconfigureAsync(manager, ["tcp"], [IPAddress.Loopback], [Protocol.Http11]);
        await InvokeReconfigureAsync(manager, [], [IPAddress.Loopback], [Protocol.Http11]);

        var draining = GetDraining(manager);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(manager.ActiveAcceptors, Is.Empty);
            Assert.That(draining.ContainsKey("tcp"), Is.True);
        }
    }

    [Test]
    public async Task ReconfigureAsync_axe_draining_on_re_enable()
    {
        var acceptor = new StubAcceptor();
        var newAcceptor = new StubAcceptor();
        using var manager = CreateManager("tcp", newAcceptor, [new TransportDescriptor("tcp", typeof(StubAcceptor), typeof(IpTransportConfig))]);

        await InvokeReconfigureAsync(manager, ["tcp"], [IPAddress.Loopback], [Protocol.Http11]);
        await InvokeReconfigureAsync(manager, [], [IPAddress.Loopback], [Protocol.Http11]);

        var draining = GetDraining(manager);
        Assert.That(draining.ContainsKey("tcp"), Is.True);

        await InvokeReconfigureAsync(manager, ["tcp"], [IPAddress.Loopback], [Protocol.Http11]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(manager.ActiveAcceptors.Count(), Is.EqualTo(1));
            Assert.That(GetDraining(manager), Is.Empty);
        }
    }

    [Test]
    public async Task ReconfigureAsync_skips_unknown_transport_key()
    {
        using var manager = CreateManager(null, null);

        await InvokeReconfigureAsync(manager, ["unknown"], [IPAddress.Loopback], [Protocol.Http11]);

        Assert.That(manager.ActiveAcceptors, Is.Empty);
    }

    [Test]
    public async Task ReconfigureAsync_reconfigures_existing_transport()
    {
        var acceptor = new StubAcceptor();
        using var manager = CreateManager("tcp", acceptor, [new TransportDescriptor("tcp", typeof(StubAcceptor), typeof(IpTransportConfig))]);

        await InvokeReconfigureAsync(manager, ["tcp"], [IPAddress.Loopback], [Protocol.Http11]);
        await InvokeReconfigureAsync(manager, ["tcp"], [IPAddress.Loopback], [Protocol.Http11]);

        Assert.That(acceptor.ReconfigureCount, Is.EqualTo(2));
    }

    [Test]
    public async Task ReconfigureAsync_disposes_acceptor_on_removal()
    {
        var acceptor = new StubAcceptor();
        using var manager = CreateManager("tcp", acceptor, [new TransportDescriptor("tcp", typeof(StubAcceptor), typeof(IpTransportConfig))]);

        await InvokeReconfigureAsync(manager, ["tcp"], [IPAddress.Loopback], [Protocol.Http11]);
        await InvokeReconfigureAsync(manager, [], [IPAddress.Loopback], [Protocol.Http11]);

        Assert.That(acceptor.IsDisposed, Is.True);
    }

    private static TransportManager CreateManager(string? acceptorKey, IAcceptor? acceptor, params TransportDescriptor[] descriptors)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Transports:tcp:backlog"] = "128",
                ["Transports:tcp:queueSize"] = "4096",
                ["Transports:tcp:ports:0"] = "80",
            })
            .Build();
        var configProvider = new Arbiter.Configuration.ConfigurationProvider(configuration);
        var serviceProvider = new StubServiceProvider(acceptorKey, acceptor);

        return new TransportManager(serviceProvider, configuration, configProvider, descriptors);
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

    private static ConcurrentDictionary<string, IAcceptor> GetDraining(TransportManager manager)
    {
        var field = typeof(TransportManager).GetField("_draining", BindingFlags.NonPublic | BindingFlags.Instance);

        return (ConcurrentDictionary<string, IAcceptor>)field!.GetValue(manager)!;
    }

    private sealed class StubAcceptor : IAcceptor, IAsyncConfigurable<List<IPAddress>, IpTransportConfig, HashSet<Protocol>>, IDisposable
    {
        private int _reconfigureCount;

        public int ReconfigureCount => _reconfigureCount;
        public bool IsDisposed
        {
            get; private set;
        }

        public Task<ITransport> Accept(CancellationToken ct) => Task.FromException<ITransport>(new InvalidOperationException("stub"));

        public ValueTask ReconfigureAsync(List<IPAddress> addresses, IpTransportConfig config, HashSet<Protocol> protocols)
        {
            Interlocked.Increment(ref _reconfigureCount);
            return ValueTask.CompletedTask;
        }

        public void Dispose() => IsDisposed = true;
    }

    private sealed class StubServiceProvider(string? acceptorKey, IAcceptor? acceptor) : IKeyedServiceProvider
    {
        public object? GetService(Type serviceType) => serviceType == typeof(IEnumerable<TransportDescriptor>) ? (IEnumerable<TransportDescriptor>)[] : null;

        public object? GetKeyedService(Type serviceType, object? key) => key?.ToString() == acceptorKey && serviceType == typeof(IAcceptor) ? acceptor : null;

        public object GetRequiredKeyedService(Type serviceType, object? key) => GetKeyedService(serviceType, key) ?? throw new InvalidOperationException();
    }
}
