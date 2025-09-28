using System.Text;
using Arbiter.Enums;
using Arbiter.Models;
using Arbiter.Models.Components;
using Arbiter.Models.Network;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Arbiter.Middleware.Acme;

internal class AcmeMiddleware(HandleDelegate next) : IMiddleware
{
    private const string AcmeChallengePathPrefix = "/.well-known/acme-challenge/";
    private AcmeDataModel? _data;

    public Task Configure(Site site, IConfiguration config)
    {
        _data = site.GetComponentData<AcmeDataModel>();
        return Task.CompletedTask;
    }

    public async Task Handle(HttpContext context)
    {
        if (!context.Request.Uri.LocalPath.StartsWith(AcmeChallengePathPrefix))
        {
            await next(context);
            return;
        }

        var token = context.Request.Uri.LocalPath[AcmeChallengePathPrefix.Length..];
        var challenge = _data!.Challenges.FirstOrDefault(c => c.Token == token);

        if (challenge is null)
        {
            Log.Warning("acme: Received a challenge query for '{Token}', which we have not requested", token);
            return;
        }

        Log.Information("acme: Received challenge query for '{Token}'", token);
        await context.Response.Send(HttpStatusCode.Ok, new MemoryStream(Encoding.ASCII.GetBytes(challenge.KeyAuthz)));
    }
}