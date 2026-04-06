using System.Net;
using Arbiter.Core.Enums;
using Arbiter.Core.ValueObjects;

namespace Arbiter.Application.DTOs;

public class RequestDto
{
    public int TransactionId { get; set; }
    public Method Method { get; set; }
    public string? Authority { get; set; }
    public string Path { get; set; } = null!;
    public ReadOnlyHeaders Headers { get; set; } = null!;
    public Stream? Stream { get; set; }
    public bool IsWebSocketUpgrade { get; set; }
    public bool IsSecure { get; set; }
    public IPAddress? RemoteAddress { get; set; }
}