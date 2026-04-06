using Arbiter.Core.Enums;
using Arbiter.Core.ValueObjects;

namespace Arbiter.Application.DTOs;

public class ResponseDto
{
    public Status Status { get; set; }
    public Stream? Stream { get; set; }
    public ReadOnlyHeaders Headers { get; set; }
}