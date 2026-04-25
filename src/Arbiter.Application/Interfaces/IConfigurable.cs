using Microsoft.Extensions.Configuration;

namespace Arbiter.Application.Interfaces;

public interface IConfigurable
{
    void Bind(IConfiguration configuration);
}