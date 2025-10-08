using System.Security.Cryptography.X509Certificates;
using Arbiter.Application.Interfaces;
using Arbiter.Domain.Aggregates;
using Arbiter.Domain.Interfaces;
using Arbiter.Infrastructure.Acme.Models;
using Certify.ACME.Anvil;
using Certify.ACME.Anvil.Acme;
using Certify.ACME.Anvil.Acme.Resource;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Arbiter.Infrastructure.Acme;

internal class AcmeWorker(
    IConfigManager configManager,
    ICertificateManager certificateManager
) : IWorker
{
    private const double RenewalTimeRemainingFraction = 0.80;
    private const string CertificatePassword = "";
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);

    private readonly CancellationTokenSource _cts = new();
    private string? _accountName;
    private AcmeContext? _acmeContext;
    private Uri? _acmeDirectoryUrl;
    private DataModel? _data;
    private List<string> _domains = [];
    private Task? _orderTask;
    private bool _tosAccepted;

    public Task Configure(Site site, IConfiguration config)
    {
        var typedConfig = config.Get<ConfigModel>();

        if (string.IsNullOrEmpty(typedConfig?.AccountName))
            throw new Exception("accountName is not set");

        if (!typedConfig.TosAccepted.HasValue)
            throw new Exception("tosAccepted is not set");

        _acmeDirectoryUrl = typedConfig.AcmeDirectoryUrl ?? throw new Exception("acmeUrl is not set");
        _accountName = typedConfig.AccountName;
        _tosAccepted = typedConfig.TosAccepted.Value;
        _data = site.GetComponentData<DataModel>();
        _domains = site.Bindings
            .Where(b => b.Scheme == Uri.UriSchemeHttps)
            .Select(b => b.Host)
            .ToList();

        return Task.CompletedTask;
    }

    public async Task Start()
    {
        await LoadCertificates();

        _acmeContext = await GetContext();
        _orderTask = OrderLoop(_cts.Token);
    }

    public async Task Stop()
    {
        if (_orderTask is null)
            return;

        await _cts.CancelAsync();
        await _orderTask;
    }

    private async Task OrderLoop(CancellationToken ct)
    {
        try
        {
            while (true)
            {
                await OrderCertificates(ct);
                await Task.Delay(CheckInterval, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // loop breaker
        }
    }

    private async Task LoadCertificates()
    {
        await Task.WhenAll(_domains
            .Select(LoadCertificate)
            .ToList());
    }

    private async Task LoadCertificate(string domain)
    {
        await Task.Run(() =>
        {
            try
            {
                var domainCertPath = configManager.GetFilePath($"{domain}.pfx");
                var cert = X509CertificateLoader.LoadPkcs12FromFile(domainCertPath, CertificatePassword);

                certificateManager.Set(domain, cert);
                Log.Information("Loaded certificate for '{Domain}'", domain);
            }
            catch (Exception e)
            {
                if (e is FileNotFoundException || e.InnerException is FileNotFoundException)
                    return;

                Log.Error(e, "Failed to load certificate for '{Domain}'", domain);
            }
        });
    }

    private async Task OrderCertificates(CancellationToken ct)
    {
        foreach (var domain in _domains)
        {
            var domainCertPath = configManager.GetFilePath($"{domain}.pfx");

            if (!await NeedsOrdering(domainCertPath))
                continue;

            Log.Information("Creating a new order for '{Domain}'", domain);

            var order = await _acmeContext!.NewOrder([domain]);
            var authorization = (await order.Authorizations()).First();
            var httpChallenge = await authorization.Http();

            _data!.Challenges.Add(httpChallenge);

            Log.Information("Starting validation of '{Domain}'", domain);

            await Validate(httpChallenge, domain, ct);

            Log.Information("Validation of '{Domain}' completed", domain);

            var privateKey = KeyFactory.NewKey(KeyAlgorithm.ES256);
            var cert = await order.Generate(new CsrInfo
            {
                CommonName = domain,
            }, privateKey);

            var pfxBuilder = cert.ToPfx(privateKey);
            var pfx = pfxBuilder.Build(domain, CertificatePassword);
            var renewAfter = GetRenewalDate(pfx);

            certificateManager.Set(domain, GetCertificateInstance(pfx));

            await File.WriteAllBytesAsync(domainCertPath, pfx, ct);
            Log.Information("Successfully created a certificate for '{Domain}', renewal after {RenewAfter}",
                domain, renewAfter);
        }
    }

    private static async Task<bool> NeedsOrdering(string cert)
    {
        try
        {
            var pfx = await File.ReadAllBytesAsync(cert);
            return DateTime.Now >= GetRenewalDate(pfx);
        }
        catch (FileNotFoundException)
        {
            return true;
        }
    }

    private static async Task Validate(IChallengeContext httpChallenge, string domain, CancellationToken ct)
    {
        Challenge challenge;

        while (true)
        {
            await Task.Delay(5000, ct);

            challenge = await httpChallenge.Validate();
            if (challenge.Status == ChallengeStatus.Valid)
                break;
        }

        if (challenge.Status != ChallengeStatus.Valid)
            throw new Exception($"Failed to validate '{domain}'");
    }

    private static X509Certificate2 GetCertificateInstance(byte[] pfxBytes)
    {
        var coll = X509CertificateLoader.LoadPkcs12Collection(pfxBytes, CertificatePassword);
        var leaf = coll.FirstOrDefault(c => c.HasPrivateKey)
            ?? coll.OrderBy(c => c.NotAfter)
                .FirstOrDefault()
            ?? throw new Exception("Failed to find a suitable certificate in chain");

        return leaf;
    }

    private static DateTime GetRenewalDate(byte[] pfxBytes)
    {
        var coll = X509CertificateLoader.LoadPkcs12Collection(pfxBytes, CertificatePassword);
        var leaf = coll.FirstOrDefault(c => c.HasPrivateKey)
            ?? coll.OrderBy(c => c.NotAfter)
                .FirstOrDefault()
            ?? throw new Exception("Failed to find a suitable certificate in chain");

        return leaf.NotBefore.Add((leaf.NotAfter - leaf.NotBefore) * RenewalTimeRemainingFraction);
    }

    private async Task<AcmeContext> GetContext()
    {
        var keyPath = configManager.GetFilePath($"{_accountName}.pem");

        if (File.Exists(keyPath))
        {
            var key = KeyFactory.FromPem(await File.ReadAllTextAsync(keyPath));
            Log.Information("acme: Loaded account '{AccountName}' keys", _accountName);

            return new AcmeContext(_acmeDirectoryUrl!, key);
        }

        var context = new AcmeContext(_acmeDirectoryUrl!);

        await context.NewAccount(_accountName, _tosAccepted);
        await File.WriteAllTextAsync(keyPath, context.AccountKey.ToPem());

        Log.Information("acme: Created account '{AccountName}'", _accountName);

        return context;
    }
}