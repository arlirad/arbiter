using System.Net;
using Arbiter.Application.DTOs;

namespace Arbiter.Application.Interfaces;

public interface ITransaction
{
    string Protocol
    {
        get;
    }

    int Id
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

    Task<RequestDto?> GetRequest();
    Task SetResponse(ResponseDto response);
}