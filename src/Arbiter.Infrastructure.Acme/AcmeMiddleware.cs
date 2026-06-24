using System.Text;
using Arbiter.Application.Interfaces;
using Arbiter.Core.Aggregates;
using Arbiter.Core.Enums;
using Arbiter.Core.Interfaces;
using Arbiter.Infrastructure.Acme.Config;
using Arbiter.Infrastructure.Acme.Models;
using Serilog;

namespace Arbiter.Infrastructure.Acme;

internal class AcmeMiddleware(HandleDelegate next) : IConfigurableMiddleware<AcmeConfig>
{
    private const string AcmeChallengePathPrefix = "/.well-known/acme-challenge/";
    private static readonly ILogger Log = Serilog.Log.ForContext("SourceContext", "acme");
    private DataModel? _data;

    public Task Configure(ComponentDataContainer data, AcmeConfig config)
    {
        _data = data.Get<DataModel>();

        return Task.CompletedTask;
    }

    public async Task Handle(Context context)
    {
        if (!context.Request.Path.StartsWith(AcmeChallengePathPrefix))
        {
            await next(context);

            return;
        }

        var token = context.Request.Path[AcmeChallengePathPrefix.Length..];
        var challenge = _data!.Challenges.FirstOrDefault(c => c.Token == token);

        if (challenge is null)
        {
            Log.Warning("Received a challenge query for '{Token}', which we have not requested", token);

            return;
        }

        Log.Information("Received challenge query for '{Token}'", token);
        await context.Response.Set(Status.Ok, new MemoryStream(Encoding.ASCII.GetBytes(challenge.KeyAuthz)));
    }
}
