using Arbiter.Models;
using Arbiter.Models.Components;
using Arbiter.Models.Config.Workers;
using Arbiter.Services;
using Certify.ACME.Anvil;
using Certify.ACME.Anvil.Pkcs;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Arbiter.Workers.Acme;

internal class AcmeWorker(ConfigManager configManager) : IWorker
{
    private string? _accountName;
    private AcmeContext? _acmeContext;
    private Uri? _acmeDirectoryUrl;
    private AcmeDataModel? _data;
    private List<string> _domains = [];
    private bool _tosAccepted;

    public Task Configure(Site site, IConfiguration config)
    {
        var typedConfig = config.Get<AcmeWorkerConfigModel>();

        if (string.IsNullOrEmpty(typedConfig?.AccountName))
            throw new Exception("accountName is not set");

        if (!typedConfig.TosAccepted.HasValue)
            throw new Exception("tosAccepted is not set");

        _acmeDirectoryUrl = typedConfig?.AcmeDirectoryUrl ?? throw new Exception("acmeUrl is not set");
        _accountName = typedConfig.AccountName;
        _tosAccepted = typedConfig.TosAccepted.Value;
        _data = site.GetComponentData<AcmeDataModel>();
        _domains = site.Bindings
            .Where(b => b.Scheme == Uri.UriSchemeHttps)
            .Select(b => b.Host)
            .ToList();

        return Task.CompletedTask;
    }

    public async Task Start()
    {
        _acmeContext = await GetContext();

        foreach (var domain in _domains)
        {
            var domainCertPath = Path.Join(configManager.DataPath, $"{domain}.pfx");

            if (!await Task.Run(() => File.Exists(domain)))
                ;

            Log.Information("Creating a new order for '{Domain}'", domain);

            var order = await _acmeContext.NewOrder([domain]);
            var authorization = (await order.Authorizations()).First();
            var httpChallenge = await authorization.Http();

            _data!.Challenges.Add(httpChallenge);

            Log.Information("Starting validation of '{Domain}'", domain);
            await httpChallenge.Validate();
            Log.Information("Validation of '{Domain}' completed", domain);

            var privateKey = KeyFactory.NewKey(KeyAlgorithm.ES256);
            var cert = await order.Generate(new CsrInfo
            {
                CommonName = domain,
            }, privateKey);

            var pfxBuilder = cert.ToPfx(privateKey);
            var pfx = pfxBuilder.Build(domain, "");

            await File.WriteAllBytesAsync(domainCertPath, pfx);
            Log.Information("Successfully created a certificate for {Domain}", domain);
        }
    }

    public Task Stop()
    {
        return Task.CompletedTask;
    }

    private async Task<AcmeContext> GetContext()
    {
        var keyPath = Path.Join(configManager.DataPath, $"{_accountName}.pem");

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