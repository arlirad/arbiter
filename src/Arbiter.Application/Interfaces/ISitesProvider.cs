using System.Reactive;
using Arbiter.Application.Configuration;

namespace Arbiter.Application.Interfaces;

public interface ISitesProvider
{
    IObservable<Dictionary<string, SiteConfig>> ObserveSites();
}