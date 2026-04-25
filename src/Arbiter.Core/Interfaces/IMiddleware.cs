using Arbiter.Core.Aggregates;
using Microsoft.Extensions.Configuration;

namespace Arbiter.Core.Interfaces;

public delegate Task HandleDelegate(Context context);

public interface IMiddleware
{
    Task Configure(Site site, IConfiguration config);
    Task Handle(Context context);
}