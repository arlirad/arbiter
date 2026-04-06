using Arbiter.Core.Aggregates;
using Microsoft.Extensions.Configuration;

namespace Arbiter.Core.Interfaces;

public delegate Task HandleDelegate(Context context);

public interface IMiddleware
{
    public Task Configure(Site site, IConfiguration config);
    public Task Handle(Context context);
}