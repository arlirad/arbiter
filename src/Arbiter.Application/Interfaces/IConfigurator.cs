using Microsoft.Extensions.Configuration;

namespace Arbiter.Application.Interfaces;

public interface IConfigurator
{
    Task Configure(IConfiguration config);
}