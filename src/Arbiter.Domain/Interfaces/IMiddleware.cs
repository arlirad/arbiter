using Arbiter.Domain.Aggregates;
using Microsoft.Extensions.Configuration;

namespace Arbiter.Domain.Interfaces;

public delegate Task HandleDelegate(Context context);

public interface IMiddleware
{
    public Task Configure(Site site, IConfiguration config);
    public Task Handle(Context context);
}