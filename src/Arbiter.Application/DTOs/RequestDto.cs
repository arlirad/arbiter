using Arbiter.Domain.Enums;
using Arbiter.Domain.ValueObjects;

namespace Arbiter.Application.DTOs;

public class RequestDto
{
    public Method Method { get; set; }
    public string? Authority { get; set; }
    public string Path { get; set; } = null!;
    public ReadOnlyHeaders Headers { get; set; } = null!;
    public Stream? Stream { get; set; }
}