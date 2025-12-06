using System.Text;
using Arbiter.Domain.Aggregates;
using Arbiter.Domain.Enums;
using Arbiter.Domain.Interfaces;
using Arbiter.Infrastructure.Acme.Models;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Arbiter.Infrastructure.Acme;

internal class AcmeMiddleware(HandleDelegate next) : IMiddleware
{
    private const string AcmeChallengePathPrefix = "/.well-known/acme-challenge/";
    private DataModel? _data;

    public Task Configure(Site site, IConfiguration config)
    {
        _data = site.GetComponentData<DataModel>();
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
            Log.Warning("acme: Received a challenge query for '{Token}', which we have not requested", token);
            return;
        }

        Log.Information("acme: Received challenge query for '{Token}'", token);
        await context.Response.Set(Status.Ok, new MemoryStream(Encoding.ASCII.GetBytes(challenge.KeyAuthz)));
    }
}