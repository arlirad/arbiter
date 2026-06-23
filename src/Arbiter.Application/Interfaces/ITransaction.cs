using System.Net;
using Arbiter.Application.DTOs;
using Arbiter.Core.Enums;

namespace Arbiter.Application.Interfaces;

public interface ITransaction
{
    int Id
    {
        get;
    }
    Protocol Protocol
    {
        get;
    }
    bool IsSecure
    {
        get;
    }
    int Port
    {
        get;
    }
    IPAddress? RemoteAddress
    {
        get;
    }

    Task<RequestDto?> GetRequest(CancellationToken ct = default);
    Task SetResponse(ResponseDto response, CancellationToken ct = default);
}
