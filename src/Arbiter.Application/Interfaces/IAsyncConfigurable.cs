using Microsoft.Extensions.Configuration;

namespace Arbiter.Application.Interfaces;

public interface IAsyncConfigurable
{
    ValueTask Bind(IConfiguration configuration);
}