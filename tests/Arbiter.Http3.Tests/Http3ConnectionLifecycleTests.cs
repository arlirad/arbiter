using System.Net.Quic;
using System.Runtime.Versioning;
using Arbiter.Http3.Tests.Helpers;
using Arlirad.Http3;

namespace Arbiter.Http3.Tests;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macOS")]
[SupportedOSPlatform("windows")]
public class Http3ConnectionLifecycleTests
{
    private Http3IntegrationFixture? _fixture;

    [SetUp]
    public async Task SetUp()
    {
        if (!QuicListener.IsSupported)
            Assert.Ignore("QUIC is not supported on this platform");

        _fixture = await Http3IntegrationFixture.CreateAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_fixture is not null)
        {
            try
            {
                await _fixture.DisposeAsync();
            }
            catch (ObjectDisposedException)
            {
            }

            _fixture = null;
        }
    }

    [Test]
    public async Task Start_completes_without_error()
    {
        Assert.That(_fixture, Is.Not.Null);
        Assert.That(_fixture.ServerConnection, Is.Not.Null);
        Assert.That(_fixture.ClientConnection, Is.Not.Null);
    }

    [Test]
    public async Task Settings_contain_expected_parameters()
    {
        Assert.Multiple(() => {
            Assert.That(_fixture.ServerConnection.LocalSettings.MaxFieldSectionSize, Is.EqualTo(8192));
            Assert.That(_fixture.ServerConnection.LocalSettings.MaxDecoderDynamicTableCapacity, Is.EqualTo(8192));
        });
    }

    [Test]
    public async Task Peer_settings_stored_from_client_SETTINGS()
    {
        await Task.Delay(100);

        Assert.Multiple(() => {
            Assert.That(_fixture!.ClientConnection.LocalSettings.MaxFieldSectionSize, Is.EqualTo(8192));
            Assert.That(_fixture.ClientConnection.LocalSettings.MaxDecoderDynamicTableCapacity, Is.EqualTo(8192));
        });
    }

    [Test]
    public async Task DisposeAsync_cancels_pending_operations()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var acceptTask = _fixture!.AcceptRequestStream(cts.Token);

        await _fixture.DisposeAsync();

        Assert.ThrowsAsync<OperationCanceledException>(async () => await acceptTask);
    }
}