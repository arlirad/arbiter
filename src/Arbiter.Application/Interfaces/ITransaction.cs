using Arbiter.Application.DTOs;

namespace Arbiter.Application.Interfaces;

public interface ITransaction
{
    bool IsSecure { get; }
    int Port { get; }

    Task<RequestDto?> GetRequest();
    Task SetResponse(ResponseDto response);
}