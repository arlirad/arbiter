using Arbiter.Models.Config;
using Microsoft.Extensions.Configuration;

namespace Arbiter.Services.Configurators;

public interface IConfigurator
{
    Task Configure(IConfiguration config);
}